using ClickToCallForSnom.Core;

namespace ClickToCallForSnom.Tests;

public class PhoneNumberNormalizerTests
{
    private static readonly NormalizationOptions German = new(
        ConvertOwnCountryCode: true, OwnCountryCode: "49", ConvertPlusToZeros: true, OutsideLinePrefix: "", OutsideLineMinLength: 5);

    [Theory]
    [InlineData("+49 30 1234567", "0301234567")]
    [InlineData("+49 (0)30 123 456-78", "03012345678")]
    [InlineData("+49 (0) 30 / 123 456", "030123456")]
    [InlineData("0049 89 12345", "08912345")]
    [InlineData("(030) 123 45-67", "0301234567")]
    [InlineData("030/1234567", "0301234567")]
    [InlineData("030.123.4567", "0301234567")]
    [InlineData("+43 1 234567", "00431234567")]
    [InlineData("+1 (555) 123-4567", "0015551234567")]
    [InlineData("  123  ", "123")]
    [InlineData("*21#", "*21#")]
    [InlineData("+49 30 12345", "03012345")]
    public void Normalizes_german_style(string input, string expected) =>
        Assert.Equal(expected, PhoneNumberNormalizer.Normalize(input, German));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("+")]
    public void Returns_empty_for_non_numbers(string input) =>
        Assert.Equal("", PhoneNumberNormalizer.Normalize(input, German));

    [Fact]
    public void Keeps_plus_when_conversion_is_disabled()
    {
        var options = German with { ConvertOwnCountryCode = false, ConvertPlusToZeros = false };
        Assert.Equal("+49301234", PhoneNumberNormalizer.Normalize("+49 30 1234", options));
    }

    [Fact]
    public void Converts_plus_only_when_own_country_is_disabled()
    {
        var options = German with { ConvertOwnCountryCode = false };
        Assert.Equal("0049301234", PhoneNumberNormalizer.Normalize("+49 30 1234", options));
    }

    [Theory]
    [InlineData("030 1234567", "00301234567")]
    [InlineData("+49 30 1234567", "00301234567")]
    [InlineData("1234", "1234")] // internal extension: no prefix
    public void Adds_outside_line_prefix_for_long_numbers(string input, string expected)
    {
        var options = German with { OutsideLinePrefix = "0", OutsideLineMinLength = 5 };
        Assert.Equal(expected, PhoneNumberNormalizer.Normalize(input, options));
    }

    [Fact]
    public void Dials_sip_addresses_as_they_are() =>
        Assert.Equal("alice@pbx.example.com", PhoneNumberNormalizer.Normalize("alice@pbx.example.com", German));

    [Fact]
    public void Uses_numeric_user_part_of_sip_address() =>
        Assert.Equal("0301234", PhoneNumberNormalizer.Normalize("+49301234@pbx.example.com", German));

    [Theory]
    [InlineData("+49 30 1234567", "+49 30 1234567")]
    [InlineData("Tel.: +49 (0)30 123 456-78\nFax: 030 99", "+49 (0)30 123 456-78")]
    [InlineData("Ruf mich an unter 0171 1234567, danke!", "0171 1234567")]
    [InlineData("Kein Treffer hier", "")]
    public void Extracts_number_from_text(string text, string expected) =>
        Assert.Equal(expected, PhoneNumberNormalizer.ExtractFromText(text));
}
