using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClickToCallForSnom.Core;

namespace ClickToCallForSnom.UI.Pages;

public partial class HotkeyPage : UserControl
{
    public HotkeyPage()
    {
        InitializeComponent();
        Loaded += (_, _) => ShowHotkey();
    }

    private static AppSettings Settings => SettingsStore.Current;

    private void ShowHotkey()
    {
        var settings = Settings;
        HotkeyBox.Text = settings.Hotkey.ToString();
        var available = App.Services?.Hotkeys.IsAvailable(settings.Hotkey) ?? true;
        StatusText.Set(HotkeyStatus,
            Loc.T(available ? "Hotkey.Available" : "Hotkey.InUse"),
            available ? StatusText.Kind.Success : StatusText.Kind.Error);
    }

    // While recording, the active global hotkey must not fire
    private void HotkeyBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (App.Services is not null)
            App.Services.HotkeyRecording = true;
        HotkeyStatus.Text = Loc.T("Hotkey.Recording");
        HotkeyStatus.ClearValue(TextBlock.ForegroundProperty);
    }

    private void HotkeyBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (App.Services is not null)
            App.Services.HotkeyRecording = false;
        ShowHotkey();
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key is Key.Tab)
        {
            MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            return;
        }

        var hotkey = Hotkey.FromWpf(Keyboard.Modifiers, key);
        if (hotkey is null)
            return; // only a modifier so far

        if (hotkey.Modifiers == HotkeyModifiers.None)
        {
            StatusText.Set(HotkeyStatus, Loc.T("Hotkey.NeedModifier"), StatusText.Kind.Error);
            return;
        }

        Settings.Hotkey = hotkey;
        HotkeyBox.Text = hotkey.ToString();

        var available = App.Services?.Hotkeys.IsAvailable(hotkey) ?? true;
        StatusText.Set(HotkeyStatus,
            Loc.T(available ? "Hotkey.Available" : "Hotkey.InUse"),
            available ? StatusText.Kind.Success : StatusText.Kind.Error);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        Settings.Hotkey = Hotkey.Default;
        ShowHotkey();
    }
}
