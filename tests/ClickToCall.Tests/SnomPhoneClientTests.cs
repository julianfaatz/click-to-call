using ClickToCall.Core;

namespace ClickToCall.Tests;

public class SnomPhoneClientTests
{
    [Fact]
    public void Parses_identities_from_settings_xml()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <settings>
              <phone-settings>
                <user_active idx="1" perm="">on</user_active>
                <user_name idx="1" perm="">201</user_name>
                <user_host idx="1" perm="">pbx.example.com</user_host>
                <user_realname idx="1" perm="">Empfang</user_realname>
                <user_active idx="2" perm="">on</user_active>
                <user_name idx="2" perm="">202</user_name>
                <user_host idx="2" perm="">pbx.example.com</user_host>
                <user_realname idx="2" perm=""></user_realname>
                <user_active idx="3" perm="">off</user_active>
                <user_name idx="3" perm="">203</user_name>
                <user_host idx="3" perm="">pbx.example.com</user_host>
                <user_name idx="4" perm=""></user_name>
                <user_host idx="4" perm=""></user_host>
              </phone-settings>
            </settings>
            """;

        var identities = SnomPhoneClient.ParseIdentities(xml);

        Assert.Equal(2, identities.Count);
        Assert.Equal(new PhoneIdentity(1, "Empfang", "201@pbx.example.com"), identities[0]);
        Assert.Equal("202@pbx.example.com", identities[1].Uri);
    }

    [Fact]
    public void Requires_host() =>
        Assert.Throws<PhoneException>(() => new SnomPhoneClient(new PhoneConnection("", false, true, "", "")));

    [Theory]
    [InlineData("192.168.1 .50")]
    [InlineData("phone<1>")]
    public void Rejects_invalid_host(string host)
    {
        var ex = Assert.Throws<PhoneException>(() => new SnomPhoneClient(new PhoneConnection(host, false, true, "", "")));
        Assert.Equal(PhoneErrorKind.InvalidAddress, ex.Kind);
    }

    [Fact]
    public void Ignores_dtd_in_settings_xml()
    {
        const string xml = """
            <?xml version="1.0"?>
            <!DOCTYPE settings [<!ENTITY a "aaaaaaaaaa"><!ENTITY b "&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;">]>
            <settings><phone-settings><user_name idx="1">201</user_name><user_host idx="1">pbx</user_host></phone-settings></settings>
            """;

        Assert.Equal("201@pbx", Assert.Single(SnomPhoneClient.ParseIdentities(xml)).Uri);
    }

    [Fact]
    public async Task Dials_via_command_htm()
    {
        using var server = new FakeHttpServer(_ => new FakeResponse(200));
        using var client = new SnomPhoneClient(new PhoneConnection(server.Host, false, true, "", ""));

        await client.DialAsync("0301234567", "201@pbx");

        Assert.Equal("/command.htm?number=0301234567&outgoing_uri=201%40pbx", Assert.Single(server.Requests).Target);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reports_blocked_remote_control_when_phone_drops_command(bool withLogin)
    {
        // Hidden tags on: the pages work, command.htm is closed without a response
        using var server = new FakeHttpServer(request =>
            withLogin && !request.Headers.ContainsKey("Authorization")
                ? new FakeResponse(401, ExtraHeaders: "WWW-Authenticate: Basic realm=\"snom\"\r\n")
                : request.Target.StartsWith("/command.htm") ? new FakeResponse(0) : new FakeResponse(200, "<title>snom D385</title>"));
        using var client = new SnomPhoneClient(new PhoneConnection(server.Host, false, true, withLogin ? "admin" : "", withLogin ? "pw" : ""));

        Assert.Equal("snom D385", await client.TestConnectionAsync());
        var dial = await Assert.ThrowsAsync<PhoneException>(() => client.DialAsync("0301234567"));
        var hangUp = await Assert.ThrowsAsync<PhoneException>(() => client.HangUpAsync());

        Assert.Equal(PhoneErrorKind.RemoteControlBlocked, dial.Kind);
        Assert.Equal(PhoneErrorKind.RemoteControlBlocked, hangUp.Kind);
    }

    [Theory]
    [InlineData("Basic realm=\"snom\"")]
    [InlineData("Digest realm=\"snom\", nonce=\"abc\", qop=\"auth\"")]
    public async Task Keeps_login_when_phone_redirects_after_command(string challenge)
    {
        // The phone runs the command and redirects to its start page, which needs the login as well
        using var server = new FakeHttpServer(request =>
            !request.Headers.ContainsKey("Authorization")
                ? new FakeResponse(401, ExtraHeaders: $"WWW-Authenticate: {challenge}\r\n")
                : request.Target.StartsWith("/command.htm") ? new FakeResponse(302, ExtraHeaders: "Location: /\r\n") : new FakeResponse(200));
        using var client = new SnomPhoneClient(new PhoneConnection(server.Host, false, true, "admin", "pw"));

        await client.DialAsync("0301234567");

        Assert.Equal("/", server.Requests[^1].Target);
    }

    [Fact]
    public async Task Unreachable_phone_is_not_reported_as_blocked()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var host = listener.LocalEndpoint.ToString()!;
        listener.Stop(); // nothing listens on the port anymore

        using var client = new SnomPhoneClient(new PhoneConnection(host, false, true, "", ""));
        var ex = await Assert.ThrowsAsync<PhoneException>(() => client.DialAsync("0301234567"));

        Assert.Equal(PhoneErrorKind.Unreachable, ex.Kind);
    }

    [Theory]
    [InlineData("192.168.1.50", false, "http://192.168.1.50/")]
    [InlineData("http://192.168.1.50/", false, "http://192.168.1.50/")]
    [InlineData("192.168.1.50:8080", true, "https://192.168.1.50:8080/")]
    public void Builds_base_address(string host, bool https, string expected)
    {
        using var client = new SnomPhoneClient(new PhoneConnection(host, https, true, "admin", "pw"));
        Assert.Equal(expected, client.BaseAddress.ToString());
    }
}
