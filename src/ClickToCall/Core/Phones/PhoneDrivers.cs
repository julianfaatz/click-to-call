using ClickToCall.Core.Network;

namespace ClickToCall.Core.Phones;

public static class PhoneDrivers
{
    public static IReadOnlyList<IPhoneDriver> All { get; } = [new SnomDriver(), new YealinkDriver()];

    public static IPhoneDriver For(PhoneVendor vendor) => All.First(d => d.Vendor == vendor);

    public static IPhoneClient CreateClient(PhoneConnection connection, TimeSpan? timeout = null) =>
        For(connection.Vendor).CreateClient(connection, timeout);

    /// <summary>The phone if a driver recognizes the device by its start page, otherwise null.</summary>
    public static FoundPhone? Recognize(string address, ProbeResponse response)
    {
        if (!response.Answered)
            return null;

        foreach (var driver in All)
        {
            if (driver.Detect(response.Text) is { } model)
                return new FoundPhone(address, model, driver.Vendor);
        }

        return null;
    }
}
