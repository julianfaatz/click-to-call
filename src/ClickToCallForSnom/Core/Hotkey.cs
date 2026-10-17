using System.Windows.Input;

namespace ClickToCallForSnom.Core;

/// <summary>Modifier flags as used by the Win32 RegisterHotKey API.</summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

/// <summary>A global hotkey: modifiers plus a Win32 virtual key code.</summary>
public sealed record Hotkey(HotkeyModifiers Modifiers, uint VirtualKey)
{
    /// <summary>Ctrl + Alt + W ("Wählen").</summary>
    public static Hotkey Default { get; } = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x57);

    public bool IsValid => VirtualKey != 0 && Modifiers != HotkeyModifiers.None;

    public static Hotkey? FromWpf(ModifierKeys modifiers, Key key)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift
            or Key.RightShift or Key.LWin or Key.RWin or Key.System or Key.None)
            return null;

        var mods = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control)) mods |= HotkeyModifiers.Control;
        if (modifiers.HasFlag(ModifierKeys.Alt)) mods |= HotkeyModifiers.Alt;
        if (modifiers.HasFlag(ModifierKeys.Shift)) mods |= HotkeyModifiers.Shift;
        if (modifiers.HasFlag(ModifierKeys.Windows)) mods |= HotkeyModifiers.Win;

        return new Hotkey(mods, (uint)KeyInterop.VirtualKeyFromKey(key));
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add(Loc.T("Key.Ctrl"));
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add(Loc.T("Key.Shift"));
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(KeyName(VirtualKey));
        return string.Join(" + ", parts);
    }

    private static string KeyName(uint virtualKey)
    {
        var key = KeyInterop.KeyFromVirtualKey((int)virtualKey);
        switch (key)
        {
            case >= Key.NumPad0 and <= Key.NumPad9:
                return "Num " + (key - Key.NumPad0);
            case Key.Space:
                return Loc.T("Key.Space");
            case Key.Return:
                return "Enter";
            case >= Key.F1 and <= Key.F24:
                return key.ToString();
        }

        // Letters, digits and OEM keys: use the character of the current keyboard layout (e.g. Ö on German keyboards)
        var ch = NativeMethods.MapVirtualKey(virtualKey, NativeMethods.MAPVK_VK_TO_CHAR) & 0x7FFF;
        return ch > 32 ? char.ToUpper((char)ch).ToString() : key.ToString();
    }
}
