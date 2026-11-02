namespace ClickToCall.Core.Phones;

public sealed record FoundPhone(string Address, string Model, PhoneVendor Vendor)
{
    public string DisplayName => $"{Model} · {Address}";
}
