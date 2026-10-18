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

public enum ProbeStatus
{
    Unreachable,
    UnknownDevice,
    Found,
}

/// <summary>Looks for supported phones in the local /24 network(s) by asking each address for its web page.</summary>
public static class PhoneScanner
{
    public static async Task<IReadOnlyList<FoundPhone>> ScanAsync(CancellationToken ct = default)
    {
        var targets = LocalSubnetAddresses().Distinct().ToList();
        var found = new ConcurrentBag<FoundPhone>();

        using var http = CreateProbeClient(TimeSpan.FromMilliseconds(800));
        await Parallel.ForEachAsync(targets, new ParallelOptions { MaxDegreeOfParallelism = 64, CancellationToken = ct }, async (ip, token) =>
        {
            // Most addresses have no device: find that out without an HTTP request, whose timeout
            // would throw (thousands of first chance exceptions per scan in the debugger)
            var http80 = IsPortOpenAsync(ip, 80, token);
            var https443 = IsPortOpenAsync(ip, 443, token);
            var url = await http80 ? $"http://{ip}/" : await https443 ? $"https://{ip}/" : null;

            if (url is not null && Recognize(await ProbeAsync(http, url, token)) is { } phone)
                found.Add(phone with { Address = ip.ToString() });
        });

        var phones = found.OrderBy(p => IPAddress.Parse(p.Address).GetAddressBytes()[3]).ToList();
#if DEBUG
        if (MockPhone.Address is { } mock)
            phones.Insert(0, new FoundPhone(mock, MockPhone.Model + " (Mock)", MockPhone.Vendor));
#endif
        return phones;
    }

    /// <summary>Finds out which phone (if any) answers at a typed address.</summary>
    public static async Task<(ProbeStatus Status, FoundPhone? Phone)> IdentifyAsync(string host, CancellationToken ct = default)
    {
        using var http = CreateProbeClient(TimeSpan.FromSeconds(3));
        // Some phones only listen on HTTPS
        var text = await ProbeAsync(http, $"http://{host}/", ct) ?? await ProbeAsync(http, $"https://{host}/", ct);
        if (text is null)
            return (ProbeStatus.Unreachable, null);

        return Recognize(text) is { } phone
            ? (ProbeStatus.Found, phone with { Address = host })
            : (ProbeStatus.UnknownDevice, null);
    }

    /// <summary>
    /// Whether something accepts TCP connections on the port. Uses the event based socket API:
    /// cancelling it completes with an error code instead of throwing.
    /// </summary>
    private static async Task<bool> IsPortOpenAsync(IPAddress address, int port, CancellationToken ct)
    {
        using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        using var args = new SocketAsyncEventArgs { RemoteEndPoint = new IPEndPoint(address, port) };
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        args.Completed += (_, e) => done.TrySetResult(e.SocketError == SocketError.Success);

        if (!socket.ConnectAsync(args))
            return args.SocketError == SocketError.Success; // completed synchronously

        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, ct);
        await using (linked.Token.Register(() => Socket.CancelConnectAsync(args)))
            return await done.Task;
    }

    private static HttpClient CreateProbeClient(TimeSpan connectTimeout) => new(new SocketsHttpHandler
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

    /// <summary>The phone (without address) if a driver recognizes the device, otherwise null.</summary>
    private static FoundPhone? Recognize(string? probeText)
    {
        if (probeText is null)
            return null;

        foreach (var driver in PhoneDrivers.All)
        {
            if (driver.Detect(probeText) is { } model)
                return new FoundPhone("", model, driver.Vendor);
        }

        return null;
    }

    /// <returns>Server header, login realm and page of the device, or null if nothing answers.</returns>
    private static async Task<string?> ProbeAsync(HttpClient http, string url, CancellationToken ct, int redirects = 0)
    {
        try
        {
            using var response = await http.GetAsync(url, ct);

            // HTTPS-only phones redirect there, others to their login page
            if (redirects < 2 && response.Headers.Location is { } location)
            {
                var target = location.IsAbsoluteUri ? location : new Uri(new Uri(url), location);
                if (target.Host == new Uri(url).Host)
                    return await ProbeAsync(http, target.ToString(), ct, redirects + 1);
            }

            return string.Join(' ',
                response.Headers.Server.ToString(),
                response.Headers.WwwAuthenticate.ToString(),
                await response.Content.ReadAsStringAsync(ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
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
