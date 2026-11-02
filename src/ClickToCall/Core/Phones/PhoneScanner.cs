using System.Collections.Concurrent;
using System.Net;
using ClickToCall.Core.Network;

namespace ClickToCall.Core.Phones;

/// <summary>
/// Looks for supported phones in the local network(s) and the default networks of common routers
/// by asking each address for its web page.
/// </summary>
public static class PhoneScanner
{
    private static readonly TimeSpan ScanTimeout = TimeSpan.FromMilliseconds(800);

    public static async Task<IReadOnlyList<FoundPhone>> ScanAsync(IProgress<ScanUpdate>? progress = null, CancellationToken ct = default)
    {
        var own = NetworkInfo.LocalAddresses().ToList();
        var networks = ScanNetworks.For(own.Select(a => ScanNetworks.Around(a.Address, a.PrefixLength)));
        var ownAddresses = own.Select(a => a.Address).ToHashSet();
        var targets = networks
            .SelectMany(n => n.Hosts().Select(ip => (Ip: ip, Network: n.Label)))
            .Where(t => !ownAddresses.Contains(t.Ip))
            .DistinctBy(t => t.Ip)
            .ToList();
        var found = new ConcurrentBag<FoundPhone>();
        var unrecognized = new ConcurrentBag<(IPAddress Ip, ProbeResponse Response)>();
        var scanProgress = new ScanProgress(progress, targets.Count);

        using var probe = new WebProbe(ScanTimeout);
        await Parallel.ForEachAsync(targets, new ParallelOptions { MaxDegreeOfParallelism = 128, CancellationToken = ct }, async (target, token) =>
        {
            try
            {
                if (await StartPageAsync(target.Ip, token) is not { } url)
                    return;

                var response = await probe.GetAsync(url, token);
                var phone = PhoneDrivers.Recognize(target.Ip.ToString(), response);
                if (phone is not null)
                    found.Add(phone);
                else
                    unrecognized.Add((target.Ip, response));
                scanProgress.DeviceAnswered(target.Network, target.Ip, phone);
            }
            finally
            {
                scanProgress.AddressChecked(target.Network);
            }
        });

        var phones = found.OrderBy(p => Subnet.ToUInt32(IPAddress.Parse(p.Address))).ToList();
        Log.Info($"Network scan: {targets.Count} addresses in {string.Join(", ", networks)}, "
            + $"found {(phones.Count == 0 ? "nothing" : string.Join(", ", phones.Select(p => p.DisplayName)))}");
        // So a phone that was not found can be told apart from one that answered unexpectedly
        foreach (var (ip, response) in unrecognized.OrderBy(d => Subnet.ToUInt32(d.Ip)))
            Log.Info($"Network scan: not recognized {ip}: {response}");
#if DEBUG
        if (MockPhone.Address is { } mock)
        {
            var mockPhone = new FoundPhone(mock, MockPhone.Model + " (Mock)", MockPhone.Vendor);
            phones.Insert(0, mockPhone);
            progress?.Report(new ScanUpdate(1, "", IPAddress.Loopback, mockPhone));
        }
#endif
        return phones;
    }

    /// <summary>Finds out which phone (if any) answers at a typed address.</summary>
    public static async Task<(ProbeStatus Status, FoundPhone? Phone)> IdentifyAsync(string host, CancellationToken ct = default)
    {
        using var probe = new WebProbe(TimeSpan.FromSeconds(3));
        // Some phones only listen on HTTPS
        var plain = await probe.GetAsync($"http://{host}/", ct);
        var response = plain.Answered ? plain : await probe.GetAsync($"https://{host}/", ct);
        var phone = PhoneDrivers.Recognize(host, response);
        var status = !response.Answered ? ProbeStatus.Unreachable : phone is null ? ProbeStatus.UnknownDevice : ProbeStatus.Found;

        Log.Info($"Identify {host}: {(phone?.Model ?? status.ToString())} - {(response == plain ? "" : plain + "; ")}{response}");
        return (status, phone);
    }

    /// <summary>
    /// The start page URL if the address has a web server. Most addresses have no device: find that out without
    /// an HTTP request, whose timeout would throw (thousands of first chance exceptions per scan in the debugger).
    /// </summary>
    private static async Task<string?> StartPageAsync(IPAddress ip, CancellationToken ct)
    {
        var http = PortProbe.IsOpenAsync(ip, 80, ScanTimeout, ct);
        var https = PortProbe.IsOpenAsync(ip, 443, ScanTimeout, ct);
        return await http ? $"http://{ip}/" : await https ? $"https://{ip}/" : null;
    }
}
