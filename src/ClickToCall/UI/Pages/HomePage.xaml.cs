using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClickToCall.Core;
using ClickToCall.Core.Phones;
using ClickToCall.Services;

namespace ClickToCall.UI.Pages;

/// <summary>
/// Start page: shows the connected phone (or lets the user connect one), guides through the setup
/// and afterwards explains the everyday use.
/// </summary>
public partial class HomePage : UserControl
{
    private CancellationTokenSource? _statusCts;
    private bool _updatingLines;
    private Window? _window; // Window.GetWindow() is already null in Unloaded

    public HomePage()
    {
        InitializeComponent();
        StatusText.Set(Step2Done, Loc.T("Home.StepLinksDone"), StatusText.Kind.Success);
        ConnectForm.Connected += ConnectForm_Connected;
        ConnectForm.Cancelled += RefreshAll;
        Stepper.StepClicked += GoToStep;
        Loaded += (_, _) =>
        {
            Settings.PropertyChanged += Settings_PropertyChanged;
            SettingsStore.Changed += SettingsStore_Changed;
            RefreshAll();
            if (Settings.IsPhoneConfigured)
                _ = CheckStatusAsync();
            else if (!ConnectForm.IsOpen)
                OpenConnectForm();

            _window = Window.GetWindow(this);
            if (_window is not null)
                _window.Activated += Window_Activated;
        };
        Unloaded += (_, _) =>
        {
            Settings.PropertyChanged -= Settings_PropertyChanged;
            SettingsStore.Changed -= SettingsStore_Changed;
            if (_window is not null)
                _window.Activated -= Window_Activated;
            _window = null;
            _statusCts?.Cancel();
        };
    }

    private static AppSettings Settings => SettingsStore.Current;

    private void Window_Activated(object? sender, EventArgs e) => RefreshSetup(); // user may come back from Windows settings

    private void SettingsStore_Changed(object? sender, EventArgs e) => RefreshSetup(); // e.g. a call was made from the tray

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSettings.Hotkey) or nameof(AppSettings.HotkeyEnabled))
            RefreshSetup();
    }

    private void RefreshAll()
    {
        RefreshPhoneCard();
        RefreshSetup();
    }

    // ---------- Phone card ----------

    private void RefreshPhoneCard()
    {
        var configured = Settings.IsPhoneConfigured;

        PhoneTitle.Text = configured
            ? (string.IsNullOrEmpty(Settings.PhoneModel) ? PhoneDrivers.For(Settings.Vendor).Name : Settings.PhoneModel)
            : Loc.T("Home.NoPhone");
        PhoneSubtitle.Text = configured ? Settings.PhoneHost : Loc.T("Home.NoPhoneText");

        PhoneIcon.Foreground = configured ? (Brush)FindResource("AccentTextFillColorPrimaryBrush") : (Brush)FindResource("TextFillColorSecondaryBrush");
        StatusPill.Visibility = configured ? Visibility.Visible : Visibility.Collapsed;
        ChangeButton.Visibility = configured && !ConnectForm.IsOpen ? Visibility.Visible : Visibility.Collapsed;
        RecheckButton.Visibility = configured && !ConnectForm.IsOpen ? Visibility.Visible : Visibility.Collapsed;
        CancelChangeButton.Visibility = configured && ConnectForm.IsOpen ? Visibility.Visible : Visibility.Collapsed;

        // e.g. Yealink: the PC has to be allowed to control the phone
        var hint = configured ? PhoneDrivers.For(Settings.Vendor).UsageHint(Settings.PhoneHost) : null;
        PhoneHintTip.Help = hint ?? "";
        PhoneHintTip.Visibility = hint is null ? Visibility.Collapsed : Visibility.Visible;
        Step3HintText.Text = hint ?? "";
        Step3Hint.Visibility = hint is null || TestHelpPanel.IsVisible ? Visibility.Collapsed : Visibility.Visible;
        TestHelpVendor.Text = hint ?? "";
        TestHelpVendor.Visibility = hint is null ? Visibility.Collapsed : Visibility.Visible;

        RefreshLines();
    }

    private void RefreshLines()
    {
        _updatingLines = true;
        var show = Settings.IsPhoneConfigured && Settings.Identities.Count > 1;
        LinePanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show)
        {
            var options = new List<PhoneIdentity> { new(0, Loc.T("Identity.PhoneDefault"), "") };
            options.AddRange(Settings.Identities);
            LineBox.ItemsSource = options;
            LineBox.SelectedItem = options.FirstOrDefault(i => i.Uri == Settings.DefaultIdentityUri) ?? options[0];
        }
        _updatingLines = false;
    }

    private void LineBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingLines && LineBox.SelectedItem is PhoneIdentity identity)
            Settings.DefaultIdentityUri = identity.Uri;
    }

    private enum PhoneState
    {
        Checking,
        Online,
        Offline,
    }

    private void SetStatus(PhoneState state, string? detail = null)
    {
        (StatusLabel.Text, StatusDot.Fill) = state switch
        {
            PhoneState.Online => (Loc.T("Home.Online"), (Brush)FindResource("SystemFillColorSuccessBrush")),
            PhoneState.Offline => (detail ?? Loc.T("Home.Offline"), (Brush)FindResource("SystemFillColorCriticalBrush")),
            _ => (Loc.T("Home.Checking"), (Brush)FindResource("TextFillColorSecondaryBrush")),
        };
    }

    private async Task CheckStatusAsync()
    {
        _statusCts?.Cancel();
        _statusCts = new CancellationTokenSource();
        var ct = _statusCts.Token;

        SetStatus(PhoneState.Checking);
        try
        {
            await PhoneSetup.CheckAsync(ct);
            SetStatus(PhoneState.Online);
            RefreshPhoneCard(); // the model name may have changed
        }
        catch (PhoneException ex)
        {
            if (!ct.IsCancellationRequested)
                SetStatus(PhoneState.Offline, ex.UserMessage);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Recheck_Click(object sender, RoutedEventArgs e) => _ = CheckStatusAsync();

    // ---------- Connecting a phone ----------

    private void ShowConnectForm_Click(object sender, RoutedEventArgs e) => OpenConnectForm();

    private void CancelChange_Click(object sender, RoutedEventArgs e) => ConnectForm.Cancel();

    private void OpenConnectForm()
    {
        _justConnected = false;
        ConnectForm.Open();
        RefreshAll();
    }

    private async void ConnectForm_Connected(ConnectResult result)
    {
        // During the setup: confirm it right here and let the user go on with "Next" (and pick a line first if there are several)
        if (!SetupProgress.Current().IsReady)
        {
            _viewStep = 1;
            _justConnected = true;
            StatusText.Set(ConnectedNote, Loc.T("Home.ConnectedText", result.Model), StatusText.Kind.Success);
            _ = Dispatcher.BeginInvoke(() => Step1NextButton.Focus(), System.Windows.Threading.DispatcherPriority.Input);
        }
        RefreshAll();
        SetStatus(PhoneState.Online);

        await PhoneSetup.LoadIdentitiesAsync();
        RefreshLines();

        // The user clicked a link before the phone was set up: continue with it
        if (App.Services?.Calls.TakePendingNumber() is { } pending)
            App.Services.Calls.ShowDialWindow(pending, fromLink: true);
    }

    // ---------- Guided setup ----------

    /// <summary>Step the user explicitly went back to (null = the next open step).</summary>
    private int? _viewStep;
    private bool _justConnected;
    private bool _justCompleted;
    private bool _linksSettingsOpened;

    private void GoToStep(int step)
    {
        var next = SetupProgress.Current().NextOpenStep;
        if (step > next)
            return; // steps can only be done in order
        _viewStep = step == next ? null : step;
        _justConnected = false;
        RefreshAll();
    }

    private void RefreshSetup()
    {
        var progress = SetupProgress.Current();
        var ready = progress.IsReady;
        TitleText.Text = Loc.T(ready ? "Home.Title" : "Home.SetupTitle");
        SubtitleText.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        Stepper.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        ReadyPanel.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        DoneBanner.Visibility = ready && _justCompleted ? Visibility.Visible : Visibility.Collapsed;

        if (_viewStep > progress.NextOpenStep)
            _viewStep = null;
        var current = ready ? 0 : _viewStep ?? progress.NextOpenStep;

        PhoneCard.Visibility = ready || current == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step1Next.Visibility = current == 1 && Settings.IsPhoneConfigured && !ConnectForm.IsOpen ? Visibility.Visible : Visibility.Collapsed;
        ConnectedNote.Visibility = _justConnected ? Visibility.Visible : Visibility.Collapsed;
        Step2Card.Visibility = current == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Card.Visibility = current == 3 ? Visibility.Visible : Visibility.Collapsed;

        if (!ready)
            Stepper.Update(progress, current);

        // Step 2 content
        var linksDone = progress.LinksDone;
        Step2Instructions.Visibility = linksDone ? Visibility.Collapsed : Visibility.Visible;
        Step2Done.Visibility = linksDone ? Visibility.Visible : Visibility.Collapsed;
        Step2Waiting.Visibility = !linksDone && _linksSettingsOpened ? Visibility.Visible : Visibility.Collapsed;
        Step2Button.Visibility = linksDone ? Visibility.Collapsed : Visibility.Visible;
        Step2NextButton.Visibility = linksDone ? Visibility.Visible : Visibility.Collapsed;
        Step2SkipButton.Visibility = linksDone ? Visibility.Collapsed : Visibility.Visible;
        Step2Button.Content = Loc.T(_linksSettingsOpened ? "Home.StepLinksButtonAgain" : "Home.StepLinksButton");

        // Everyday use
        LinksTip.Description = Loc.T(linksDone ? "Home.LinksActive" : "Home.StepLinksTodo");
        LinksTipButton.Visibility = linksDone ? Visibility.Collapsed : Visibility.Visible;
        HotkeyTip.Title = Settings.HotkeyEnabled ? Loc.T("Home.HotkeyTitle", Settings.Hotkey) : Loc.T("Home.HotkeyOffTitle");
        HotkeyTip.Description = Loc.T(Settings.HotkeyEnabled ? "Home.HotkeyDescription" : "Home.HotkeyOff");
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        _viewStep = null;
        _justConnected = false;
        RefreshAll();
        FocusStepAction();
    }

    /// <summary>Keyboard focus to the main action of the step now shown – the clicked button is gone.</summary>
    private void FocusStepAction()
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (Step2Card.IsVisible)
                (Step2NextButton.IsVisible ? Step2NextButton : Step2Button).Focus();
            else if (Step3Card.IsVisible)
                TestNumberBox.Focus();
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void Links_Click(object sender, RoutedEventArgs e)
    {
        if (Environment.ProcessPath is { } exe)
            ProtocolRegistration.Register(Settings, exe);
        ProtocolRegistration.OpenDefaultAppsSettings();

        _linksSettingsOpened = true;
        RefreshSetup();
    }

    private void SkipLinks_Click(object sender, RoutedEventArgs e)
    {
        Settings.SetupLinksSkipped = true;
        _viewStep = null;
        RefreshAll();
        FocusStepAction();
    }

    private void CompleteSetup()
    {
        Settings.SetupCompleted = true;
        _viewStep = null;
        _justCompleted = true;
        RefreshAll();
    }

    // ---------- Test call (setup step 3) ----------

    private void SkipTest_Click(object sender, RoutedEventArgs e) => CompleteSetup();

    private void TestWorked_Click(object sender, RoutedEventArgs e)
    {
        TestNumberBox.Clear();
        CompleteSetup();
    }

    private void TestFailed_Click(object sender, RoutedEventArgs e)
    {
        var sent = PhoneNumberNormalizer.Normalize(TestNumberBox.Text, NormalizationOptions.From(Settings));
        TestHelpNumber.Text = Loc.T("Home.TestFailedNumber", sent);
        ShowTestPanels(confirm: false, help: true);
        RefreshPhoneCard(); // vendor hint moves into the help panel
    }

    private void TestRetry_Click(object sender, RoutedEventArgs e) => Call_Click(TestCallButton, e);

    private void NumberSettings_Click(object sender, RoutedEventArgs e) => MainWindow.ShowSingle(AppPage.Dialing, "Numbers");

    private void ShowTestPanels(bool confirm, bool help)
    {
        TestConfirmPanel.Visibility = confirm ? Visibility.Visible : Visibility.Collapsed;
        TestHelpPanel.Visibility = help ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------- Dialing (test call and everyday use) ----------

    private void Hotkey_Click(object sender, RoutedEventArgs e) => MainWindow.ShowSingle(AppPage.Dialing, "Hotkey");

    private void DialBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Call_Click(sender == TestNumberBox ? TestCallButton : DialCallButton, e);
    }

    private async void Call_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var box = button == TestCallButton ? TestNumberBox : DialNumberBox;
        if (App.Services is null || string.IsNullOrWhiteSpace(box.Text))
        {
            box.Focus();
            return;
        }

        var isTest = button == TestCallButton;
        if (isTest)
            ShowTestPanels(confirm: false, help: false);

        button.IsEnabled = false;
        var identity = string.IsNullOrEmpty(Settings.DefaultIdentityUri) ? null : Settings.DefaultIdentityUri;
        var success = await App.Services.Calls.DialAsync(box.Text, identity);
        button.IsEnabled = true;

        if (!success)
            return; // the error is shown as a notification

        if (isTest)
        {
            // Keep the number: it is needed for "try again"
            var sent = PhoneNumberNormalizer.Normalize(box.Text, NormalizationOptions.From(Settings));
            TestConfirmText.Text = Loc.T("Home.TestQuestion", sent);
            ShowTestPanels(confirm: true, help: false);
            RefreshPhoneCard();
        }
        else
        {
            box.Clear();
        }
    }
}
