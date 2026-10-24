using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;

namespace ClickToCall.Core;

/// <summary>Phone manufacturers the app can talk to. Stored in the settings, so never rename a value.</summary>
public enum PhoneVendor
{
    Snom,
    Yealink,
}

public sealed record PhoneConnection(
    string Host, bool UseHttps, bool IgnoreCertificateErrors, string Username, string Password, PhoneVendor Vendor = PhoneVendor.Snom)
{
    public static PhoneConnection From(AppSettings s) =>
        new(s.PhoneHost, s.UseHttps, s.IgnoreCertificateErrors, s.Username, s.Password, s.Vendor);
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
    /// <summary>The phone answers, but refuses remote control commands (Snom: "hidden tags").</summary>
    RemoteControlBlocked,
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

/// <summary>What the app needs from a phone. Create one with <see cref="PhoneDrivers.CreateClient"/>.</summary>
public interface IPhoneClient : IDisposable
{
    Task DialAsync(string number, string? outgoingUri = null, CancellationToken ct = default);

    Task HangUpAsync(CancellationToken ct = default);

    /// <summary>Checks reachability and credentials. Returns the phone's model name for display.</summary>
    Task<string> TestConnectionAsync(CancellationToken ct = default);

    /// <summary>The phone's SIP identities (lines). Only called if <see cref="IPhoneDriver.CanReadIdentities"/>.</summary>
    Task<IReadOnlyList<PhoneIdentity>> GetIdentitiesAsync(CancellationToken ct = default);
}

/// <summary>
/// Base for phones with an HTTP interface: Basic/Digest login, self-signed certificates and
/// the mapping of network and HTTP errors to <see cref="PhoneException"/>.
/// </summary>
public abstract class HttpPhoneClient : IPhoneClient
{
    private const int MaxRedirects = 3;
    private readonly HttpClient _http;
    private readonly bool _followRedirects;

    /// <param name="sendBasicAuthUpfront">
    /// Send the login with every request instead of waiting for the phone to ask for it
    /// (for phones that answer without a login challenge).
    /// </param>
    protected HttpPhoneClient(PhoneConnection connection, TimeSpan? timeout = null, bool sendBasicAuthUpfront = false)
    {
        var host = NormalizeHost(connection.Host);
        if (string.IsNullOrWhiteSpace(host))
            throw new PhoneException(PhoneErrorKind.NotConfigured);

        // e.g. typos with spaces or other characters that are not allowed in a host name
        if (!Uri.TryCreate($"{(connection.UseHttps ? "https" : "http")}://{host}/", UriKind.Absolute, out var baseAddress))
            throw new PhoneException(PhoneErrorKind.InvalidAddress, host);

        var hasLogin = !string.IsNullOrEmpty(connection.Username);
        var handler = new HttpClientHandler
        {
            Credentials = hasLogin && !sendBasicAuthUpfront ? CreateCredentials(baseAddress, connection) : null,
            PreAuthenticate = true,
            UseProxy = false,
            // .NET drops a preset Authorization header on redirects, so we follow them ourselves then
            AllowAutoRedirect = !sendBasicAuthUpfront,
        };
        _followRedirects = sendBasicAuthUpfront;

        // Also needed for HTTP: phones set to HTTPS only redirect there
        if (connection.IgnoreCertificateErrors)
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        _http = new HttpClient(handler)
        {
            BaseAddress = baseAddress,
            Timeout = timeout ?? TimeSpan.FromSeconds(6),
            // The phone's pages are small – don't buffer whatever a wrong device sends back
            MaxResponseContentBufferSize = 4 * 1024 * 1024,
        };

        if (hasLogin && sendBasicAuthUpfront)
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{connection.Username}:{connection.Password}"));
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        }
    }

    public Uri BaseAddress => _http.BaseAddress!;

    /// <summary>
    /// Basic and Digest login for the phone's address only. .NET drops a plain <see cref="NetworkCredential"/>
    /// on redirects, but keeps a <see cref="CredentialCache"/> (it is tied to URIs). Without that, a phone that
    /// redirects after command.htm dials, but the redirected page answers "Unauthorized".
    /// </summary>
    private static CredentialCache CreateCredentials(Uri baseAddress, PhoneConnection connection)
    {
        var credential = new NetworkCredential(connection.Username, connection.Password);
        return new CredentialCache
        {
            { baseAddress, "Basic", credential },
            { baseAddress, "Digest", credential },
        };
    }

    /// <summary>"http://192.168.1.50/" → "192.168.1.50".</summary>
    internal static string NormalizeHost(string host)
    {
        host = host.Trim();
        var idx = host.IndexOf("://", StringComparison.Ordinal);
        if (idx >= 0)
            host = host[(idx + 3)..];
        return host.TrimEnd('/');
    }

    public abstract Task DialAsync(string number, string? outgoingUri = null, CancellationToken ct = default);

    public abstract Task HangUpAsync(CancellationToken ct = default);

    public abstract Task<string> TestConnectionAsync(CancellationToken ct = default);

    public virtual Task<IReadOnlyList<PhoneIdentity>> GetIdentitiesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PhoneIdentity>>([]);

    /// <summary>The phone accepted the connection, but closed it without sending a response.</summary>
    protected static bool IsClosedWithoutResponse(PhoneException ex)
    {
        for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is HttpRequestException { HttpRequestError: HttpRequestError.ResponseEnded }
                or HttpIOException { HttpRequestError: HttpRequestError.ResponseEnded })
                return true;
        }

        return false;
    }

    protected Task<string> GetStringAsync(string relativeUrl, CancellationToken ct) =>
        SendAsync(() => new HttpRequestMessage(HttpMethod.Get, relativeUrl), ct);

    protected Task<string> PostFormAsync(string relativeUrl, IEnumerable<KeyValuePair<string, string>> form, CancellationToken ct) =>
        SendAsync(() => new HttpRequestMessage(HttpMethod.Post, relativeUrl) { Content = new FormUrlEncodedContent(form) }, ct);

    private async Task<string> SendAsync(Func<HttpRequestMessage> createRequest, CancellationToken ct)
    {
        var request = createRequest();
        for (var redirects = 0; ; redirects++)
        {
            HttpResponseMessage response;
            try
            {
                using (request)
                    response = await _http.SendAsync(request, ct);
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
                if (_followRedirects && (int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
                {
                    var target = location.IsAbsoluteUri ? location : new Uri(BaseAddress, location);
                    // A phone set to HTTPS only redirects there: the caller tries HTTPS itself (see PhoneConnector)
                    if (target.Scheme != BaseAddress.Scheme || target.Authority != BaseAddress.Authority || redirects >= MaxRedirects)
                        throw new PhoneException(PhoneErrorKind.Unreachable, $"Redirected to {target}");

                    request = createRequest();
                    request.RequestUri = target;
                    continue;
                }

                switch (response.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        // For the log: which page refused (after redirects) and which logins the phone offers
                        var schemes = string.Join(", ", response.Headers.WwwAuthenticate.Select(h => h.Scheme));
                        throw new PhoneException(PhoneErrorKind.Unauthorized,
                            $"401 from {response.RequestMessage?.RequestUri?.AbsolutePath} ({(schemes.Length > 0 ? schemes : "no challenge")})");
                    case HttpStatusCode.Forbidden:
                        throw new PhoneException(PhoneErrorKind.Forbidden);
                    case var code when !response.IsSuccessStatusCode:
                        throw new PhoneException(PhoneErrorKind.HttpError, ((int)code).ToString());
                }

                return await response.Content.ReadAsStringAsync(ct);
            }
        }
    }

    public void Dispose() => _http.Dispose();
}
