using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClickToCall.Core;

namespace ClickToCall.UI;

/// <summary>Shows the key combination of a global hotkey; click it and press a new combination to change it.</summary>
public partial class HotkeyRecorder : UserControl
{
    /// <summary>The hotkey shown and changed here.</summary>
    public HotkeyAction Action { get; set; }

    public HotkeyRecorder()
    {
        InitializeComponent();
        Loaded += (_, _) => ShowCurrent();
    }

    private static AppSettings Settings => SettingsStore.Current;

    private HotkeyAction Other => Action == HotkeyAction.Dial ? HotkeyAction.HangUp : HotkeyAction.Dial;

    private void ShowCurrent()
    {
        var hotkey = Settings.HotkeyFor(Action);
        Box.Text = hotkey.ToString();
        ShowAvailability(hotkey, "Hotkey.Available");
    }

    private void ShowAvailability(Hotkey hotkey, string okKey)
    {
        var available = App.Services?.Hotkeys.IsAvailable(hotkey) ?? true;
        StatusText.Set(Status, Loc.T(available ? okKey : "Hotkey.InUse"), available ? StatusText.Kind.Success : StatusText.Kind.Error);
    }

    // While recording, the active global hotkeys must not fire
    private void Box_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (App.Services is not null)
            App.Services.HotkeyRecording = true;
        StatusText.Set(Status, Loc.T("Hotkey.Recording"), StatusText.Kind.Neutral);
    }

    private void Box_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (App.Services is not null)
            App.Services.HotkeyRecording = false;
        ShowCurrent();
    }

    private void Box_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key is Key.Tab)
        {
            Box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            return;
        }

        if (key is Key.Escape)
        {
            Keyboard.ClearFocus();
            return;
        }

        var hotkey = Hotkey.FromWpf(Keyboard.Modifiers, key);
        if (hotkey is null)
            return; // only a modifier so far

        // Shift alone would swallow e.g. the capital letter system wide
        if ((hotkey.Modifiers & ~HotkeyModifiers.Shift) == HotkeyModifiers.None)
        {
            StatusText.Set(Status, Loc.T("Hotkey.NeedModifier"), StatusText.Kind.Error);
            return;
        }

        // One combination can only do one thing
        if (Settings.IsHotkeyEnabled(Other) && Settings.HotkeyFor(Other) == hotkey)
        {
            StatusText.Set(Status, Loc.T(Other == HotkeyAction.HangUp ? "Hotkey.UsedForHangUp" : "Hotkey.UsedForDial"), StatusText.Kind.Error);
            return;
        }

        Settings.SetHotkey(Action, hotkey);
        Box.Text = hotkey.ToString();
        ShowAvailability(hotkey, "Hotkey.Saved");
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        Settings.SetHotkey(Action, Hotkey.DefaultFor(Action));
        ShowCurrent();
    }
}
