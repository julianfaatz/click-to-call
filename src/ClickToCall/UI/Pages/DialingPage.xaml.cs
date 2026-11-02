using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClickToCall.Core;

namespace ClickToCall.UI.Pages;

/// <summary>Everything about dialing: confirmation, notification, hotkeys, links; link types and number formatting under "advanced".</summary>
public partial class DialingPage : UserControl, ISectionPage
{
    private static readonly int[] SecondOptions = [5, 10, 15, 30, 60];
    private Window? _window; // Window.GetWindow() is already null in Unloaded
    private AppSettings? _beforeReset; // for "undo"

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
            ShowAdvanced(AdvancedPanel.IsVisible);
            IdentityCard.Visibility = Settings.Identities.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            RefreshLinkStatus();
            UpdateFormatting();
        };
        Unloaded += (_, _) =>
        {
            Settings.PropertyChanged -= Settings_PropertyChanged;
            if (_window is not null)
                _window.Activated -= Window_Activated;
            _window = null;
            _beforeReset = null;
            ResetDonePanel.Visibility = Visibility.Collapsed;
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
        {
            ShowAdvanced(true);
            NumbersSection.BringIntoView(new Rect(0, 0, 1, 400));
        }
    }

    // ---------- Advanced ----------

    private void AdvancedToggle_Click(object sender, RoutedEventArgs e)
    {
        ShowAdvanced(!AdvancedPanel.IsVisible);
        if (AdvancedPanel.IsVisible)
            Dispatcher.BeginInvoke(() => AdvancedPanel.BringIntoView(new Rect(0, 0, 1, 200)), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ShowAdvanced(bool show)
    {
        AdvancedPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        AdvancedToggleText.Text = Loc.T(show ? "Dialing.HideAdvanced" : "Dialing.ShowAdvanced");
        AdvancedToggleIcon.Text = show ? "" : ""; // chevron up / down
    }

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateFormatting();

    private void Window_Activated(object? sender, EventArgs e) => RefreshLinkStatus(); // user usually comes back from the Windows settings app

    private void FillSecondOptions()
    {
        var options = SecondOptions.Contains(Settings.NotificationSeconds) ? SecondOptions : [.. SecondOptions, Settings.NotificationSeconds];
        SecondsBox.ItemsSource = options.Order().ToDictionary(s => s, s => Loc.T("Dialing.SecondsOption", s));
    }

    // ---------- Reset ----------

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _beforeReset = Settings.Clone();
        Settings.ResetDialingSettings();
        FillSecondOptions();
        RefreshLinkStatus();

        StatusText.Set(ResetDoneText, Loc.T("Dialing.ResetDone"), StatusText.Kind.Success);
        ResetDonePanel.Visibility = Visibility.Visible;
    }

    private void UndoReset_Click(object sender, RoutedEventArgs e)
    {
        if (_beforeReset is not null)
            Settings.CopyDialingSettingsFrom(_beforeReset);
        _beforeReset = null;
        FillSecondOptions();
        RefreshLinkStatus();
        ResetDonePanel.Visibility = Visibility.Collapsed;
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
