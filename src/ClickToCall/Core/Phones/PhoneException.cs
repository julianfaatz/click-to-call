namespace ClickToCall.Core.Phones;

public enum PhoneErrorKind
{
    NotConfigured,
    InvalidAddress,
    Unreachable,
    Timeout,
    Unauthorized,
    Forbidden,
    Certificate,
    HttpError,
    /// <summary>The phone answers, but refuses remote control commands (Snom: "hidden tags").</summary>
    RemoteControlBlocked,
}

public sealed class PhoneException(PhoneErrorKind kind, string? detail = null, Exception? inner = null)
    : Exception(detail ?? kind.ToString(), inner)
{
    public PhoneErrorKind Kind { get; } = kind;
    public string? Detail { get; } = detail;

    public string UserMessage => Kind switch
    {
        PhoneErrorKind.HttpError => Loc.T("Error.HttpError", Detail ?? "?"),
        PhoneErrorKind.InvalidAddress => Loc.T("Error.InvalidAddress", Detail ?? ""),
        _ => Loc.T("Error." + Kind),
    };
}
