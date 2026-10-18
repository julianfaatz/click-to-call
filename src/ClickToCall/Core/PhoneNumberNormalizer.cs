using System.Text;
using System.Text.RegularExpressions;

namespace ClickToCall.Core;

public sealed record NormalizationOptions(
    bool ConvertOwnCountryCode,
    string OwnCountryCode,
    bool ConvertPlusToZeros,
    string OutsideLinePrefix,
    int OutsideLineMinLength)
{
    public static NormalizationOptions From(AppSettings s) => new(
        s.ConvertOwnCountryCode, s.OwnCountryCode, s.ConvertPlusToZeros, s.OutsideLinePrefix, s.OutsideLineMinLength);
}

/// <summary>Turns human formatted numbers like "+49 (0)30 / 123-456" into dialable strings like "030123456".</summary>
public static partial class PhoneNumberNormalizer
{
    // "+49 (0) 30" style: the optional national trunk prefix in parentheses must be dropped
    [GeneratedRegex(@"^\s*(\+|00)\s*(\d{1,3})\s*\(\s*0\s*\)")]
    private static partial Regex TrunkPrefixInParentheses();

    // Something that looks like a phone number inside arbitrary text
    [GeneratedRegex(@"(?:\+|\b00|\b0|\b[1-9])[\d\s()/.\-–]{2,}\d")]
    private static partial Regex NumberInText();

    public static string Normalize(string input, NormalizationOptions options)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "";

        var text = input.Trim();

        // SIP addresses with a non numeric user part (e.g. "alice@pbx.local") are dialed as they are
        if (text.Contains('@') && !IsNumericUserPart(text))
            return text;
        if (text.Contains('@'))
            text = text[..text.IndexOf('@')];

        text = TrunkPrefixInParentheses().Replace(text, "$1$2");

        // Keep digits, a leading plus and the phone keys * and #
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (char.IsAsciiDigit(ch) || ch is '*' or '#')
                sb.Append(ch);
            else if (ch == '+' && sb.Length == 0)
                sb.Append(ch);
        }

        var number = sb.ToString();
        if (number is "" or "+")
            return "";

        // Treat 0049... like +49...
        if (number.StartsWith("00"))
            number = "+" + number[2..];

        if (number.StartsWith('+'))
        {
            var cc = options.OwnCountryCode.Trim().TrimStart('+');
            if (options.ConvertOwnCountryCode && cc.Length > 0 && number[1..].StartsWith(cc))
                number = "0" + number[(1 + cc.Length)..];
            else if (options.ConvertPlusToZeros)
                number = "00" + number[1..];
        }

        if (!string.IsNullOrEmpty(options.OutsideLinePrefix)
            && !number.StartsWith('+')
            && !number.StartsWith('*')
            && !number.StartsWith('#')
            && number.Length >= options.OutsideLineMinLength)
        {
            number = options.OutsideLinePrefix + number;
        }

        return number;
    }

    /// <summary>Returns the first phone number like part of a text (e.g. from an e-mail signature), or the text itself.</summary>
    public static string ExtractFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var trimmed = text.Trim();
        if (IsSingleNumber(trimmed))
            return trimmed;

        var match = NumberInText().Match(trimmed);
        return match.Success ? match.Value.Trim() : "";
    }

    /// <summary>
    /// True when the whole text is one phone number (e.g. a selection of "+49 30 1234-56"),
    /// not a text that merely contains one ("Tel.: 030 1234", a signature).
    /// </summary>
    public static bool IsSingleNumber(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= 40 && LooksLikeNumber(trimmed);
    }

    /// <summary>True when the text consists only of characters that appear in written phone numbers.</summary>
    public static bool LooksLikeNumber(string text)
    {
        var digits = 0;
        foreach (var ch in text.Trim())
        {
            if (char.IsAsciiDigit(ch))
                digits++;
            else if (!(ch is '+' or ' ' or '(' or ')' or '-' or '–' or '/' or '.' or '*' or '#' or ' '))
                return false;
        }

        return digits >= 2;
    }

    private static bool IsNumericUserPart(string address)
    {
        var user = address[..address.IndexOf('@')];
        return user.Length > 0 && LooksLikeNumber(user);
    }
}
