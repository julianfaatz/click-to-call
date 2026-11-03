using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;

namespace ClickToCall.Core.Phones;

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
            // With the login sent up front, a phone may still ask for Digest instead: then the handler answers that
            Credentials = hasLogin ? CreateCredentials(baseAddress, connection, digestOnly: sendBasicAuthUpfront) : null,
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
    private static CredentialCache CreateCredentials(Uri baseAddress, PhoneConnection connection, bool digestOnly)
    {
        var credential = new NetworkCredential(connection.Username, connection.Password);
        var credentials = new CredentialCache { { baseAddress, "Digest", credential } };
        if (!digestOnly)
            credentials.Add(baseAddress, "Basic", credential);
        return credentials;
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

    /// <summary>Whether anything answers at the address – an error page or a login request count, too.</summary>
    protected async Task<bool> AnswersAsync(Uri url, CancellationToken ct)
    {
        try
        {
            await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, url), ct);
            return true;
        }
        catch (PhoneException ex) when (ex.Kind is PhoneErrorKind.Unreachable or PhoneErrorKind.Timeout or PhoneErrorKind.Certificate)
        {
            return ex.RedirectedTo is not null;
        }
        catch (PhoneException)
        {
            return true;
        }
    }

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
                        throw new PhoneException(PhoneErrorKind.Unreachable, $"Redirected to {target}") { RedirectedTo = target };

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
