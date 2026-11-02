using System.Net;
using ClickToCall.Core.Network;

namespace ClickToCall.Tests;

public class SubnetTests
{
    [Theory]
    [InlineData("192.168.1.77", 24, "192.168.1.0")]
    [InlineData("10.0.3.200", 22, "10.0.0.0")]
    [InlineData("172.16.5.9", 23, "172.16.4.0")]
    public void Computes_network_address(string address, int prefix, string expected) =>
        Assert.Equal(IPAddress.Parse(expected), Subnet.Of(IPAddress.Parse(address), prefix).Network);

    [Fact]
    public void Lists_hosts_of_a_24_subnet()
    {
        var addresses = new Subnet(IPAddress.Parse("192.168.1.0"), 24).Hosts().ToList();

        Assert.Equal(254, addresses.Count);
        Assert.Equal(IPAddress.Parse("192.168.1.1"), addresses[0]);
        Assert.Equal(IPAddress.Parse("192.168.1.254"), addresses[^1]);
    }

    [Fact]
    public void Lists_hosts_across_the_24_boundaries_of_a_larger_subnet()
    {
        // PC in 10.0.0.x, phone in 10.0.1.x – the old /24 scan never found it
        var addresses = new Subnet(IPAddress.Parse("10.0.0.0"), 22).Hosts().ToList();

        Assert.Equal(1022, addresses.Count);
        Assert.Contains(IPAddress.Parse("10.0.1.50"), addresses);
        Assert.Contains(IPAddress.Parse("10.0.0.255"), addresses);
        Assert.Equal(IPAddress.Parse("10.0.3.254"), addresses[^1]);
    }

    [Theory]
    [InlineData("192.168.178.0", 24, "192.168.178.x")]
    [InlineData("10.0.0.0", 22, "10.0.0.0/22")]
    public void Labels_networks(string network, int prefix, string expected) =>
        Assert.Equal(expected, new Subnet(IPAddress.Parse(network), prefix).Label);

    [Fact]
    public void Knows_its_addresses()
    {
        var subnet = new Subnet(IPAddress.Parse("192.168.0.0"), 22);

        Assert.True(subnet.Contains(IPAddress.Parse("192.168.3.254")));
        Assert.False(subnet.Contains(IPAddress.Parse("192.168.4.1")));
    }
}
