using Microsoft.Win32;

namespace ClickToCall.Core;

/// <summary>Start with Windows via HKCU\...\Run (the installer uses the same value).</summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClickToCall";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void Set(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, $"\"{exePath}\" --autostart");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
