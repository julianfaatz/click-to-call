#if DEBUG
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ClickToCallForSnom.Core;

public sealed record MockPhoneEvent(DateTime Time, string Text, string? Display = null);

/// <summary>
/// Development only: a fake Snom phone on 127.0.0.1 that speaks the same HTTP interface as a real one,
/// so connecting, login, dialing, hang up and identities run through the real code.
/// <para>
/// Start the app with <c>--mock</c> (no login) or <c>--mock-login</c> (user "admin", password "1234").
/// <c>--mock-reset</c> starts the setup from scratch. Mock settings live in %APPDATA%\ClickToCallForSnom\mock,
/// the real configuration is not touched.
/// </para>
/// </summary>
public static class MockPhone
{
    public const string Model = "snom D385";
    public const string Username = "admin";
    public const string Password = "1234";
    private const int PreferredPort = 18080;

    public static bool IsEnabled { get; private set; }
    public static bool RequiresLogin { get; private set; }
    public static string? Address { get; private set; }

    /// <summary>While true the phone does not answer (simulates a switched off phone).</summary>
    public static bool Offline { get; set; }

    /// <summary>Raised on a background thread for every request the phone handled.</summary>
    public static event Action<MockPhoneEvent>? Activity;

    public static bool IsRequested(string[] args) => args.Any(a => a.StartsWith("--mock", StringComparison.Ordinal));

    /// <summary>Switches to separate settings. Call before <see cref="SettingsStore.Load"/>.</summary>
    public static void Prepare(string[] args)
    {
        IsEnabled = true;
        RequiresLogin = args.Contains("--mock-login");

        var directory = Path.Combine(SettingsStore.Directory, "mock");
        if (args.Contains("--mock-reset") && Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
        SettingsStore.UseDirectory(directory);
    }

    public static void Start()
    {
        TcpListener listener;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, PreferredPort);
            listener.Start();
        }
        catch (SocketException)
        {
            listener = new TcpListener(IPAddress.Loopback, 0); // port in use: any free port
            listener.Start();
        }

        Address = $"127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
        Log.Info($"Mock phone listening on {Address} (login required: {RequiresLogin})");
        _ = Task.Run(() => AcceptLoopAsync(listener));
    }

    private static async Task AcceptLoopAsync(TcpListener listener)
    {
        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            _ = Task.Run(() => HandleAsync(client));
        }
    }

    private static async Task HandleAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                if (Offline)
                    return; // just drop the connection like an unreachable device

                var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync();
                if (requestLine?.Split(' ') is not [_, var target, ..])
                    return;

                string? authorization = null;
                while (await reader.ReadLineAsync() is { Length: > 0 } header)
                {
                    if (header.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                        authorization = header["Authorization:".Length..].Trim();
                }

                await Task.Delay(200); // feel like a real device
                var uri = new Uri("http://mock" + target);

                var expected = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Username}:{Password}"));
                if (RequiresLogin && authorization != expected)
                {
                    if (authorization is not null)
                        Raise($"Login abgelehnt ({uri.AbsolutePath})");
                    await WriteAsync(stream, "401 Unauthorized", "text/html", "<html><body>snom: login required</body></html>",
                        "WWW-Authenticate: Basic realm=\"snom\"\r\n");
                    return;
                }

                var (status, contentType, body) = Respond(uri);
                await WriteAsync(stream, status, contentType, body);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                // client went away
            }
        }
    }

    private static (string Status, string ContentType, string Body) Respond(Uri uri)
    {
        var query = ParseQuery(uri.Query);
        switch (uri.AbsolutePath.ToLowerInvariant())
        {
            case "/" or "/info.htm":
                Raise("Verbindungstest");
                return ("200 OK", "text/html", $"<html><head><title>{Model} (Mock)</title></head><body>snom mock phone</body></html>");

            case "/command.htm" when query.TryGetValue("number", out var number):
                var line = query.TryGetValue("outgoing_uri", out var uriValue) ? uriValue : "Standard-Leitung";
                Raise($"Wählt {number} über {line}", $"☎  {number}");
                return ("200 OK", "text/html", "<html><body>OK</body></html>");

            case "/command.htm" when query.TryGetValue("key", out var key) && key == "CANCEL":
                Raise("Aufgelegt", "Bereit");
                return ("200 OK", "text/html", "<html><body>OK</body></html>");

            case "/settings.xml":
                Raise("Leitungen gelesen");
                return ("200 OK", "text/xml", SettingsXml);

            default:
                Raise($"Unbekannte Anfrage: {uri.PathAndQuery} (404)");
                return ("404 Not Found", "text/html", "<html><body>404</body></html>");
        }
    }

    private const string SettingsXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <settings>
          <phone-settings>
            <user_active idx="1" perm="">on</user_active>
            <user_name idx="1" perm="">100</user_name>
            <user_host idx="1" perm="">pbx.mock.local</user_host>
            <user_realname idx="1" perm="">Zentrale</user_realname>
            <user_active idx="2" perm="">on</user_active>
            <user_name idx="2" perm="">200</user_name>
            <user_host idx="2" perm="">pbx.mock.local</user_host>
            <user_realname idx="2" perm="">Vertrieb</user_realname>
          </phone-settings>
        </settings>
        """;

    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => p.Length > 1 ? Uri.UnescapeDataString(p[1]) : "");

    private static async Task WriteAsync(Stream stream, string status, string contentType, string body, string extraHeaders = "")
    {
        var content = Encoding.UTF8.GetBytes(body);
        var head = $"HTTP/1.1 {status}\r\nServer: snom embedded (mock)\r\nContent-Type: {contentType}; charset=utf-8\r\n" +
                   $"Content-Length: {content.Length}\r\n{extraHeaders}Connection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
        await stream.WriteAsync(content);
    }

    private static void Raise(string text, string? display = null)
    {
        Log.Info("Mock phone: " + text);
        Activity?.Invoke(new MockPhoneEvent(DateTime.Now, text, display));
    }
}
#endif
