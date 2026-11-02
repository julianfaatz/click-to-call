namespace ClickToCall.Core.Phones;

/// <summary>What the app needs from a phone. Create one with <see cref="PhoneDrivers.CreateClient"/>.</summary>
public interface IPhoneClient : IDisposable
{
    Task DialAsync(string number, string? outgoingUri = null, CancellationToken ct = default);

    Task HangUpAsync(CancellationToken ct = default);

    /// <summary>Checks reachability and credentials. Returns the phone's model name for display.</summary>
    Task<string> TestConnectionAsync(CancellationToken ct = default);

    /// <summary>The phone's SIP identities (lines). Only called if <see cref="IPhoneDriver.CanReadIdentities"/>.</summary>
    Task<IReadOnlyList<PhoneIdentity>> GetIdentitiesAsync(CancellationToken ct = default);
}
