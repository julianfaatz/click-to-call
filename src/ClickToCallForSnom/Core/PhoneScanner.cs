using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ClickToCallForSnom.Core;

public sealed record FoundPhone(string Address, string Model, PhoneVendor Vendor)
{
    public string DisplayName => $"{Model} · {Address}";
}

/// <summary>Looks for supported phones in the local /24 network(s) by asking each address for its web page.</summary>
public static class PhoneScanner
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
            MaxResponseContentBufferSize = 1024 * 1024, // every device in the network answers, not only phones
        };

        await Parallel.ForEachAsync(targets, new ParallelOptions { MaxDegreeOfParallelism = 64, CancellationToken = ct }, async (ip, token) =>
        {
            if (await ProbeAsync(http, $"http://{ip}/", token) is { } phone)
                found.Add(phone with { Address = ip.ToString() });
        });

        var phones = found.OrderBy(p => IPAddress.Parse(p.Address).GetAddressBytes()[3]).ToList();
#if DEBUG
        if (MockPhone.Address is { } mock)
            phones.Insert(0, new FoundPhone(mock, MockPhone.Model + " (Mock)", PhoneVendor.Snom));
#endif
        return phones;
    }

    /// <returns>The phone (without address) if a driver recognizes the device, otherwise null.</returns>
    private static async Task<FoundPhone?> ProbeAsync(HttpClient http, string url, CancellationToken ct, bool followHttps = true)
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

            foreach (var driver in PhoneDrivers.All)
            {
                if (driver.Detect(text) is { } model)
                    return new FoundPhone("", model, driver.Vendor);
            }

            return null;
        }
        catch
        {
            return null;
        }
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
}
