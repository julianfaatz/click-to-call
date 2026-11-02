using System.Net;
using System.Net.Sockets;

namespace ClickToCall.Core.Network;

public static class PortProbe
{
    /// <summary>
    /// Whether something accepts TCP connections on the port. Uses the event based socket API:
    /// cancelling it completes with an error code instead of throwing.
    /// </summary>
    public static async Task<bool> IsOpenAsync(IPAddress address, int port, TimeSpan timeout, CancellationToken ct)
    {
        using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        using var args = new SocketAsyncEventArgs { RemoteEndPoint = new IPEndPoint(address, port) };
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        args.Completed += (_, e) => done.TrySetResult(e.SocketError == SocketError.Success);

        if (!socket.ConnectAsync(args))
            return args.SocketError == SocketError.Success; // completed synchronously

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, ct);
        await using (linked.Token.Register(() => Socket.CancelConnectAsync(args)))
            return await done.Task;
    }
}
