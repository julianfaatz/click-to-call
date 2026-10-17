namespace ClickToCallForSnom.Core;

/// <summary>Everything that differs between phone manufacturers: recognizing their phones and talking to them.</summary>
public interface IPhoneDriver
{
    PhoneVendor Vendor { get; }

    /// <summary>Manufacturer name, also shown when the phone did not report a model.</summary>
    string Name { get; }

    /// <summary>Whether the phone can report its SIP identities (lines).</summary>
    bool CanReadIdentities { get; }

    /// <summary>
    /// Used by the network scan: the Server header, login realm and start page of a device.
    /// Returns the model name if it is a phone of this manufacturer, otherwise null.
    /// </summary>
    string? Detect(string probeText);

    IPhoneClient CreateClient(PhoneConnection connection, TimeSpan? timeout = null);
}

public static class PhoneDrivers
{
    public static IReadOnlyList<IPhoneDriver> All { get; } = [new SnomDriver()];

    public static IPhoneDriver For(PhoneVendor vendor) => All.First(d => d.Vendor == vendor);

    public static IPhoneClient CreateClient(PhoneConnection connection, TimeSpan? timeout = null) =>
        For(connection.Vendor).CreateClient(connection, timeout);
}
