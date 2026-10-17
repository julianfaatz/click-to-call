using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace ClickToCallForSnom.Core;

public sealed record PhoneConnection(string Host, bool UseHttps, bool IgnoreCertificateErrors, string Username, string Password)
{
    public static PhoneConnection From(AppSettings s) =>
        new(s.PhoneHost, s.UseHttps, s.IgnoreCertificateErrors, s.Username, s.Password);
}

public enum PhoneErrorKind
{
    NotConfigured,
    InvalidAddress,
    Unreachable,
    Timeout,
    Unauthorized,
    Forbidden,
    Certificate,
    HttpError,
}

public sealed class PhoneException(PhoneErrorKind kind, string? detail = null, Exception? inner = null)
    : Exception(detail ?? kind.ToString(), inner)
{
    public PhoneErrorKind Kind { get; } = kind;
    public string? Detail { get; } = detail;

    public string UserMessage => Kind switch
    {
        PhoneErrorKind.HttpError => Loc.T("Error.HttpError", Detail ?? "?"),
        PhoneErrorKind.InvalidAddress => Loc.T("Error.InvalidAddress", Detail ?? ""),
        _ => Loc.T("Error." + Kind),
    };
}

/// <summary>
/// Talks to the HTTP interface of Snom desk phones (D1xx, D3xx, D7xx, D8xx).
/// Dialing:  GET /command.htm?number=...&amp;outgoing_uri=user@host
/// Hang up:  GET /command.htm?key=CANCEL
/// </summary>
public sealed partial class SnomPhoneClient : IDisposable
{
    private readonly HttpClient _http;

    public SnomPhoneClient(PhoneConnection connection, TimeSpan? timeout = null)
    {
        var host = StripScheme(connection.Host);
        if (string.IsNullOrWhiteSpace(host))
            throw new PhoneException(PhoneErrorKind.NotConfigured);

        var handler = new HttpClientHandler
        {
            // Supports both Basic and Digest authentication
            Credentials = string.IsNullOrEmpty(connection.Username)
                ? null
                : new NetworkCredential(connection.Username, connection.Password),
            PreAuthenticate = true,
            UseProxy = false,
        };

        // Also needed for HTTP: phones set to HTTPS only redirect there
        if (connection.IgnoreCertificateErrors)
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        // e.g. typos with spaces or other characters that are not allowed in a host name
        if (!Uri.TryCreate($"{(connection.UseHttps ? "https" : "http")}://{host}/", UriKind.Absolute, out var baseAddress))
        {
            handler.Dispose();
            throw new PhoneException(PhoneErrorKind.InvalidAddress, host);
        }

        _http = new HttpClient(handler)
        {
            BaseAddress = baseAddress,
            Timeout = timeout ?? TimeSpan.FromSeconds(6),
            // The phone's pages are small – don't buffer whatever a wrong device sends back
            MaxResponseContentBufferSize = 4 * 1024 * 1024,
        };
    }

    public Uri BaseAddress => _http.BaseAddress!;

    public async Task DialAsync(string number, string? outgoingUri = null, CancellationToken ct = default)
    {
        var query = "command.htm?number=" + Uri.EscapeDataString(number);
        if (!string.IsNullOrWhiteSpace(outgoingUri))
            query += "&outgoing_uri=" + Uri.EscapeDataString(outgoingUri);

        await GetStringAsync(query, ct);
    }

    public Task HangUpAsync(CancellationToken ct = default) => GetStringAsync("command.htm?key=CANCEL", ct);

    /// <summary>Checks reachability and credentials. Returns the phone's page title (usually contains the model).</summary>
    public async Task<string> TestConnectionAsync(CancellationToken ct = default)
    {
        string html;
        try
        {
            html = await GetStringAsync("info.htm", ct);
        }
        catch (PhoneException ex) when (ex.Kind == PhoneErrorKind.HttpError && ex.Detail == "404")
        {
            html = await GetStringAsync("", ct);
        }

        var title = TitleRegex().Match(html);
        return title.Success ? WebUtility.HtmlDecode(title.Groups[1].Value).Trim() : "Snom";
    }

    /// <summary>Reads the configured SIP identities from /settings.xml.</summary>
    public async Task<IReadOnlyList<PhoneIdentity>> GetIdentitiesAsync(CancellationToken ct = default)
    {
        var xml = await GetStringAsync("settings.xml", ct);
        return ParseIdentities(xml);
    }

    internal static IReadOnlyList<PhoneIdentity> ParseIdentities(string xml)
    {
        // The phone is trusted only so far: never process DTDs / entity definitions from it
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
        var doc = XDocument.Load(reader);

        Dictionary<int, string> Values(string name) => doc.Descendants(name)
            .Where(e => int.TryParse((string?)e.Attribute("idx"), out _))
            .GroupBy(e => int.Parse((string)e.Attribute("idx")!))
            .ToDictionary(g => g.Key, g => g.First().Value.Trim());

        var names = Values("user_name");
        var hosts = Values("user_host");
        var realNames = Values("user_realname");
        var active = Values("user_active");

        var identities = new List<PhoneIdentity>();
        foreach (var (idx, user) in names.OrderBy(kv => kv.Key))
        {
            if (string.IsNullOrEmpty(user) || !hosts.TryGetValue(idx, out var host) || string.IsNullOrEmpty(host))
                continue;
            if (active.TryGetValue(idx, out var isActive) && isActive == "off")
                continue;

            realNames.TryGetValue(idx, out var realName);
            identities.Add(new PhoneIdentity(idx, string.IsNullOrEmpty(realName) ? Loc.T("Identity.Numbered", idx) : realName, $"{user}@{host}"));
        }

        return identities;
    }

    private async Task<string> GetStringAsync(string relativeUrl, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(relativeUrl, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new PhoneException(PhoneErrorKind.Timeout, inner: ex);
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException)
        {
            throw new PhoneException(PhoneErrorKind.Certificate, ex.InnerException.Message, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new PhoneException(PhoneErrorKind.Unreachable, ex.Message, ex);
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    throw new PhoneException(PhoneErrorKind.Unauthorized);
                case HttpStatusCode.Forbidden:
                    throw new PhoneException(PhoneErrorKind.Forbidden);
                case var code when !response.IsSuccessStatusCode:
                    throw new PhoneException(PhoneErrorKind.HttpError, ((int)code).ToString());
            }

            return await response.Content.ReadAsStringAsync(ct);
        }
    }

    private static string StripScheme(string host)
    {
        host = host.Trim();
        var idx = host.IndexOf("://", StringComparison.Ordinal);
        if (idx >= 0)
            host = host[(idx + 3)..];
        return host.TrimEnd('/');
    }

    [GeneratedRegex("<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    public void Dispose() => _http.Dispose();
}
