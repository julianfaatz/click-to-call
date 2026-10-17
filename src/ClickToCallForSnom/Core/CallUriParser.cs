namespace ClickToCallForSnom.Core;

/// <summary>Extracts the number from tel:, callto: and sip: URIs as passed by Windows on the command line.</summary>
public static class CallUriParser
{
    public static readonly string[] SupportedSchemes = ["tel", "callto", "sip"];

    public static bool IsCallUri(string arg) => SchemeOf(arg) is not null;

    public static string? SchemeOf(string arg)
    {
        var colon = arg.IndexOf(':');
        if (colon <= 0)
            return null;

        var scheme = arg[..colon].ToLowerInvariant();
        return SupportedSchemes.Contains(scheme) ? scheme : null;
    }

    /// <summary>
    /// "tel:+49-30-1234;phone-context=x" → "+49-30-1234", "callto://+4930" → "+4930",
    /// "sip:1234@pbx;transport=tls" → "1234@pbx".
    /// </summary>
    public static string ExtractTarget(string uri)
    {
        var colon = uri.IndexOf(':');
        var rest = colon >= 0 ? uri[(colon + 1)..] : uri;

        rest = Uri.UnescapeDataString(rest).Trim();
        rest = rest.TrimStart('/');

        // Drop URI parameters and query strings
        var cut = rest.IndexOfAny([';', '?']);
        if (cut >= 0)
            rest = rest[..cut];

        // Browsers sometimes append a trailing slash
        return rest.TrimEnd('/').Trim();
    }
}
