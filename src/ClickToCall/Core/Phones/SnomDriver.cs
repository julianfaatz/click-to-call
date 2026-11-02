using System.Text.RegularExpressions;

namespace ClickToCall.Core.Phones;

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
