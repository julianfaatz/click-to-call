using System.Text;
using ClickToCall.Core;

namespace ClickToCall.Tests;

public class YealinkPhoneClientTests
{
    private static readonly string AdminAuth = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:1234"));

    private static YealinkPhoneClient Client(FakeHttpServer server, string user = "admin", string password = "1234") =>
        new(new PhoneConnection(server.Host, false, true, user, password, PhoneVendor.Yealink));

    [Theory]
    [InlineData("<title>Yealink SIP-T46U</title>", "Yealink T46U")]
    [InlineData("Yealink T54W Web", "Yealink T54W")]
    [InlineData("Copyright Yealink Network Technology", "Yealink")]
    public void Detects_yealink_phones(string probeText, string expectedModel) =>
        Assert.Equal(expectedModel, PhoneDrivers.For(PhoneVendor.Yealink).Detect(probeText));

    [Fact]
    public void Snom_and_yealink_do_not_claim_each_other()
    {
        Assert.Null(PhoneDrivers.For(PhoneVendor.Yealink).Detect("snom embedded <title>snom D385</title>"));
        Assert.Null(PhoneDrivers.For(PhoneVendor.Snom).Detect("<title>Yealink SIP-T46U</title>"));
    }

    [Fact]
    public async Task Dials_via_action_uri_with_login_sent_upfront()
    {
        using var server = new FakeHttpServer(_ => new FakeResponse(200, "OK"));
        using var client = Client(server);

        await client.DialAsync("+4930123#", "201@pbx.example.com");

        var request = Assert.Single(server.Requests);
        Assert.Equal("/servlet?key=number=%2B4930123%23&outgoing_uri=201%40pbx.example.com", request.Target);
        Assert.Equal(AdminAuth, request.Headers["Authorization"]);
    }

    [Fact]
    public async Task Hangs_up_with_callend_key()
    {
        using var server = new FakeHttpServer(_ => new FakeResponse(200, "OK"));
        using var client = Client(server);

        await client.HangUpAsync();

        Assert.Equal("/servlet?key=CALLEND", Assert.Single(server.Requests).Target);
    }

    [Fact]
    public async Task Wrong_login_is_reported_as_unauthorized()
    {
        using var server = new FakeHttpServer(_ => new FakeResponse(401));
        using var client = Client(server);

        var ex = await Assert.ThrowsAsync<PhoneException>(() => client.DialAsync("123"));
        Assert.Equal(PhoneErrorKind.Unauthorized, ex.Kind);
    }

    [Fact]
    public async Task Follows_redirects_on_the_same_host_and_keeps_the_login()
    {
        using var server = new FakeHttpServer(r => r.Target == "/"
            ? new FakeResponse(302, ExtraHeaders: "Location: /servlet?m=mod_listener&p=login\r\n")
            : r.Target.StartsWith("/api/") ? new FakeResponse(404)
            : new FakeResponse(200, "<title>Yealink SIP-T46U</title>"));
        using var client = Client(server);

        Assert.Equal("Yealink T46U", await client.TestConnectionAsync());
        Assert.All(server.Requests, r => Assert.Equal(AdminAuth, r.Headers["Authorization"]));
    }

    [Fact]
    public async Task Redirect_to_https_lets_the_connector_try_https()
    {
        using var server = new FakeHttpServer(_ => new FakeResponse(302, ExtraHeaders: "Location: https://127.0.0.1/\r\n"));
        using var client = Client(server);

        var ex = await Assert.ThrowsAsync<PhoneException>(() => client.TestConnectionAsync());
        Assert.Equal(PhoneErrorKind.Unreachable, ex.Kind);
    }

    [Fact]
    public async Task Connection_test_asks_for_a_login_first()
    {
        using var server = new FakeHttpServer(_ => new FakeResponse(200, "<title>Yealink T54W</title>"));
        using var client = Client(server, user: "", password: "");

        var ex = await Assert.ThrowsAsync<PhoneException>(() => client.TestConnectionAsync());
        Assert.Equal(PhoneErrorKind.Unauthorized, ex.Kind);
        Assert.Empty(server.Requests);
    }

    [Theory]
    [InlineData("{\"ret\":\"ok\",\"data\":{}}", null)]
    [InlineData("{\"ret\":\"failed\"}", PhoneErrorKind.Unauthorized)]
    public async Task Connection_test_checks_the_login_with_the_web_api(string loginResponse, PhoneErrorKind? expectedError)
    {
        using var server = new FakeHttpServer(r => r.Target.StartsWith("/api/auth/login")
            ? new FakeResponse(200, loginResponse, "application/json")
            : new FakeResponse(200, "<title>Yealink T54W</title>"));
        using var client = Client(server);

        var ex = await Record.ExceptionAsync(() => client.TestConnectionAsync());

        Assert.Equal(expectedError, (ex as PhoneException)?.Kind);
        var login = Assert.Single(server.Requests, r => r.Method == "POST");
        Assert.Equal("username=admin&pwd=1234", login.Body);
    }

    [Fact]
    public async Task Older_firmware_without_login_api_is_accepted()
    {
        using var server = new FakeHttpServer(r => r.Target.StartsWith("/api/")
            ? new FakeResponse(404)
            : new FakeResponse(200, "<title>Yealink SIP-T21P E2</title>"));
        using var client = Client(server);

        Assert.Equal("Yealink T21P", await client.TestConnectionAsync());
    }

    [Fact]
    public async Task Connector_recognizes_the_vendor_of_a_typed_address()
    {
        using var server = new FakeHttpServer(r => r.Target.StartsWith("/api/")
            ? new FakeResponse(200, "{\"ret\":\"ok\"}", "application/json")
            : new FakeResponse(200, "<title>Yealink T54W</title>"));

        var result = await PhoneConnector.ConnectAsync(null, server.Host, "admin", "1234", preferHttps: false);

        Assert.Equal(ConnectStatus.Connected, result.Status);
        Assert.Equal(PhoneVendor.Yealink, result.Vendor);
    }

    [Fact]
    public async Task Connector_asks_for_the_vendor_of_unknown_devices()
    {
        using var server = new FakeHttpServer(_ => new FakeResponse(200, "<title>Some router</title>"));

        var result = await PhoneConnector.ConnectAsync(null, server.Host, "", "", preferHttps: false);

        Assert.Equal(ConnectStatus.UnknownVendor, result.Status);
    }
}
