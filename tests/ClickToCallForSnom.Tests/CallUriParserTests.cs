using ClickToCallForSnom.Core;

namespace ClickToCallForSnom.Tests;

public class CallUriParserTests
{
    [Theory]
    [InlineData("tel:+49301234567", "+49301234567")]
    [InlineData("tel:+49-30-1234567", "+49-30-1234567")]
    [InlineData("tel:+49%2030%20123", "+49 30 123")]
    [InlineData("tel:030123;phone-context=+49", "030123")]
    [InlineData("TEL:030123", "030123")]
    [InlineData("callto://+4930123/", "+4930123")]
    [InlineData("callto:030123", "030123")]
    [InlineData("sip:1234@pbx.example.com;transport=tls", "1234@pbx.example.com")]
    [InlineData("sip:alice@pbx.example.com?subject=hi", "alice@pbx.example.com")]
    public void Extracts_target(string uri, string expected) =>
        Assert.Equal(expected, CallUriParser.ExtractTarget(uri));

    [Theory]
    [InlineData("tel:123", true)]
    [InlineData("Callto:123", true)]
    [InlineData("sip:a@b", true)]
    [InlineData("--settings", false)]
    [InlineData("C:\\file.txt", false)]
    [InlineData("https://example.com", false)]
    public void Detects_call_uris(string arg, bool expected) =>
        Assert.Equal(expected, CallUriParser.IsCallUri(arg));
}
