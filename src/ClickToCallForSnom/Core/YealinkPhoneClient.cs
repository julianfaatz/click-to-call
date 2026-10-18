using System.Net;
using System.Text.RegularExpressions;

namespace ClickToCallForSnom.Core;

public sealed partial class YealinkDriver : IPhoneDriver
{
    public PhoneVendor Vendor => PhoneVendor.Yealink;
    public string Name => "Yealink";
    public bool CanReadIdentities => false;

    public string? Detect(string probeText)
    {
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
}

/// <summary>
/// Talks to Yealink desk phones via their "Action URI" interface (T3x, T4x, T5x).
/// Dialing:  GET /servlet?key=number=...&amp;outgoing_uri=user@host
/// Hang up:  GET /servlet?key=CALLEND
/// The phone executes these only for computers in its "Action URI Allow IP List" (or after the user
/// confirmed "Allow remote control?" on the phone) – otherwise it answers 200 and does nothing.
/// </summary>
public sealed partial class YealinkPhoneClient(PhoneConnection connection, TimeSpan? timeout = null)
    : HttpPhoneClient(connection, timeout, sendBasicAuthUpfront: true)
{
    private readonly bool _hasLogin = connection.Username.Length > 0;
    private readonly string _username = connection.Username;
    private readonly string _password = connection.Password;

    public override async Task DialAsync(string number, string? outgoingUri = null, CancellationToken ct = default)
    {
        var query = "servlet?key=number=" + Uri.EscapeDataString(number);
        if (!string.IsNullOrWhiteSpace(outgoingUri))
            query += "&outgoing_uri=" + Uri.EscapeDataString(outgoingUri);

        await GetStringAsync(query, ct);
    }

    public override Task HangUpAsync(CancellationToken ct = default) => GetStringAsync("servlet?key=CALLEND", ct);

    public override async Task<string> TestConnectionAsync(CancellationToken ct = default)
    {
        // Action URI always needs the web login (default admin/admin), even if the start page opens without one
        if (!_hasLogin)
            throw new PhoneException(PhoneErrorKind.Unauthorized);

        var html = await GetStringAsync("", ct);
        await CheckLoginAsync(ct);

        var title = TitleRegex().Match(html);
        var titleText = title.Success ? WebUtility.HtmlDecode(title.Groups[1].Value).Trim() : "";
        return new YealinkDriver().Detect(titleText + " " + html) ?? "Yealink";
    }

    /// <summary>
    /// Current firmware has a login API for the web interface: a wrong password is noticed right away
    /// instead of on the first call. Older firmware does not have it – then the first call tells.
    /// </summary>
    private async Task CheckLoginAsync(CancellationToken ct)
    {
        string response;
        try
        {
            response = await PostFormAsync("api/auth/login?p=Login", [new("username", _username), new("pwd", _password)], ct);
        }
        catch (PhoneException ex) when (ex.Kind is PhoneErrorKind.HttpError or PhoneErrorKind.Forbidden)
        {
            return; // no login API
        }

        if (LoginResultRegex().Match(response) is { Success: true } result && result.Groups[1].Value != "ok")
            throw new PhoneException(PhoneErrorKind.Unauthorized);
    }

    [GeneratedRegex("<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    // {"ret":"ok", ...} or {"ret":"failed", ...}
    [GeneratedRegex("\"ret\"\\s*:\\s*\"([^\"]*)\"")]
    private static partial Regex LoginResultRegex();
}
