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
    public void Does_not_scan_common_networks_twice()
    {
        // 192.168.0.0/22 already covers 192.168.0.x – 192.168.3.x
        var networks = PhoneScanner.NetworksToScan([(IPAddress.Parse("192.168.0.0"), 22), (IPAddress.Parse("192.168.178.0"), 24)]);

        Assert.Equal([(IPAddress.Parse("192.168.0.0"), 22), (IPAddress.Parse("192.168.178.0"), 24)], networks);
    }
}
