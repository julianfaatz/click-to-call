using System.Text.RegularExpressions;
using ClickToCall.Core.Network;

namespace ClickToCall.Core.Phones;

public sealed partial class YealinkDriver : IPhoneDriver
{
    public PhoneVendor Vendor => PhoneVendor.Yealink;
    public string Name => "Yealink";
    public bool CanReadIdentities => false;

    public string? Detect(string probeText)
    {
        // The web interface of the T4xS (firmware 66.x) names the model in a script variable – with or without "Yealink" on the page
        if (PhoneTypeRegex().Match(probeText) is { Success: true } phoneType)
            return "Yealink " + phoneType.Groups[1].Value.ToUpperInvariant();

        if (!probeText.Contains("yealink", StringComparison.OrdinalIgnoreCase))
            return null;

        var match = ModelRegex().Match(probeText);
        return match.Success ? "Yealink " + match.Groups[1].Value.ToUpperInvariant() : Name;
    }

    /// <summary>Yealink phones only accept remote control from allowed computers – the one thing the user has to do on the phone.</summary>
    public string? UsageHint(string phoneHost) =>
        Loc.T("Yealink.RemoteControlHint", NetworkInfo.LocalAddressFor(HttpPhoneClient.NormalizeHost(phoneHost)) is { } ip ? $" ({ip})" : "");

    public IPhoneClient CreateClient(PhoneConnection connection, TimeSpan? timeout = null) => new YealinkPhoneClient(connection, timeout);

    // "Yealink SIP-T46U", "Yealink T54W", "Yealink W70B"
    [GeneratedRegex(@"yealink\W{0,3}(?:sip-?)?([A-Z]{1,2}\d{2,3}[A-Z]{0,2})\b", RegexOptions.IgnoreCase)]
    private static partial Regex ModelRegex();

    // g_phonetype = "T48S"
    [GeneratedRegex(@"\bg_phonetype\s*=\s*[""']([A-Z]{1,2}\d{2,3}[A-Z]{0,2})[""']", RegexOptions.IgnoreCase)]
    private static partial Regex PhoneTypeRegex();
}
