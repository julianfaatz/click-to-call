namespace ClickToCall.Core.Phones;

public sealed record PhoneConnection(
    string Host, bool UseHttps, bool IgnoreCertificateErrors, string Username, string Password, PhoneVendor Vendor = PhoneVendor.Snom)
{
    public static PhoneConnection From(AppSettings s) =>
        new(s.PhoneHost, s.UseHttps, s.IgnoreCertificateErrors, s.Username, s.Password, s.Vendor);
}
