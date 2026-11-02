using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
            if (App.Services is not null)
                App.Services.Calls.HotkeyPractice = HotkeyPractice;
        };
        Unloaded += (_, _) =>
        {
            Settings.PropertyChanged -= Settings_PropertyChanged;
            SettingsStore.Changed -= SettingsStore_Changed;
            if (_window is not null)
                _window.Activated -= Window_Activated;
            _window = null;
            _statusCts?.Cancel();
            if (App.Services?.Calls.HotkeyPractice == HotkeyPractice)
                App.Services.Calls.HotkeyPractice = null;
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
        TestHintText.Text = hint ?? "";
        TestHint.Visibility = hint is null || TestHelpPanel.IsVisible ? Visibility.Collapsed : Visibility.Visible;
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
    private string? _practiceHint; // what went wrong at the last hotkey practice

    private void GoToStep(int step)
    {
        var next = SetupProgress.Current().NextOpenStep;
        if (step > next)
            return; // steps can only be done in order
        _viewStep = step == next ? null : step;
        _justConnected = false;
        _practiceHint = null;
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
        HotkeyStepCard.Visibility = current == 3 ? Visibility.Visible : Visibility.Collapsed;
        TestCard.Visibility = current == 4 ? Visibility.Visible : Visibility.Collapsed;

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

        RefreshHotkeyStep();

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
            else if (HotkeyStepCard.IsVisible)
                (HotkeyNextButton.IsVisible ? HotkeyNextButton : (Control)PracticeBox).Focus();
            else if (TestCard.IsVisible)
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

    // ---------- Practicing the hotkey (setup step 3) ----------

    /// <summary>The number in the practice text. Never dialed: the hotkey is taken over while this step is shown.</summary>
    private const string PracticeNumber = "030 12345678";

    private void RefreshHotkeyStep()
    {
        var tried = Settings.SetupHotkeyTried;
        ShowKeys(HotkeyKeys, Settings.Hotkey);

        HotkeySuccess.Visibility = tried ? Visibility.Visible : Visibility.Collapsed;
        HotkeyNextButton.Visibility = tried ? Visibility.Visible : Visibility.Collapsed;
        HotkeySkipButton.Visibility = tried ? Visibility.Collapsed : Visibility.Visible;
        var practiceNumber = PhoneNumberNormalizer.Normalize(PracticeNumber, NormalizationOptions.From(Settings));
        HotkeySuccessText.Text = Loc.T(Settings.ConfirmHotkeyCalls ? "Home.StepHotkeySuccessConfirm" : "Home.StepHotkeySuccessDirect", practiceNumber);

        // Off or taken by another application: practicing makes no sense, say why and offer the fix
        var registered = App.Services?.Hotkeys.IsRegistered(HotkeyAction.Dial) ?? true;
        string? problem = tried ? null : _practiceHint, button = null;
        if (!Settings.HotkeyEnabled)
            (problem, button) = (Loc.T("Home.StepHotkeyOff"), Loc.T("Home.TurnOn"));
        else if (!registered)
            (problem, button) = (Loc.T("Hotkey.InUseText", Settings.Hotkey), Loc.T("Home.Change"));
        HotkeyProblem.Visibility = problem is null ? Visibility.Collapsed : Visibility.Visible;
        HotkeyProblemText.Text = problem ?? "";
        HotkeyProblemButton.Content = button;
        HotkeyProblemButton.Visibility = button is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>The key combination as keyboard keys: [Ctrl] + [Alt] + [W].</summary>
    private static void ShowKeys(Panel target, Hotkey hotkey)
    {
        target.Children.Clear();
        foreach (var key in hotkey.Keys())
        {
            if (target.Children.Count > 0)
                target.Children.Add(new TextBlock { Text = "+", Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });

            var cap = new Border
            {
                MinWidth = 30,
                Padding = new Thickness(8, 3, 8, 3),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1, 1, 1, 3),
                Child = new TextBlock { Text = key, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
            };
            cap.SetResourceReference(Border.BackgroundProperty, "ControlFillColorDefaultBrush");
            cap.SetResourceReference(Border.BorderBrushProperty, "ControlStrongStrokeColorDefaultBrush");
            target.Children.Add(cap);
        }
    }

    /// <summary>Gets the selection when the hotkey is pressed while this step is in front – instead of dialing.</summary>
    private bool HotkeyPractice(string selection)
    {
        if (_window?.IsActive != true || !HotkeyStepCard.IsVisible || Settings.SetupHotkeyTried)
            return false;

        var options = NormalizationOptions.From(Settings);
        var found = PhoneNumberNormalizer.ExtractFromText(selection);
        if (found.Length > 0 && PhoneNumberNormalizer.Normalize(found, options) == PhoneNumberNormalizer.Normalize(PracticeNumber, options))
        {
            Settings.SetupHotkeyTried = true;
            _viewStep = 3; // stay here to see it worked, "Next" goes on
            _practiceHint = null;
            RefreshSetup();
            Celebrate(HotkeySuccess);
            _ = Dispatcher.BeginInvoke(() => HotkeyNextButton.Focus(), System.Windows.Threading.DispatcherPriority.Input);
            return true;
        }

        _practiceHint = found.Length == 0
            ? Loc.T("Home.StepHotkeyNothing", Settings.Hotkey)
            : Loc.T("Home.StepHotkeyPartial", found, Settings.Hotkey);
        RefreshSetup();
        Nudge(HotkeyProblem);
        return true;
    }

    private void HotkeyProblem_Click(object sender, RoutedEventArgs e)
    {
        if (!Settings.HotkeyEnabled)
            Settings.HotkeyEnabled = true; // registered right away, the step refreshes itself
        else
            MainWindow.ShowSingle(AppPage.Dialing, "Hotkey");
    }

    private void SkipHotkey_Click(object sender, RoutedEventArgs e)
    {
        Settings.SetupHotkeySkipped = true;
        _viewStep = null;
        _practiceHint = null;
        RefreshAll();
        FocusStepAction();
    }

    /// <summary>A little pop for the success message – unless animations are turned off in Windows.</summary>
    private static void Celebrate(FrameworkElement element)
    {
        if (!SystemParameters.ClientAreaAnimation)
            return;

        var scale = new ScaleTransform(0.9, 0.9);
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = scale;
        var pop = new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(600)) { EasingFunction = new ElasticEase { Oscillations = 2, Springiness = 6 } };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
    }

    /// <summary>A short shake: "not quite" – noticeable also when the same hint is shown again.</summary>
    private static void Nudge(FrameworkElement element)
    {
        if (!SystemParameters.ClientAreaAnimation)
            return;

        var shift = new TranslateTransform();
        element.RenderTransform = shift;
        var shake = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(360) };
        foreach (var (x, ms) in new[] { (-6.0, 60), (6.0, 140), (-3.0, 220), (3.0, 290), (0.0, 360) })
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(x, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms))));
        shift.BeginAnimation(TranslateTransform.XProperty, shake);
    }

    private void CompleteSetup()
    {
        Settings.SetupCompleted = true;
        _viewStep = null;
        _justCompleted = true;
        RefreshAll();
    }

    // ---------- Test call (setup step 4) ----------

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
