using System.Net;
using ClickToCall.Core;

namespace ClickToCall.Tests;

public class PhoneScannerTests
{
    [Theory]
    [InlineData("192.168.1.77", 24, "192.168.1.0")]
    [InlineData("10.0.3.200", 22, "10.0.0.0")]
    [InlineData("172.16.5.9", 23, "172.16.4.0")]
    public void Computes_network_address(string address, int prefix, string expected) =>
        Assert.Equal(IPAddress.Parse(expected), PhoneScanner.NetworkAddress(IPAddress.Parse(address), prefix));

    [Fact]
    public void Lists_hosts_of_a_24_subnet()
    {
        var addresses = PhoneScanner.SubnetAddresses(IPAddress.Parse("192.168.1.0"), 24).ToList();

        Assert.Equal(254, addresses.Count);
        Assert.Equal(IPAddress.Parse("192.168.1.1"), addresses[0]);
        Assert.Equal(IPAddress.Parse("192.168.1.254"), addresses[^1]);
    }

    [Fact]
    public void Lists_hosts_across_the_24_boundaries_of_a_larger_subnet()
    {
        // PC in 10.0.0.x, phone in 10.0.1.x – the old /24 scan never found it
        var addresses = PhoneScanner.SubnetAddresses(IPAddress.Parse("10.0.0.0"), 22).ToList();

        Assert.Equal(1022, addresses.Count);
        Assert.Contains(IPAddress.Parse("10.0.1.50"), addresses);
        Assert.Contains(IPAddress.Parse("10.0.0.255"), addresses);
        Assert.Equal(IPAddress.Parse("10.0.3.254"), addresses[^1]);
    }

    [Fact]
    public void Also_scans_common_router_networks_after_the_own_one()
    {
        // PC in 192.168.10.x, phone behind a FRITZ!Box in 192.168.178.x
        var networks = PhoneScanner.NetworksToScan([(IPAddress.Parse("192.168.10.0"), 24)]);

        Assert.Equal((IPAddress.Parse("192.168.10.0"), 24), networks[0]);
        Assert.Contains((IPAddress.Parse("192.168.178.0"), 24), networks);
        Assert.Contains((IPAddress.Parse("192.168.2.0"), 24), networks);
    }

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
    public async Task Reports_devices_it_does_not_recognize()
    {
        using var server = new FakeHttpServer(_ => new FakeResponse(200, "<title>FRITZ!Box</title>"));

        var (status, phone) = await PhoneScanner.IdentifyAsync(server.Host);

        Assert.Equal(ProbeStatus.UnknownDevice, status);
        Assert.Null(phone);
    }

    [Fact]
    public void Summarizes_a_probe_for_the_log()
    {
        var response = new ProbeResponse("http://192.168.1.5/", 401, "embedded", "Digest realm=\"Phone\"", Body: "<title>\n  Login\n</title>");

        Assert.Equal("http://192.168.1.5/: 401, server \"embedded\", login \"Digest realm=\"Phone\"\", title \"Login\", 24 chars", response.ToString());
        Assert.Equal("http://192.168.1.5/: timeout", new ProbeResponse("http://192.168.1.5/", Error: "timeout").ToString());
    }

    [Fact]
    public void Does_not_scan_common_networks_twice()
    {
        // 192.168.0.0/22 already covers 192.168.0.x – 192.168.3.x
        var networks = PhoneScanner.NetworksToScan([(IPAddress.Parse("192.168.0.0"), 22), (IPAddress.Parse("192.168.178.0"), 24)]);

        Assert.Equal([(IPAddress.Parse("192.168.0.0"), 22), (IPAddress.Parse("192.168.178.0"), 24)], networks);
    }
}
