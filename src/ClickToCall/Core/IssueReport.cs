namespace ClickToCall.Core;

/// <summary>Link to a new GitHub issue with the bug report form (.github/ISSUE_TEMPLATE/bug_report.yml) prefilled.</summary>
public static class IssueReport
{
    public static string BugReportUrl(AppSettings settings)
    {
        var phone = settings.PhoneModel is { Length: > 0 } model ? model
            : settings.IsPhoneConfigured ? settings.Vendor.ToString()
            : "";
        return BugReportUrl(phone, UpdateChecker.CurrentVersion, Environment.OSVersion.Version);
    }

    /// <remarks>The parameter names are the field ids of the issue form.</remarks>
    internal static string BugReportUrl(string phone, Version appVersion, Version osVersion)
    {
        var windows = $"Windows {(osVersion.Build >= 22000 ? 11 : 10)} (Build {osVersion.Build})";
        var query = new[]
        {
            ("template", "bug_report.yml"),
            ("phone", phone),
            ("version", appVersion.ToString()),
            ("windows", windows),
        };
        return UpdateChecker.RepositoryUrl + "/issues/new?"
            + string.Join("&", query.Where(p => p.Item2.Length > 0).Select(p => $"{p.Item1}={Uri.EscapeDataString(p.Item2)}"));
    }
}
