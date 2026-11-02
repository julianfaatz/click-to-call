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
        Loaded += (_, _) =>
        {
            Settings.PropertyChanged += Settings_PropertyChanged;
            ShowCurrent();
        };
        Unloaded += (_, _) => Settings.PropertyChanged -= Settings_PropertyChanged;
        IsEnabledChanged += (_, _) => ShowCurrent(); // the hotkey was switched on or off
    }

    private static AppSettings Settings => SettingsStore.Current;

    // Changed elsewhere, e.g. reset to the defaults – not while the user is recording here
    private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSettings.Hotkey) or nameof(AppSettings.HangUpHotkey) && !Box.IsKeyboardFocused)
            ShowCurrent();
    }

    private HotkeyAction Other => Action == HotkeyAction.Dial ? HotkeyAction.HangUp : HotkeyAction.Dial;

    private void ShowCurrent()
    {
        var hotkey = Settings.HotkeyFor(Action);
        Box.Text = hotkey.ToString();
        ShowAvailability(hotkey, "Hotkey.Available");
    }

    private void ShowAvailability(Hotkey hotkey, string okKey)
    {
        // Switched off: "active" would be wrong, "in use" irrelevant
        if (!IsEnabled)
        {
            StatusText.Set(Status, "", StatusText.Kind.Neutral);
            return;
        }

        // e.g. Ctrl + Alt + Q from an older version or the defaults on another keyboard layout
        if (hotkey.BlockedCharacter() is { } blocked)
        {
            StatusText.Set(Status, Loc.T("Hotkey.BlocksCharacter", blocked), StatusText.Kind.Error);
            return;
        }

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
        {
            Box.Text = Hotkey.PartialText(Keyboard.Modifiers); // only modifiers so far: show what is held down
            return;
        }

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

        // Ctrl + Alt is AltGr: Ctrl + Alt + Q would make @ impossible to type on a German keyboard
        if (hotkey.BlockedCharacter() is { } blocked)
        {
            StatusText.Set(Status, Loc.T("Hotkey.BlocksCharacter", blocked), StatusText.Kind.Error);
            return;
        }

        Settings.SetHotkey(Action, hotkey);
        Box.Text = hotkey.ToString();
        ShowAvailability(hotkey, "Hotkey.Saved");
    }

    /// <summary>All keys released without a complete combination: show the saved one again.</summary>
    private void Box_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.None && Box.Text.EndsWith('…'))
            Box.Text = Settings.HotkeyFor(Action).ToString();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        Settings.SetHotkey(Action, Hotkey.DefaultFor(Action));
        ShowCurrent();
    }
}
