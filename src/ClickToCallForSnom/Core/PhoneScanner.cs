using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace ClickToCallForSnom.Core;

public sealed record FoundPhone(string Address, string Model)
{
    public string DisplayName => $"{Model} · {Address}";
}

/// <summary>Looks for Snom phones in the local /24 network(s) by asking each address for its web page.</summary>
public static partial class PhoneScanner
{
    public static async Task<IReadOnlyList<FoundPhone>> ScanAsync(CancellationToken ct = default)
    {
        var targets = LocalSubnetAddresses().Distinct().ToList();
        var found = new ConcurrentBag<FoundPhone>();

        using var http = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            ConnectTimeout = TimeSpan.FromMilliseconds(800),
            SslOptions = { RemoteCertificateValidationCallback = (_, _, _, _) => true },
        })
        {
            Timeout = TimeSpan.FromSeconds(3),
        };

        await Parallel.ForEachAsync(targets, new ParallelOptions { MaxDegreeOfParallelism = 64, CancellationToken = ct }, async (ip, token) =>
        {
            var model = await ProbeAsync(http, $"http://{ip}/", token);
            if (model is not null)
                found.Add(new FoundPhone(ip.ToString(), model));
        });

        return found.OrderBy(p => IPAddress.Parse(p.Address).GetAddressBytes()[3]).ToList();
    }

    /// <returns>The model name if the address looks like a Snom phone, otherwise null.</returns>
    private static async Task<string?> ProbeAsync(HttpClient http, string url, CancellationToken ct, bool followHttps = true)
    {
        try
        {
            using var response = await http.GetAsync(url, ct);

            // Phones configured for HTTPS only redirect there
            if (followHttps && response.Headers.Location is { Scheme: "https" } location)
                return await ProbeAsync(http, location.ToString(), ct, followHttps: false);

            var text = string.Join(' ',
                response.Headers.Server.ToString(),
                response.Headers.WwwAuthenticate.ToString(),
                await response.Content.ReadAsStringAsync(ct));

            return text.Contains("snom", StringComparison.OrdinalIgnoreCase) ? ExtractModel(text) : null;
        }
        catch
        {
            return null;
        }
    }

    internal static string ExtractModel(string text)
    {
        var match = ModelRegex().Match(text);
        return match.Success ? "Snom " + match.Groups[1].Value.ToUpperInvariant() : "Snom";
    }

    private static IEnumerable<IPAddress> LocalSubnetAddresses()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up
                || nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;

            var props = nic.GetIPProperties();
            // Interfaces without a gateway are usually virtual (Hyper-V, VPN adapters, ...)
            if (!props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
                continue;

            foreach (var unicast in props.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                var bytes = unicast.Address.GetAddressBytes();
                for (var i = 1; i < 255; i++)
                {
                    if (i == bytes[3])
                        continue;
                    yield return new IPAddress([bytes[0], bytes[1], bytes[2], (byte)i]);
                }
            }
        }
    }

    [GeneratedRegex(@"snom\s*-?\s*([A-Z]\d{3,4}[A-Z]*)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ModelRegex();
}
