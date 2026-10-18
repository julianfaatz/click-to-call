using System.Net;
using System.Net.Sockets;

namespace ClickToCall.Core;

public static class NetworkInfo
{
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
