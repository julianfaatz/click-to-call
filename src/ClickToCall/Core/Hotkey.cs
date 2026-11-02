using System.Windows.Input;

namespace ClickToCall.Core;

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

/// <summary>What a global hotkey does.</summary>
public enum HotkeyAction
{
    Dial,
    HangUp,
}

/// <summary>A global hotkey: modifiers plus a Win32 virtual key code.</summary>
public sealed record Hotkey(HotkeyModifiers Modifiers, uint VirtualKey)
{
    /// <summary>Ctrl + Alt + W ("Wählen").</summary>
    public static Hotkey Default { get; } = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x57);

    /// <summary>Ctrl + Alt + A ("Auflegen"). Not Q, E or M: Ctrl + Alt is AltGr on German keyboards (@, €, µ).</summary>
    public static Hotkey DefaultHangUp { get; } = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x41);

    public static Hotkey DefaultFor(HotkeyAction action) => action == HotkeyAction.HangUp ? DefaultHangUp : Default;

    public bool IsValid => VirtualKey != 0 && Modifiers != HotkeyModifiers.None;

    public static Hotkey? FromWpf(ModifierKeys modifiers, Key key)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift
            or Key.RightShift or Key.LWin or Key.RWin or Key.System or Key.None)
            return null;

        return new Hotkey(FromWpf(modifiers), (uint)KeyInterop.VirtualKeyFromKey(key));
    }

    /// <summary>
    /// The character this combination types in the current keyboard layout: Ctrl + Alt is AltGr, e.g. @ for Q or € for E
    /// on German keyboards. As a hotkey it would make that character impossible to type. Null if it types nothing.
    /// </summary>
    public string? BlockedCharacter()
    {
        if ((Modifiers & ~HotkeyModifiers.Shift) != (HotkeyModifiers.Control | HotkeyModifiers.Alt))
            return null;

        var state = new byte[256];
        foreach (var vk in new[] { NativeMethods.VK_CONTROL, NativeMethods.VK_MENU, NativeMethods.VK_LCONTROL, NativeMethods.VK_RMENU })
            state[vk] = 0x80;
        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
            state[NativeMethods.VK_SHIFT] = 0x80;

        var buffer = new char[4];
        var scanCode = NativeMethods.MapVirtualKey(VirtualKey, NativeMethods.MAPVK_VK_TO_VSC);
        var count = NativeMethods.ToUnicodeEx(VirtualKey, scanCode, state, buffer, buffer.Length,
            NativeMethods.TOUNICODE_KEEP_STATE, NativeMethods.GetKeyboardLayout(0));
        if (count < 0)
            count = 1; // dead key (e.g. ~ before a letter): blocked as well

        var text = new string(buffer, 0, Math.Min(count, buffer.Length));
        // Without an AltGr character Windows returns nothing or a control character (Ctrl + W = 0x17)
        return text.Length == 0 || text.Any(char.IsControl) || string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public override string ToString() => string.Join(" + ", Keys());

    /// <summary>The keys to press, e.g. "Ctrl", "Alt", "W" – to show them as keyboard keys.</summary>
    public List<string> Keys() => [.. ModifierNames(Modifiers), KeyName(VirtualKey)];

    /// <summary>Modifiers held down so far while recording a new combination, e.g. "Ctrl + Alt + …".</summary>
    public static string PartialText(ModifierKeys modifiers) => string.Join(" + ", [.. ModifierNames(FromWpf(modifiers)), "…"]);

    private static HotkeyModifiers FromWpf(ModifierKeys modifiers)
    {
        var mods = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control)) mods |= HotkeyModifiers.Control;
        if (modifiers.HasFlag(ModifierKeys.Alt)) mods |= HotkeyModifiers.Alt;
        if (modifiers.HasFlag(ModifierKeys.Shift)) mods |= HotkeyModifiers.Shift;
        if (modifiers.HasFlag(ModifierKeys.Windows)) mods |= HotkeyModifiers.Win;
        return mods;
    }

    private static List<string> ModifierNames(HotkeyModifiers modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add(Loc.T("Key.Ctrl"));
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add(Loc.T("Key.Shift"));
        if (modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        return parts;
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
