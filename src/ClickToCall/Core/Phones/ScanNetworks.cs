using System.Net;
using ClickToCall.Core.Network;

namespace ClickToCall.Core.Phones;

/// <summary>Which networks the phone scan covers.</summary>
public static class ScanNetworks
{
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

    /// <summary>
    /// The part of the PC's own network that is scanned. Company networks are often larger than /24
    /// (phone in 10.0.1.x, PC in 10.0.0.x): larger ones are only scanned around the own address, smaller ones as /24.
    /// </summary>
    public static Subnet Around(IPAddress ownAddress, int prefixLength) =>
        Subnet.Of(ownAddress, Math.Clamp(prefixLength, MinPrefixLength, 24));

    /// <summary>The own networks first, then the common router networks (/24) they don't already cover.</summary>
    public static List<Subnet> For(IEnumerable<Subnet> own)
    {
        var networks = own.Distinct().ToList();
        foreach (var common in CommonNetworks)
        {
            if (!networks.Any(n => n.Contains(common)))
                networks.Add(new Subnet(common, 24));
        }

        return networks;
    }
}
