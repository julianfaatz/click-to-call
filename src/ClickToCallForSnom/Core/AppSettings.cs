using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace ClickToCallForSnom.Core;

/// <summary>
/// All user settings. Persisted as JSON by <see cref="SettingsStore"/>.
/// </summary>
public sealed class AppSettings : ObservableObject
{
    // ---------- Phone ----------

    /// <summary>Manufacturer of the connected phone. Missing in settings of older versions = Snom.</summary>
    private PhoneVendor _vendor = PhoneVendor.Snom;
    public PhoneVendor Vendor { get => _vendor; set => Set(ref _vendor, value); }

    private string _phoneHost = "";
    public string PhoneHost { get => _phoneHost; set => Set(ref _phoneHost, (value ?? "").Trim()); }

    private bool _useHttps;
    public bool UseHttps { get => _useHttps; set => Set(ref _useHttps, value); }

    /// <summary>Desk phones ship with self-signed certificates, so this is on by default.</summary>
    private bool _ignoreCertificateErrors = true;
    public bool IgnoreCertificateErrors { get => _ignoreCertificateErrors; set => Set(ref _ignoreCertificateErrors, value); }

    private string _username = "";
    public string Username { get => _username; set => Set(ref _username, (value ?? "").Trim()); }

    /// <summary>Password encrypted with Windows DPAPI (current user).</summary>
    private string _protectedPassword = "";
    public string ProtectedPassword { get => _protectedPassword; set => Set(ref _protectedPassword, value ?? ""); }

    [JsonIgnore]
    public string Password
    {
        get => SecretProtector.Unprotect(ProtectedPassword);
        set => ProtectedPassword = SecretProtector.Protect(value);
    }

    /// <summary>Model name reported by the phone at the last successful connection (for display only).</summary>
    private string _phoneModel = "";
    public string PhoneModel { get => _phoneModel; set => Set(ref _phoneModel, value ?? ""); }

    // ---------- Identities ----------

    public ObservableCollection<PhoneIdentity> Identities { get; set; } = [];

    /// <summary>Outgoing URI of the default identity. Empty = use the phone's default.</summary>
    private string _defaultIdentityUri = "";
    public string DefaultIdentityUri { get => _defaultIdentityUri; set => Set(ref _defaultIdentityUri, value ?? ""); }

    private bool _showIdentitySelection;
    public bool ShowIdentitySelection { get => _showIdentitySelection; set => Set(ref _showIdentitySelection, value); }

    // ---------- Number normalization ----------

    /// <summary>Replace the own country code (+49 / 0049) with a national 0.</summary>
    private bool _convertOwnCountryCode = true;
    public bool ConvertOwnCountryCode { get => _convertOwnCountryCode; set => Set(ref _convertOwnCountryCode, value); }

    private string _ownCountryCode = "49";
    public string OwnCountryCode { get => _ownCountryCode; set => Set(ref _ownCountryCode, (value ?? "").Trim().TrimStart('+')); }

    /// <summary>Replace a leading + of foreign numbers with 00.</summary>
    private bool _convertPlusToZeros = true;
    public bool ConvertPlusToZeros { get => _convertPlusToZeros; set => Set(ref _convertPlusToZeros, value); }

    /// <summary>Prefix to get an outside line (e.g. "0"). Empty = none.</summary>
    private string _outsideLinePrefix = "";
    public string OutsideLinePrefix { get => _outsideLinePrefix; set => Set(ref _outsideLinePrefix, (value ?? "").Trim()); }

    /// <summary>Only numbers with at least this many digits get the outside line prefix (internal extensions are shorter).</summary>
    private int _outsideLineMinLength = 5;
    public int OutsideLineMinLength { get => _outsideLineMinLength; set => Set(ref _outsideLineMinLength, Math.Clamp(value, 1, 30)); }

    // ---------- Behavior ----------

    private bool _confirmLinkCalls = true;
    public bool ConfirmLinkCalls { get => _confirmLinkCalls; set => Set(ref _confirmLinkCalls, value); }

    /// <summary>Show the dial window for a selected number (hotkey). Off = dial a clearly selected number right away.</summary>
    private bool _confirmHotkeyCalls = true;
    public bool ConfirmHotkeyCalls { get => _confirmHotkeyCalls; set => Set(ref _confirmHotkeyCalls, value); }

    private bool _showCallNotification = true;
    public bool ShowCallNotification { get => _showCallNotification; set => Set(ref _showCallNotification, value); }

    private int _notificationSeconds = 15;
    public int NotificationSeconds { get => _notificationSeconds; set => Set(ref _notificationSeconds, Math.Clamp(value, 3, 300)); }

    // ---------- Hotkey ----------

    private bool _hotkeyEnabled = true;
    public bool HotkeyEnabled { get => _hotkeyEnabled; set => Set(ref _hotkeyEnabled, value); }

    private Hotkey _hotkey = Hotkey.Default;
    public Hotkey Hotkey { get => _hotkey; set => Set(ref _hotkey, value); }

    /// <summary>When nothing is selected, prefill the dial window from the clipboard (if it looks like a number).</summary>
    private bool _hotkeyUseClipboardFallback;
    public bool HotkeyUseClipboardFallback { get => _hotkeyUseClipboardFallback; set => Set(ref _hotkeyUseClipboardFallback, value); }

    // ---------- Protocols ----------

    private bool _handleTel = true;
    public bool HandleTel { get => _handleTel; set => Set(ref _handleTel, value); }

    private bool _handleCallto = true;
    public bool HandleCallto { get => _handleCallto; set => Set(ref _handleCallto, value); }

    private bool _handleSip = true;
    public bool HandleSip { get => _handleSip; set => Set(ref _handleSip, value); }

    // ---------- General ----------

    /// <summary>"de", "en" or empty for the Windows display language.</summary>
    private string _language = "";
    public string Language { get => _language; set => Set(ref _language, value ?? ""); }

    private bool _checkForUpdates = true;
    public bool CheckForUpdates { get => _checkForUpdates; set => Set(ref _checkForUpdates, value); }

    public string SkippedUpdateVersion { get; set; } = "";

    /// <summary>The "keeps running in the notification area" hint is shown only once.</summary>
    public bool BackgroundHintShown { get; set; }

    // ---------- Setup ----------

    /// <summary>The guided setup on the start page was finished (test call made or skipped).</summary>
    private bool _setupCompleted;
    public bool SetupCompleted { get => _setupCompleted; set => Set(ref _setupCompleted, value); }

    /// <summary>The user skipped the "make phone numbers clickable" setup step.</summary>
    private bool _setupLinksSkipped;
    public bool SetupLinksSkipped { get => _setupLinksSkipped; set => Set(ref _setupLinksSkipped, value); }

    // ---------- Window ----------

    /// <summary>Last size of the app window (0 = default size).</summary>
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }

    /// <summary>Lives in the registry (Run key), not in the JSON file. Filled when a settings window opens.</summary>
    private bool _autostart;
    [JsonIgnore]
    public bool Autostart { get => _autostart; set => Set(ref _autostart, value); }

    /// <summary>Most recent dialed numbers (newest first). Basis for redial / call history.</summary>
    public List<string> RecentNumbers { get; set; } = [];

    // ---------- Derived ----------

    [JsonIgnore]
    public bool IsPhoneConfigured => !string.IsNullOrWhiteSpace(PhoneHost);

    public void AddRecentNumber(string number, int max = 20)
    {
        RecentNumbers.Remove(number);
        RecentNumbers.Insert(0, number);
        if (RecentNumbers.Count > max)
            RecentNumbers.RemoveRange(max, RecentNumbers.Count - max);
    }

    /// <summary>Country calling code for the Windows region (used for new installations).</summary>
    public static string CountryCodeForRegion()
    {
        try
        {
            return System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName switch
            {
                "AT" => "43",
                "CH" => "41",
                "LI" => "423",
                "LU" => "352",
                "NL" => "31",
                "BE" => "32",
                "FR" => "33",
                "IT" => "39",
                "ES" => "34",
                "PT" => "351",
                "GB" => "44",
                "IE" => "353",
                "DK" => "45",
                "SE" => "46",
                "NO" => "47",
                "FI" => "358",
                "PL" => "48",
                "CZ" => "420",
                "SK" => "421",
                "HU" => "36",
                "US" or "CA" => "1",
                _ => "49",
            };
        }
        catch (ArgumentException)
        {
            return "49";
        }
    }

    public AppSettings Clone() => SettingsStore.Deserialize(SettingsStore.Serialize(this));
}

public sealed record PhoneIdentity(int Index, string Name, string Uri)
{
    public string DisplayName =>
        string.IsNullOrWhiteSpace(Uri) ? Name
        : string.IsNullOrWhiteSpace(Name) ? Uri
        : $"{Name} ({Uri})";
    public override string ToString() => DisplayName;
}
