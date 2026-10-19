using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ClickToCall.Tests;

public sealed record FakeRequest(string Method, string Target, IReadOnlyDictionary<string, string> Headers, string Body);

/// <param name="Status">0 = close the connection without sending a response.</param>
public sealed record FakeResponse(int Status, string Body = "", string ContentType = "text/html", string ExtraHeaders = "");

/// <summary>Minimal HTTP/1.1 server on 127.0.0.1 that records requests and answers with a callback.</summary>
public sealed class FakeHttpServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly Func<FakeRequest, FakeResponse> _respond;
    private readonly CancellationTokenSource _cts = new();

    public List<FakeRequest> Requests { get; } = [];

    public string Host => $"127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

    public FakeHttpServer(Func<FakeRequest, FakeResponse> respond)
    {
        _respond = respond;
        _listener.Start();
        _ = Task.Run(AcceptLoopAsync);
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);
            }
            catch (Exception)
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(client));
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        using (client)
        {
            var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync();
            if (requestLine?.Split(' ') is not [var method, var target, ..])
                return;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadLineAsync() is { Length: > 0 } line)
            {
                var colon = line.IndexOf(':');
                if (colon > 0)
                    headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
            }

            var length = headers.TryGetValue("Content-Length", out var l) ? int.Parse(l) : 0;
            var body = new char[length];
            var read = 0;
            while (read < length && await reader.ReadAsync(body.AsMemory(read)) is > 0 and var n)
                read += n;

            var request = new FakeRequest(method, target, headers, new string(body, 0, read));
            lock (Requests)
                Requests.Add(request);

            var response = _respond(request);
            if (response.Status == 0)
                return;

            var content = Encoding.UTF8.GetBytes(response.Body);
            var head = $"HTTP/1.1 {response.Status} X\r\nContent-Type: {response.ContentType}; charset=utf-8\r\n" +
                       $"Content-Length: {content.Length}\r\n{response.ExtraHeaders}Connection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
            await stream.WriteAsync(content);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
    }
}
