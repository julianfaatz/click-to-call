using System.Net;
using ClickToCall.Core.Network;
using ClickToCall.Core.Phones;

namespace ClickToCall.Tests;

public class PhoneScannerTests
{
    private static Subnet Net(string network, int prefix) => new(IPAddress.Parse(network), prefix);

    [Fact]
    public void Also_scans_common_router_networks_after_the_own_one()
    {
        // PC in 192.168.10.x, phone behind a FRITZ!Box in 192.168.178.x
        var networks = ScanNetworks.For([Net("192.168.10.0", 24)]);

        Assert.Equal(Net("192.168.10.0", 24), networks[0]);
        Assert.Contains(Net("192.168.178.0", 24), networks);
        Assert.Contains(Net("192.168.2.0", 24), networks);
    }

    [Fact]
    public void Does_not_scan_common_networks_twice()
    {
        // 192.168.0.0/22 already covers 192.168.0.x – 192.168.3.x
        var networks = ScanNetworks.For([Net("192.168.0.0", 22), Net("192.168.178.0", 24)]);

        Assert.Equal([Net("192.168.0.0", 22), Net("192.168.178.0", 24)], networks);
    }

    [Theory]
    [InlineData(16, "10.0.0.0/22")] // large company network: only around the own address
    [InlineData(28, "10.0.1.0/24")] // small network: still the whole /24
    public void Scans_the_own_network_around_the_own_address(int prefix, string expected) =>
        Assert.Equal(expected, ScanNetworks.Around(IPAddress.Parse("10.0.1.50"), prefix).ToString());

    [Fact]
    public async Task Identifies_a_phone_by_its_start_page()
    {
        // T48S web interface: model only in a script variable
        using var server = new FakeHttpServer(_ => new FakeResponse(200, "<html><script>var g_phonetype = \"T48S\";</script></html>"));

        var (status, phone) = await PhoneScanner.IdentifyAsync(server.Host);

        Assert.Equal(ProbeStatus.Found, status);
        Assert.Equal(new FoundPhone(server.Host, "Yealink T48S", PhoneVendor.Yealink), phone);
    }

    [Fact]
    public async Task Identifies_a_phone_by_its_redirect_even_if_the_target_does_not_answer()
    {
        // Yealink T48S (issue #4): HTTP redirects to the HTTPS login page. Here HTTPS fails (the fake server only speaks HTTP).
        string? host = null;
        using var server = new FakeHttpServer(_ => new FakeResponse(302, "<html><body><div id=\"_RES_INFO_\"></div></body></html>",
            ExtraHeaders: $"Location: https://{host}/servlet?m=mod_listener&p=login&q=loginForm&jumpto=status\r\n"));
        host = server.Host;

        var (status, phone) = await PhoneScanner.IdentifyAsync(server.Host);

        Assert.Equal(ProbeStatus.Found, status);
        Assert.Equal(PhoneVendor.Yealink, phone?.Vendor);
    }

    [Fact]
    public async Task Reports_devices_it_does_not_recognize()
    {
        using var server = new FakeHttpServer(_ => new FakeResponse(200, "<title>FRITZ!Box</title>"));

        var (status, phone) = await PhoneScanner.IdentifyAsync(server.Host);

        Assert.Equal(ProbeStatus.UnknownDevice, status);
        Assert.Null(phone);
    }
}
