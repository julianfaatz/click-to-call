using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClickToCallForSnom.Core;

namespace ClickToCallForSnom.UI.Pages;

/// <summary>Everything about dialing: confirmation, notification, hotkey, link types and number formatting.</summary>
public partial class DialingPage : UserControl, ISectionPage
{
    private static readonly int[] SecondOptions = [5, 10, 15, 30, 60];
    private Window? _window; // Window.GetWindow() is already null in Unloaded

    public DialingPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Settings.PropertyChanged += Settings_PropertyChanged;
            _window = Window.GetWindow(this);
            if (_window is not null)
                _window.Activated += Window_Activated;

            FillSecondOptions();
            IdentityCard.Visibility = Settings.Identities.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ShowHotkey();
            RefreshLinkStatus();
            UpdateFormatting();
        };
        Unloaded += (_, _) =>
        {
            Settings.PropertyChanged -= Settings_PropertyChanged;
            if (_window is not null)
                _window.Activated -= Window_Activated;
            _window = null;
        };
    }

    private static AppSettings Settings => SettingsStore.Current;

    public void ShowSection(string section)
    {
        if (section == "Hotkey")
            HotkeySection.BringIntoView(new Rect(0, 0, 1, 400));
        else if (section == "Links")
            LinksSection.BringIntoView(new Rect(0, 0, 1, 400));
        else if (section == "Numbers")
            NumbersSection.BringIntoView(new Rect(0, 0, 1, 400));
    }

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateFormatting();

    private void Window_Activated(object? sender, EventArgs e) => RefreshLinkStatus(); // user usually comes back from the Windows settings app

    private void FillSecondOptions()
    {
        var options = SecondOptions.Contains(Settings.NotificationSeconds) ? SecondOptions : [.. SecondOptions, Settings.NotificationSeconds];
        SecondsBox.ItemsSource = options.Order().ToDictionary(s => s, s => Loc.T("Dialing.SecondsOption", s));
    }

    // ---------- Hotkey ----------

    private void ShowHotkey()
    {
        HotkeyBox.Text = Settings.Hotkey.ToString();
        var available = App.Services?.Hotkeys.IsAvailable(Settings.Hotkey) ?? true;
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
            StatusText.Set(HotkeyStatus, Loc.T("Hotkey.NeedModifier"), StatusText.Kind.Error);
            return;
        }

        Settings.Hotkey = hotkey;
        HotkeyBox.Text = hotkey.ToString();

        var available = App.Services?.Hotkeys.IsAvailable(hotkey) ?? true;
        StatusText.Set(HotkeyStatus,
            Loc.T(available ? "Hotkey.Saved" : "Hotkey.InUse"),
            available ? StatusText.Kind.Success : StatusText.Kind.Error);
    }

    private void ResetHotkey_Click(object sender, RoutedEventArgs e)
    {
        Settings.Hotkey = Hotkey.Default;
        ShowHotkey();
    }

    // ---------- Links ----------

    private void OpenDefaultApps_Click(object sender, RoutedEventArgs e)
    {
        // Make sure Windows knows about the currently selected link types before the user picks the app
        if (Environment.ProcessPath is { } exe)
            ProtocolRegistration.Register(Settings, exe);

        ProtocolRegistration.OpenDefaultAppsSettings();
    }

    private void RefreshLinkStatus()
    {
        var telDefault = ShowLinkStatus(TelStatus, "tel");
        ShowLinkStatus(CalltoStatus, "callto");
        ShowLinkStatus(SipStatus, "sip");

        DefaultAppCard.Description = Loc.T(telDefault ? "Home.LinksActive" : "Home.StepLinksTodo");
        DefaultAppButton.Content = Loc.T(telDefault ? "Protocols.OpenDefaultApps" : "Home.LinksSet");
        if (telDefault)
            DefaultAppButton.ClearValue(StyleProperty);
        else
            DefaultAppButton.Style = (Style)FindResource("AccentButtonStyle");
    }

    private static bool ShowLinkStatus(TextBlock target, string scheme)
    {
        var isDefault = ProtocolRegistration.IsDefaultHandler(scheme);
        StatusText.Set(target,
            Loc.T(isDefault ? "Protocols.IsDefault" : "Protocols.NotDefault"),
            isDefault ? StatusText.Kind.Success : StatusText.Kind.Neutral);
        if (!isDefault)
            target.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");
        return isDefault;
    }

    // ---------- Number formatting ----------

    private void TryInput_TextChanged(object sender, TextChangedEventArgs e) => UpdateFormatting();

    private void UpdateFormatting()
    {
        if (TryOutput is null)
            return;

        var code = Settings.OwnCountryCode.Length > 0 ? Settings.OwnCountryCode : "49";
        CountryCard.Description = Loc.T("Dialing.ConvertOwnCountryHelp", code);

        var result = PhoneNumberNormalizer.Normalize(TryInput.Text, NormalizationOptions.From(Settings));
        TryOutput.Text = result.Length > 0 ? result : "–";
    }
}
