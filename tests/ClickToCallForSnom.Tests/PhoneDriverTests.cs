using ClickToCallForSnom.Core;

namespace ClickToCallForSnom.Tests;

public class PhoneDriverTests
{
    [Theory]
    [InlineData("snom embedded <title>snom D385</title>", "Snom D385")]
    [InlineData("Digest realm=\"snom\" <html>snom-D717 Web</html>", "Snom D717")]
    [InlineData("Server: snom embedded", "Snom")]
    public void Snom_driver_detects_snom_phones(string probeText, string expectedModel) =>
        Assert.Equal(expectedModel, PhoneDrivers.For(PhoneVendor.Snom).Detect(probeText));

    [Theory]
    [InlineData("nginx <title>Router</title>")]
    [InlineData("")]
    public void Snom_driver_ignores_other_devices(string probeText) =>
        Assert.Null(PhoneDrivers.For(PhoneVendor.Snom).Detect(probeText));

    [Fact]
    public void Creates_client_for_the_connection_vendor()
    {
        using var client = PhoneDrivers.CreateClient(new PhoneConnection("192.168.1.50", false, true, "", "", PhoneVendor.Snom));
        Assert.IsType<SnomPhoneClient>(client);
    }

    [Fact]
    public void Settings_without_vendor_mean_snom() =>
        Assert.Equal(PhoneVendor.Snom, SettingsStore.Deserialize("""{ "PhoneHost": "192.168.1.50" }""").Vendor);

    [Fact]
    public void Vendor_is_stored_by_name()
    {
        var json = SettingsStore.Serialize(new AppSettings { Vendor = PhoneVendor.Snom });
        Assert.Contains("\"Vendor\": \"Snom\"", json);
    }
}
