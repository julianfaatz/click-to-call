using ClickToCall.Core;

namespace ClickToCall.Tests;

public class SnomPhoneClientTests
{
    [Fact]
    public void Parses_identities_from_settings_xml()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <settings>
              <phone-settings>
                <user_active idx="1" perm="">on</user_active>
                <user_name idx="1" perm="">201</user_name>
                <user_host idx="1" perm="">pbx.example.com</user_host>
                <user_realname idx="1" perm="">Empfang</user_realname>
                <user_active idx="2" perm="">on</user_active>
                <user_name idx="2" perm="">202</user_name>
                <user_host idx="2" perm="">pbx.example.com</user_host>
                <user_realname idx="2" perm=""></user_realname>
                <user_active idx="3" perm="">off</user_active>
                <user_name idx="3" perm="">203</user_name>
                <user_host idx="3" perm="">pbx.example.com</user_host>
                <user_name idx="4" perm=""></user_name>
                <user_host idx="4" perm=""></user_host>
              </phone-settings>
            </settings>
            """;

        var identities = SnomPhoneClient.ParseIdentities(xml);

        Assert.Equal(2, identities.Count);
        Assert.Equal(new PhoneIdentity(1, "Empfang", "201@pbx.example.com"), identities[0]);
        Assert.Equal("202@pbx.example.com", identities[1].Uri);
    }

    [Fact]
    public void Requires_host() =>
        Assert.Throws<PhoneException>(() => new SnomPhoneClient(new PhoneConnection("", false, true, "", "")));

    [Theory]
    [InlineData("192.168.1 .50")]
    [InlineData("phone<1>")]
    public void Rejects_invalid_host(string host)
    {
        var ex = Assert.Throws<PhoneException>(() => new SnomPhoneClient(new PhoneConnection(host, false, true, "", "")));
        Assert.Equal(PhoneErrorKind.InvalidAddress, ex.Kind);
    }

    [Fact]
    public void Ignores_dtd_in_settings_xml()
    {
        const string xml = """
            <?xml version="1.0"?>
            <!DOCTYPE settings [<!ENTITY a "aaaaaaaaaa"><!ENTITY b "&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;">]>
            <settings><phone-settings><user_name idx="1">201</user_name><user_host idx="1">pbx</user_host></phone-settings></settings>
            """;

        Assert.Equal("201@pbx", Assert.Single(SnomPhoneClient.ParseIdentities(xml)).Uri);
    }

    [Theory]
    [InlineData("192.168.1.50", false, "http://192.168.1.50/")]
    [InlineData("http://192.168.1.50/", false, "http://192.168.1.50/")]
    [InlineData("192.168.1.50:8080", true, "https://192.168.1.50:8080/")]
    public void Builds_base_address(string host, bool https, string expected)
    {
        using var client = new SnomPhoneClient(new PhoneConnection(host, https, true, "admin", "pw"));
        Assert.Equal(expected, client.BaseAddress.ToString());
    }
}
