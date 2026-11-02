using ClickToCall.Core;
using ClickToCall.Core.Phones;

namespace ClickToCall.Services;

/// <summary>Stores the connected phone in the settings and keeps what the app knows about it up to date.</summary>
public static class PhoneSetup
{
    private static AppSettings Settings => SettingsStore.Current;

    /// <summary>The manufacturer of an address that is known to work, or null to recognize it from the phone.</summary>
    public static PhoneVendor? KnownVendor(string host) =>
        Settings.IsPhoneConfigured && host == Settings.PhoneHost ? Settings.Vendor : null; // e.g. reconnecting with a new password

    /// <summary>Saves a successful connection. The lines of another phone no longer apply.</summary>
    public static void Save(ConnectResult result, string host, string username, string password)
    {
        if (host != Settings.PhoneHost || result.Vendor != Settings.Vendor)
        {
            Settings.Identities.Clear();
            Settings.DefaultIdentityUri = "";
        }

        Settings.Vendor = result.Vendor!.Value;
        Settings.PhoneHost = host;
        Settings.UseHttps = result.UseHttps;
        Settings.IgnoreCertificateErrors = true;
        Settings.Username = username;
        Settings.Password = password;
        Settings.PhoneModel = result.Model;
    }

    /// <summary>Checks that the saved phone answers and updates its model name. Throws <see cref="PhoneException"/> if not.</summary>
    public static async Task CheckAsync(CancellationToken ct)
    {
        using var client = PhoneDrivers.CreateClient(PhoneConnection.From(Settings), TimeSpan.FromSeconds(5));
        var model = await client.TestConnectionAsync(ct);
        // A newer check (e.g. for another phone) took over
        ct.ThrowIfCancellationRequested();
        Settings.PhoneModel = model;
    }

    /// <summary>Reads the phone's identities (lines) silently – the user only needs them if there is more than one.</summary>
    public static async Task LoadIdentitiesAsync()
    {
        if (!PhoneDrivers.For(Settings.Vendor).CanReadIdentities)
            return;

        try
        {
            using var client = PhoneDrivers.CreateClient(PhoneConnection.From(Settings));
            var identities = await client.GetIdentitiesAsync();
            Settings.Identities.Clear();
            foreach (var identity in identities)
                Settings.Identities.Add(identity);
            if (identities.All(i => i.Uri != Settings.DefaultIdentityUri))
                Settings.DefaultIdentityUri = "";
        }
        catch (Exception ex)
        {
            Log.Error("Could not read identities", ex);
        }
    }
}
