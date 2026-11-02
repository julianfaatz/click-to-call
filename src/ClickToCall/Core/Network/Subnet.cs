using System.Net;

namespace ClickToCall.Core.Network;

/// <summary>An IPv4 network, e.g. 192.168.178.0/24.</summary>
public readonly record struct Subnet(IPAddress Network, int PrefixLength)
{
    /// <summary>The network that contains the address, e.g. 192.168.178.36 with /24 → 192.168.178.0/24.</summary>
    public static Subnet Of(IPAddress address, int prefixLength)
    {
        var mask = prefixLength == 0 ? 0 : uint.MaxValue << (32 - prefixLength);
        return new Subnet(ToAddress(ToUInt32(address) & mask), prefixLength);
    }

    /// <summary>"192.168.178.x" for a /24 network, otherwise "10.0.0.0/22".</summary>
    public string Label => PrefixLength == 24 ? Network.ToString()[..Network.ToString().LastIndexOf('.')] + ".x" : ToString();

    /// <summary>All host addresses, without network and broadcast address.</summary>
    public IEnumerable<IPAddress> Hosts()
    {
        var first = ToUInt32(Network);
        var count = 1u << (32 - PrefixLength);
        for (var i = 1u; i < count - 1; i++)
            yield return ToAddress(first + i);
    }

    public bool Contains(IPAddress address) => Of(address, PrefixLength) == this;

    public override string ToString() => $"{Network}/{PrefixLength}";

    /// <summary>The address as a number, e.g. for sorting addresses numerically.</summary>
    public static uint ToUInt32(IPAddress address) => BitConverter.ToUInt32(address.GetAddressBytes().Reverse().ToArray());

    private static IPAddress ToAddress(uint value) => new(BitConverter.GetBytes(value).Reverse().ToArray());
}
