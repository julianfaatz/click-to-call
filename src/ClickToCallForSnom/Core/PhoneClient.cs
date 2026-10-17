using System.Net;
using System.Net.Http;
using System.Security.Authentication;

namespace ClickToCallForSnom.Core;

/// <summary>Phone manufacturers the app can talk to. Stored in the settings, so never rename a value.</summary>
public enum PhoneVendor
{
    Snom,
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
    private readonly HttpClient _http;

    protected HttpPhoneClient(PhoneConnection connection, TimeSpan? timeout = null)
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

    public abstract Task DialAsync(string number, string? outgoingUri = null, CancellationToken ct = default);

    public abstract Task HangUpAsync(CancellationToken ct = default);

    public abstract Task<string> TestConnectionAsync(CancellationToken ct = default);

    public virtual Task<IReadOnlyList<PhoneIdentity>> GetIdentitiesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PhoneIdentity>>([]);

    protected async Task<string> GetStringAsync(string relativeUrl, CancellationToken ct)
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

    public void Dispose() => _http.Dispose();
}
