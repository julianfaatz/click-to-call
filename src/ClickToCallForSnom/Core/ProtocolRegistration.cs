using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ClickToCallForSnom.Core;

/// <summary>
/// Registers the app as handler for tel:, callto: and sip: links for the current user (no admin rights needed).
/// Since Windows 10 an app cannot make itself the default handler – the user has to pick it once in
/// "Settings → Apps → Default apps". <see cref="OpenDefaultAppsSettings"/> opens the right page.
/// </summary>
public static class ProtocolRegistration
{
    public const string RegisteredAppName = "Click to Call for Phones";
    // Name up to version 0.1 – removed so "Default apps" does not list the app twice
    private const string LegacyAppName = "Click to Call for Snom";
    private const string AppKey = @"Software\ClickToCallForSnom";
    private const string CapabilitiesKey = AppKey + @"\Capabilities";
    private const string ClassesKey = @"Software\Classes";
    private const string RegisteredApplicationsKey = @"Software\RegisteredApplications";

    public static string ProgId(string scheme) => "ClickToCallForSnom." + scheme;

    public static bool IsEnabled(AppSettings settings, string scheme) => scheme switch
    {
        "tel" => settings.HandleTel,
        "callto" => settings.HandleCallto,
        "sip" => settings.HandleSip,
        _ => false,
    };

    public static void Register(AppSettings settings, string exePath)
    {
        var command = $"\"{exePath}\" \"%1\"";

        using var caps = Registry.CurrentUser.CreateSubKey(CapabilitiesKey);
        caps.SetValue("ApplicationName", RegisteredAppName);
        caps.SetValue("ApplicationDescription", Loc.T("App.Description"));
        caps.SetValue("ApplicationIcon", $"\"{exePath}\",0");
        using var urlAssociations = caps.CreateSubKey("URLAssociations");

        foreach (var scheme in CallUriParser.SupportedSchemes)
        {
            var progId = ProgId(scheme);
            var enabled = IsEnabled(settings, scheme);

            if (enabled)
            {
                WriteProtocolClass($@"{ClassesKey}\{progId}", scheme, exePath, command);
                urlAssociations.SetValue(scheme, progId);
            }
            else
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"{ClassesKey}\{progId}", throwOnMissingSubKey: false);
                urlAssociations.DeleteValue(scheme, throwOnMissingValue: false);
            }

            // Direct class registration: makes links work right away when no other app owns the scheme yet.
            // We never overwrite another app's per-user registration.
            var directKey = $@"{ClassesKey}\{scheme}";
            var directOwner = ReadCommand(Registry.CurrentUser, directKey);
            if (enabled && (directOwner is null || IsOurCommand(directOwner)))
                WriteProtocolClass(directKey, scheme, exePath, command);
            else if (!enabled && directOwner is not null && IsOurCommand(directOwner))
                Registry.CurrentUser.DeleteSubKeyTree(directKey, throwOnMissingSubKey: false);
        }

        using (var registered = Registry.CurrentUser.CreateSubKey(RegisteredApplicationsKey))
        {
            registered.DeleteValue(LegacyAppName, throwOnMissingValue: false);
            registered.SetValue(RegisteredAppName, CapabilitiesKey);
        }

        NotifyAssociationsChanged();
    }

    public static void Unregister()
    {
        foreach (var scheme in CallUriParser.SupportedSchemes)
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"{ClassesKey}\{ProgId(scheme)}", throwOnMissingSubKey: false);

            var directKey = $@"{ClassesKey}\{scheme}";
            if (ReadCommand(Registry.CurrentUser, directKey) is { } cmd && IsOurCommand(cmd))
                Registry.CurrentUser.DeleteSubKeyTree(directKey, throwOnMissingSubKey: false);
        }

        Registry.CurrentUser.DeleteSubKeyTree(AppKey, throwOnMissingSubKey: false);
        using (var registered = Registry.CurrentUser.OpenSubKey(RegisteredApplicationsKey, writable: true))
        {
            registered?.DeleteValue(RegisteredAppName, throwOnMissingValue: false);
            registered?.DeleteValue(LegacyAppName, throwOnMissingValue: false);
        }

        NotifyAssociationsChanged();
    }

    /// <summary>True if Windows currently opens links of this scheme with this app.</summary>
    public static bool IsDefaultHandler(string scheme)
    {
        if (ReadUserChoice(scheme) is { } progId)
            return progId == ProgId(scheme) || (progId == scheme && ReadCommand(Registry.ClassesRoot, scheme) is { } c && IsOurCommand(c));

        // No explicit user choice: Windows uses the class registration
        return ReadCommand(Registry.ClassesRoot, scheme) is { } cmd && IsOurCommand(cmd);
    }

    /// <summary>
    /// The ProgId the user picked in the Windows settings. Newer Windows 11 builds store it in
    /// "UserChoiceLatest\ProgId" (and may leave an outdated "UserChoice" behind), older ones in "UserChoice".
    /// </summary>
    private static string? ReadUserChoice(string scheme)
    {
        var basePath = $@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\{scheme}";

        using (var latest = Registry.CurrentUser.OpenSubKey($@"{basePath}\UserChoiceLatest\ProgId"))
        {
            if (latest?.GetValue("ProgId") is string { Length: > 0 } progId)
                return progId;
        }

        using var choice = Registry.CurrentUser.OpenSubKey($@"{basePath}\UserChoice");
        return choice?.GetValue("ProgId") is string { Length: > 0 } legacy ? legacy : null;
    }

    public static void OpenDefaultAppsSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo(
                "ms-settings:defaultapps?registeredAppUser=" + Uri.EscapeDataString(RegisteredAppName))
            { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error("Could not open default apps settings", ex);
            Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
        }
    }

    private static void WriteProtocolClass(string keyPath, string scheme, string exePath, string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath);
        key.SetValue("", $"URL:{scheme}");
        key.SetValue("URL Protocol", "");
        using (var icon = key.CreateSubKey("DefaultIcon"))
            icon.SetValue("", $"\"{exePath}\",0");
        using (var cmd = key.CreateSubKey(@"shell\open\command"))
            cmd.SetValue("", command);
    }

    private static string? ReadCommand(RegistryKey root, string keyPath)
    {
        using var key = root.OpenSubKey($@"{keyPath}\shell\open\command");
        return key?.GetValue("") as string;
    }

    private static bool IsOurCommand(string command) =>
        command.Contains("ClickToCallForSnom.exe", StringComparison.OrdinalIgnoreCase);

    private static void NotifyAssociationsChanged() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
