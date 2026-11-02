using System.Net;
using System.Text.RegularExpressions;

namespace ClickToCall.Core.Network;

/// <summary>What a device answered on its web page, or why it did not answer.</summary>
/// <param name="Url">The page that answered, after redirects.</param>
/// <param name="Previous">The response that redirected here, if any.</param>
public sealed partial record ProbeResponse(
    string Url, int Status = 0, string Server = "", string Authenticate = "", string Location = "", string Body = "", string? Error = null,
    ProbeResponse? Previous = null)
{
    /// <summary>The device answered – possibly only with a redirect to a page that then did not answer.</summary>
    public bool Answered => Error is null || Previous is { Answered: true };

    /// <summary>
    /// Server header, login realm, redirect and page of all responses: what a device can be recognized by.
    /// Some only name themselves in the redirect to their login page.
    /// </summary>
    public string Text => string.Join(' ', Previous?.Text ?? "", Server, Authenticate, Location, Body);

    /// <summary>For the log: enough to see why a phone was not recognized, without the whole page.</summary>
    public override string ToString() => Previous is null ? Summary() : $"{Previous} -> {Summary()}";

    private string Summary()
    {
        if (Error is not null)
            return $"{Url}: {Error}";

        var title = TitleRegex().Match(Body) is { Success: true } match ? Shorten(WebUtility.HtmlDecode(match.Groups[1].Value)) : "";
        var parts = new List<string> { $"{Url}: {Status}" };
        if (Server.Length > 0)
            parts.Add($"server \"{Server}\"");
        if (Authenticate.Length > 0)
            parts.Add($"login \"{Shorten(Authenticate)}\"");
        if (Location.Length > 0)
            parts.Add($"redirect to {Location}");
        // Single page web interfaces often have no meaningful title: then the start of the page tells more
        parts.Add(title.Length > 0 ? $"title \"{title}\"" : $"page \"{Shorten(Body, 200)}\"");
        parts.Add($"{Body.Length} chars");
        return string.Join(", ", parts);
    }

    private static string Shorten(string text, int maxLength = 80)
    {
        text = WhitespaceRegex().Replace(text, " ").Trim();
        return text.Length <= maxLength ? text : text[..maxLength] + "…";
    }

    [GeneratedRegex("<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
