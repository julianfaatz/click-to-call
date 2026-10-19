using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ClickToCall.Core;

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

/// <summary>
/// Looks for supported phones in the local network(s) and the default networks of common routers
/// by asking each address for its web page.
/// </summary>
public static class PhoneScanner
{
    /// <param name="progress">Share of the addresses checked so far, 0 to 1.</param>
    public static async Task<IReadOnlyList<FoundPhone>> ScanAsync(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var local = LocalSubnets().ToList();
        var networks = NetworksToScan(local.Select(s => (s.Network, s.PrefixLength)).ToList());
        var own = local.Select(s => s.Own).ToHashSet();
        var targets = networks.SelectMany(n => SubnetAddresses(n.Network, n.PrefixLength)).Where(ip => !own.Contains(ip)).Distinct().ToList();
        var found = new ConcurrentBag<FoundPhone>();
        var done = 0;
        var lastPercent = 0;

        using var http = CreateProbeClient(TimeSpan.FromMilliseconds(800));
        await Parallel.ForEachAsync(targets, new ParallelOptions { MaxDegreeOfParallelism = 128, CancellationToken = ct }, async (ip, token) =>
        {
            try
            {
                await ProbeAddressAsync(ip, token);
            }
            finally
            {
                // Only whole percents: Progress<T> posts every report to the UI thread
                var percent = Interlocked.Increment(ref done) * 100 / targets.Count;
                for (var last = Volatile.Read(ref lastPercent); last < percent; last = Volatile.Read(ref lastPercent))
                {
                    if (Interlocked.CompareExchange(ref lastPercent, percent, last) == last)
                    {
                        progress?.Report(percent / 100.0);
                        break;
                    }
                }
            }
        });

        var phones = found.OrderBy(p => ToUInt32(IPAddress.Parse(p.Address))).ToList();
        Log.Info($"Network scan: {targets.Count} addresses in {string.Join(", ", networks.Select(n => $"{n.Network}/{n.PrefixLength}"))}, "
            + $"found {(phones.Count == 0 ? "nothing" : string.Join(", ", phones.Select(p => p.DisplayName)))}");
#if DEBUG
        if (MockPhone.Address is { } mock)
            phones.Insert(0, new FoundPhone(mock, MockPhone.Model + " (Mock)", MockPhone.Vendor));
#endif
        return phones;

        async Task ProbeAddressAsync(IPAddress ip, CancellationToken token)
        {
            // Most addresses have no device: find that out without an HTTP request, whose timeout
            // would throw (thousands of first chance exceptions per scan in the debugger)
            var http80 = IsPortOpenAsync(ip, 80, token);
            var https443 = IsPortOpenAsync(ip, 443, token);
            var url = await http80 ? $"http://{ip}/" : await https443 ? $"https://{ip}/" : null;

            if (url is not null && Recognize(await ProbeAsync(http, url, token)) is { } phone)
                found.Add(phone with { Address = ip.ToString() });
        }
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

    /// <summary>Smallest prefix length that is scanned completely (/22 = 1022 addresses, a few seconds).</summary>
    private const int MinPrefixLength = 22;

    /// <summary>
    /// Default networks of common routers. Phones are often connected to the router acting as PBX
    /// (e.g. a FRITZ!Box) while the PC is in another network – still reachable, but not in its own subnet.
    /// </summary>
    private static readonly IPAddress[] CommonNetworks =
    [
        IPAddress.Parse("192.168.178.0"), // AVM FRITZ!Box
        IPAddress.Parse("192.168.0.0"),
        IPAddress.Parse("192.168.1.0"),
        IPAddress.Parse("192.168.2.0"), // Telekom Speedport
    ];

    /// <summary>The own networks first, then the common router networks (/24) they don't already cover.</summary>
    internal static List<(IPAddress Network, int PrefixLength)> NetworksToScan(IReadOnlyCollection<(IPAddress Network, int PrefixLength)> local)
    {
        var networks = local.Distinct().ToList();
        foreach (var common in CommonNetworks)
        {
            if (!networks.Any(n => NetworkAddress(common, n.PrefixLength).Equals(n.Network)))
                networks.Add((common, 24));
        }

        return networks;
    }

    private static IEnumerable<(IPAddress Network, int PrefixLength, IPAddress Own)> LocalSubnets()
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

                // Company networks are often larger than /24 (phone in 10.0.1.x, PC in 10.0.0.x).
                // Larger ones are only scanned around the own address, smaller ones still as /24 as before.
                var prefix = Math.Clamp(unicast.PrefixLength, MinPrefixLength, 24);
                yield return (NetworkAddress(unicast.Address, prefix), prefix, unicast.Address);
            }
        }
    }

    internal static IPAddress NetworkAddress(IPAddress address, int prefixLength)
    {
        var mask = prefixLength == 0 ? 0 : uint.MaxValue << (32 - prefixLength);
        return ToAddress(ToUInt32(address) & mask);
    }

    /// <summary>All host addresses of the subnet, without network and broadcast address.</summary>
    internal static IEnumerable<IPAddress> SubnetAddresses(IPAddress network, int prefixLength)
    {
        var first = ToUInt32(network);
        var count = 1u << (32 - prefixLength);
        for (var i = 1u; i < count - 1; i++)
            yield return ToAddress(first + i);
    }

    private static uint ToUInt32(IPAddress address) => BitConverter.ToUInt32(address.GetAddressBytes().Reverse().ToArray());

    private static IPAddress ToAddress(uint value) => new(BitConverter.GetBytes(value).Reverse().ToArray());
}
