using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace ClickToCall.Core;

public sealed partial class SnomDriver : IPhoneDriver
{
    public PhoneVendor Vendor => PhoneVendor.Snom;
    public string Name => "Snom";
    public bool CanReadIdentities => true;

    public string? Detect(string probeText)
    {
        if (!probeText.Contains("snom", StringComparison.OrdinalIgnoreCase))
            return null;

        var match = ModelRegex().Match(probeText);
        return match.Success ? "Snom " + match.Groups[1].Value.ToUpperInvariant() : Name;
    }

    public string? UsageHint(string phoneHost) => null;

    public IPhoneClient CreateClient(PhoneConnection connection, TimeSpan? timeout = null) => new SnomPhoneClient(connection, timeout);

    [GeneratedRegex(@"snom\s*-?\s*([A-Z]\d{3,4}[A-Z]*)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ModelRegex();
}

/// <summary>
/// Talks to the HTTP interface of Snom desk phones (D3xx, D7xx; D8xx does not support it).
/// Dialing:  GET /command.htm?number=...&amp;outgoing_uri=user@host
/// Hang up:  GET /command.htm?key=CANCEL
/// </summary>
public sealed partial class SnomPhoneClient(PhoneConnection connection, TimeSpan? timeout = null)
    : HttpPhoneClient(connection, timeout)
{
    public override async Task DialAsync(string number, string? outgoingUri = null, CancellationToken ct = default)
    {
        var query = "command.htm?number=" + Uri.EscapeDataString(number);
        if (!string.IsNullOrWhiteSpace(outgoingUri))
            query += "&outgoing_uri=" + Uri.EscapeDataString(outgoingUri);

        await SendCommandAsync(query, ct);
    }

    public override Task HangUpAsync(CancellationToken ct = default) => SendCommandAsync("command.htm?key=CANCEL", ct);

    /// <summary>
    /// With "Use hidden security tags" on and a changed web login, the phone serves its pages but
    /// closes the connection on command.htm without any response.
    /// </summary>
    private async Task SendCommandAsync(string query, CancellationToken ct)
    {
        try
        {
            await GetStringAsync(query, ct);
        }
        catch (PhoneException ex) when (ex.Kind == PhoneErrorKind.Unreachable && IsClosedWithoutResponse(ex))
        {
            throw new PhoneException(PhoneErrorKind.RemoteControlBlocked, ex.Detail, ex);
        }
    }

    /// <summary>Returns the phone's page title (usually contains the model).</summary>
    public override async Task<string> TestConnectionAsync(CancellationToken ct = default)
    {
        string html;
        try
        {
            html = await GetStringAsync("info.htm", ct);
        }
        catch (PhoneException ex) when (ex.Kind == PhoneErrorKind.HttpError && ex.Detail == "404")
        {
            html = await GetStringAsync("", ct);
        }

        var title = TitleRegex().Match(html);
        return title.Success ? WebUtility.HtmlDecode(title.Groups[1].Value).Trim() : "Snom";
    }

    /// <summary>Reads the configured SIP identities from /settings.xml.</summary>
    public override async Task<IReadOnlyList<PhoneIdentity>> GetIdentitiesAsync(CancellationToken ct = default)
    {
        var xml = await GetStringAsync("settings.xml", ct);
        return ParseIdentities(xml);
    }

    internal static IReadOnlyList<PhoneIdentity> ParseIdentities(string xml)
    {
        // The phone is trusted only so far: never process DTDs / entity definitions from it
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
        var doc = XDocument.Load(reader);

        Dictionary<int, string> Values(string name) => doc.Descendants(name)
            .Where(e => int.TryParse((string?)e.Attribute("idx"), out _))
            .GroupBy(e => int.Parse((string)e.Attribute("idx")!))
            .ToDictionary(g => g.Key, g => g.First().Value.Trim());

        var names = Values("user_name");
        var hosts = Values("user_host");
        var realNames = Values("user_realname");
        var active = Values("user_active");

        var identities = new List<PhoneIdentity>();
        foreach (var (idx, user) in names.OrderBy(kv => kv.Key))
        {
            if (string.IsNullOrEmpty(user) || !hosts.TryGetValue(idx, out var host) || string.IsNullOrEmpty(host))
                continue;
            if (active.TryGetValue(idx, out var isActive) && isActive == "off")
                continue;

            realNames.TryGetValue(idx, out var realName);
            identities.Add(new PhoneIdentity(idx, string.IsNullOrEmpty(realName) ? Loc.T("Identity.Numbered", idx) : realName, $"{user}@{host}"));
        }

        return identities;
    }

    [GeneratedRegex("<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();
}
