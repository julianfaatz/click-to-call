using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ClickToCall.Core.Network;

public static class NetworkInfo
{
    /// <summary>The IPv4 addresses of this PC in real networks, with the prefix length of their network.</summary>
    public static IEnumerable<(IPAddress Address, int PrefixLength)> LocalAddresses()
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
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                    yield return (unicast.Address, unicast.PrefixLength);
            }
        }
    }

    /// <summary>
    /// The IP address of this PC as the phone sees it (the local address Windows would use to reach it).
    /// Nothing is sent: "connecting" a UDP socket only picks the route.
    /// </summary>
    public static string? LocalAddressFor(string phoneHost)
    {
        try
        {
            var host = phoneHost.Split(':')[0];
            var address = IPAddress.TryParse(host, out var ip)
                ? ip
                : Dns.GetHostAddresses(host).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (address is null)
                return null;

            using var socket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(address, 9);
            return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString();
        }
        catch (Exception ex) when (ex is SocketException or ArgumentException)
        {
            return null;
        }
    }
}
