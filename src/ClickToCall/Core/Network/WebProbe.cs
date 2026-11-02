using System.Net.Http;

namespace ClickToCall.Core.Network;

/// <summary>Asks a device for its start page – to find out what it is.</summary>
public sealed class WebProbe : IDisposable
{
    private const int MaxRedirects = 2;

    private readonly HttpClient _http;

    public WebProbe(TimeSpan connectTimeout)
    {
        _http = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            ConnectTimeout = connectTimeout,
            SslOptions = { RemoteCertificateValidationCallback = (_, _, _, _) => true },
        })
        {
            Timeout = TimeSpan.FromSeconds(3),
            MaxResponseContentBufferSize = 1024 * 1024, // every device in the network answers, not only phones
        };
    }

    /// <summary>The page at the URL; follows redirects on the same host. Never throws, except when cancelled.</summary>
    public Task<ProbeResponse> GetAsync(string url, CancellationToken ct) => GetAsync(url, 0, ct);

    private async Task<ProbeResponse> GetAsync(string url, int redirects, CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(url, ct);
            var result = new ProbeResponse(url, (int)response.StatusCode,
                response.Headers.Server.ToString(),
                response.Headers.WwwAuthenticate.ToString(),
                response.Headers.Location?.ToString() ?? "",
                await response.Content.ReadAsStringAsync(ct));

            // HTTPS-only phones redirect there, others to their login page. The redirect is kept:
            // the target may not answer (e.g. HTTPS the PC cannot talk to), and it can tell which device this is.
            if (redirects < MaxRedirects && response.Headers.Location is { } location)
            {
                var target = location.IsAbsoluteUri ? location : new Uri(new Uri(url), location);
                if (target.Host == new Uri(url).Host)
                    return await GetAsync(target.ToString(), redirects + 1, ct) with { Previous = result };
            }

            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // e.g. a TLS handshake the phone does not support: worth seeing in the log
            return new ProbeResponse(url, Error: ex is TaskCanceledException ? "timeout" : (ex.InnerException ?? ex).Message);
        }
    }

    public void Dispose() => _http.Dispose();
}
