using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClickToCallForSnom.Core;

namespace ClickToCallForSnom.UI.Pages;

/// <summary>
/// Start page: shows the connected phone (or lets the user connect one), guides through the setup
/// and afterwards explains the everyday use.
/// </summary>
public partial class HomePage : UserControl
{
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _statusCts;
    private bool _updatingLines;
    private bool _autoScanDone;
    private Window? _window; // Window.GetWindow() is already null in Unloaded
    private IReadOnlyList<FoundPhone> _foundPhones = [];

    public HomePage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Settings.PropertyChanged += Settings_PropertyChanged;
            SettingsStore.Changed += SettingsStore_Changed;
            RefreshAll();
            if (Settings.IsPhoneConfigured)
                _ = CheckStatusAsync();
            else if (ConnectForm.Visibility != Visibility.Visible)
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
            _scanCts?.Cancel();
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
        var formOpen = ConnectForm.Visibility == Visibility.Visible;
        ChangeButton.Visibility = configured && !formOpen ? Visibility.Visible : Visibility.Collapsed;
        RecheckButton.Visibility = configured && !formOpen ? Visibility.Visible : Visibility.Collapsed;

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
            using var client = PhoneDrivers.CreateClient(PhoneConnection.From(Settings), TimeSpan.FromSeconds(5));
            var model = await client.TestConnectionAsync(ct);
            if (ct.IsCancellationRequested)
                return;

            SetStatus(PhoneState.Online);
            if (model != Settings.PhoneModel)
            {
                Settings.PhoneModel = model;
                RefreshPhoneCard();
            }
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

    // ---------- Connect form ----------

    private void ShowConnectForm_Click(object sender, RoutedEventArgs e) => OpenConnectForm();

    private void OpenConnectForm()
    {
        HostBox.Text = Settings.PhoneHost;
        UserBox.Text = Settings.Username;
        PasswordBox.Password = Settings.Password;
        CredentialsPanel.Visibility = Settings.Username.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        CancelConnectButton.Visibility = Settings.IsPhoneConfigured ? Visibility.Visible : Visibility.Collapsed;
        SetConnectMessage("", StatusText.Kind.Neutral);

        ConnectForm.Visibility = Visibility.Visible;
        RefreshAll();

        Dispatcher.BeginInvoke(() =>
        {
            HostBox.Focus();
            HostBox.SelectAll();
        }, System.Windows.Threading.DispatcherPriority.Input);

        // Most people don't know their phone's IP address: on the first start just look for it
        if (!Settings.IsPhoneConfigured && !_autoScanDone)
        {
            _autoScanDone = true;
            Scan_Click(ScanButton, new RoutedEventArgs());
        }
    }

    private void CloseConnectForm()
    {
        _scanCts?.Cancel();
        ConnectForm.Visibility = Visibility.Collapsed;
        ScanPanel.Visibility = Visibility.Collapsed;
        RefreshAll();
    }

    private void CancelConnect_Click(object sender, RoutedEventArgs e) => CloseConnectForm();

    private void SetConnectMessage(string text, StatusText.Kind kind)
    {
        StatusText.Set(ConnectMessage, text, kind);
        ConnectMessage.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FormBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Connect_Click(sender, e);
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (!DoConnectButton.IsEnabled)
            return; // already connecting (Enter in a text box, a scan result)

        var host = HostBox.Text.Trim();
        if (host.Length == 0)
        {
            SetConnectMessage(Loc.T("Home.HostRequired"), StatusText.Kind.Error);
            HostBox.Focus();
            return;
        }

        var user = CredentialsPanel.IsVisible ? UserBox.Text.Trim() : "";
        var password = CredentialsPanel.IsVisible ? PasswordBox.Password : "";

        DoConnectButton.IsEnabled = false;
        SetConnectMessage(Loc.T("Phone.Testing"), StatusText.Kind.Neutral);
        try
        {
            // The scan already knows the manufacturer of the phones it found
            var vendor = _foundPhones.FirstOrDefault(p => p.Address == host)?.Vendor ?? Settings.Vendor;
            var result = await PhoneConnector.ConnectAsync(vendor, host, user, password, Settings.UseHttps && host == Settings.PhoneHost);
            switch (result.Status)
            {
                case ConnectStatus.Connected:
                    Settings.Vendor = vendor;
                    Settings.PhoneHost = host;
                    Settings.UseHttps = result.UseHttps;
                    Settings.IgnoreCertificateErrors = true;
                    Settings.Username = user;
                    Settings.Password = password;
                    Settings.PhoneModel = result.Model;
                    _viewStep = null; // continue with the next setup step
                    CloseConnectForm();
                    SetStatus(PhoneState.Online);
                    ToastWindow.ShowToast(Loc.T("Home.ConnectedTitle"), Loc.T("Home.ConnectedText", result.Model), ToastKind.Success, TimeSpan.FromSeconds(5));
                    await LoadIdentitiesAsync();

                    // The user clicked a link before the phone was set up: continue with it
                    if (App.Services?.Calls.TakePendingNumber() is { } pending)
                        App.Services.Calls.ShowDialWindow(pending, fromLink: true);
                    break;

                case ConnectStatus.NeedsCredentials:
                    SetConnectMessage("", StatusText.Kind.Neutral);
                    CredentialsPanel.Visibility = Visibility.Visible;
                    if (UserBox.Text.Length == 0)
                        UserBox.Text = "admin";
                    PasswordBox.Focus();
                    break;

                case ConnectStatus.WrongCredentials:
                    CredentialsPanel.Visibility = Visibility.Visible;
                    SetConnectMessage(Loc.T("Error.Unauthorized"), StatusText.Kind.Error);
                    PasswordBox.Focus();
                    PasswordBox.SelectAll();
                    break;

                default:
                    SetConnectMessage(result.Error?.UserMessage ?? Loc.T("Error.Unreachable"), StatusText.Kind.Error);
                    break;
            }
        }
        finally
        {
            DoConnectButton.IsEnabled = true;
        }
    }

    /// <summary>Reads the phone's identities silently – only relevant if there is more than one.</summary>
    private async Task LoadIdentitiesAsync()
    {
        if (!PhoneDrivers.For(Settings.Vendor).CanReadIdentities)
            return;

        try
        {
            using var client = PhoneDrivers.CreateClient(PhoneConnection.From(Settings));
            var identities = await client.GetIdentitiesAsync();
            Settings.Identities.Clear();
            foreach (var identity in identities)
                Settings.Identities.Add(identity);
            if (identities.All(i => i.Uri != Settings.DefaultIdentityUri))
                Settings.DefaultIdentityUri = "";
        }
        catch (Exception ex)
        {
            Log.Error("Could not read identities", ex);
        }

        RefreshLines();
    }

    // ---------- Network scan ----------

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        ScanButton.IsEnabled = false;
        ScanPanel.Visibility = Visibility.Visible;
        ScanProgress.Visibility = Visibility.Visible;
        ScanResults.Children.Clear();
        ScanStatus.Text = Loc.T("Home.Scanning");
        try
        {
            var phones = await PhoneScanner.ScanAsync(ct);
            _foundPhones = phones;
            ScanStatus.Text = phones.Count switch
            {
                0 => Loc.T("Home.ScanNone"),
                1 => Loc.T("Home.ScanFoundOne"),
                _ => Loc.T("Home.ScanFound"),
            };

            foreach (var phone in phones)
                ScanResults.Children.Add(CreateScanResult(phone, highlight: phones.Count == 1));

            // Exactly one phone and nothing typed yet: it is almost certainly the right one
            if (phones.Count == 1 && HostBox.Text.Trim().Length == 0)
                HostBox.Text = phones[0].Address;
        }
        catch (OperationCanceledException)
        {
            ScanPanel.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Log.Error("Network scan failed", ex);
            ScanStatus.Text = Loc.T("Home.ScanNone");
        }
        finally
        {
            ScanProgress.Visibility = Visibility.Collapsed;
            ScanButton.IsEnabled = true;
            ScanButtonText.Text = Loc.T("Home.ScanAgain");
        }
    }

    private Button CreateScanResult(FoundPhone phone, bool highlight)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock
        {
            Text = "",
            FontFamily = (FontFamily)FindResource("IconFont"),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        content.Children.Add(new TextBlock { Text = phone.DisplayName, VerticalAlignment = VerticalAlignment.Center });

        var button = new Button { Content = content, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(12, 6, 12, 6) };
        if (highlight && TryFindResource("AccentButtonStyle") is Style accent)
            button.Style = accent;

        button.Click += (_, _) =>
        {
            HostBox.Text = phone.Address;
            Connect_Click(button, new RoutedEventArgs());
        };
        return button;
    }

    // ---------- Guided setup ----------

    private const int StepCount = 3;

    /// <summary>Step the user explicitly went back to (null = the next open step).</summary>
    private int? _viewStep;
    private bool _justCompleted;
    private bool _linksSettingsOpened;
    private readonly List<(Border Circle, TextBlock Glyph, TextBlock Label, FrameworkElement Item)> _steps = [];
    private readonly List<Border> _stepLines = [];

    private static bool IsReady => Settings.IsPhoneConfigured && (Settings.SetupCompleted || Settings.RecentNumbers.Count > 0);

    private static bool LinksDone => ProtocolRegistration.IsDefaultHandler("tel");

    private static bool IsStepDone(int step) => step switch
    {
        1 => Settings.IsPhoneConfigured,
        2 => LinksDone,
        _ => IsReady,
    };

    /// <summary>The first step that still has to be done.</summary>
    private static int NextOpenStep =>
        !Settings.IsPhoneConfigured ? 1
        : !LinksDone && !Settings.SetupLinksSkipped ? 2
        : 3;

    private static string StepLabel(int step, bool skipped = false) => step switch
    {
        1 => Loc.T("Home.StepPhone"),
        2 => Loc.T("Home.StepLinksShort") + (skipped ? " " + Loc.T("Home.Skipped") : ""),
        _ => Loc.T("Home.StepTest"),
    };

    private void BuildStepper()
    {
        for (var i = 0; i < StepCount; i++)
        {
            Stepper.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (i < StepCount - 1)
                Stepper.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var glyph = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
            };
            var circle = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(2), Child = glyph };
            var label = new TextBlock { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Transparent };
            panel.Children.Add(circle);
            panel.Children.Add(label);

            var step = i + 1;
            panel.MouseLeftButtonUp += (_, _) => GoToStep(step);
            Grid.SetColumn(panel, i * 2);
            Stepper.Children.Add(panel);
            _steps.Add((circle, glyph, label, panel));

            if (i < StepCount - 1)
            {
                var line = new Border { Height = 2, Margin = new Thickness(16, 0, 16, 0), CornerRadius = new CornerRadius(1), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(line, i * 2 + 1);
                Stepper.Children.Add(line);
                _stepLines.Add(line);
            }
        }
    }

    private void GoToStep(int step)
    {
        if (step > NextOpenStep)
            return; // steps can only be done in order
        _viewStep = step == NextOpenStep ? null : step;
        RefreshAll();
    }

    private void RefreshSetup()
    {
        if (_steps.Count == 0)
            BuildStepper();

        var ready = IsReady;
        TitleText.Text = Loc.T(ready ? "Home.Title" : "Home.SetupTitle");
        SubtitleText.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        Stepper.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        ReadyPanel.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        DoneBanner.Visibility = ready && _justCompleted ? Visibility.Visible : Visibility.Collapsed;

        var next = NextOpenStep;
        if (_viewStep > next)
            _viewStep = null;
        var current = ready ? 0 : _viewStep ?? next;

        PhoneCard.Visibility = ready || current == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step1Next.Visibility = current == 1 && Settings.IsPhoneConfigured && ConnectForm.Visibility != Visibility.Visible
            ? Visibility.Visible : Visibility.Collapsed;
        Step2Card.Visibility = current == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Card.Visibility = current == 3 ? Visibility.Visible : Visibility.Collapsed;

        if (!ready)
            RefreshStepper(current, next);

        // Step 2 content
        var linksDone = LinksDone;
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

    private void RefreshStepper(int current, int next)
    {
        var accent = (Brush)FindResource("AccentFillColorDefaultBrush");
        var success = (Brush)FindResource("SystemFillColorSuccessBrush");
        var onAccent = (Brush)FindResource("TextOnAccentFillColorPrimaryBrush");
        var accentText = (Brush)FindResource("AccentTextFillColorPrimaryBrush");
        var muted = (Brush)FindResource("TextFillColorSecondaryBrush");
        var stroke = (Brush)FindResource("ControlStrongStrokeColorDefaultBrush");
        var divider = (Brush)FindResource("DividerStrokeColorDefaultBrush");
        var normal = (Brush)FindResource("TextFillColorPrimaryBrush");

        for (var i = 0; i < _steps.Count; i++)
        {
            var step = i + 1;
            var (circle, glyph, label, item) = _steps[i];
            // Shown as done only in order, so the steps never look like "2 done, 1 open"
            var done = Enumerable.Range(1, step).All(IsStepDone);
            var isCurrent = step == current;

            if (done)
            {
                circle.Background = success;
                circle.BorderBrush = success;
                glyph.Text = ""; // check mark
                glyph.FontFamily = (FontFamily)FindResource("IconFont");
                glyph.FontSize = 14;
                glyph.Foreground = onAccent;
            }
            else
            {
                circle.Background = Brushes.Transparent;
                circle.BorderBrush = isCurrent ? accent : stroke;
                glyph.Text = step.ToString();
                glyph.ClearValue(TextBlock.FontFamilyProperty);
                glyph.FontSize = 14;
                glyph.Foreground = isCurrent ? accentText : muted;
            }

            label.FontWeight = isCurrent ? FontWeights.SemiBold : FontWeights.Normal;
            label.Foreground = isCurrent || done ? normal : muted;
            label.Text = StepLabel(step, skipped: step == 2 && !done && Settings.SetupLinksSkipped);

            var reachable = step <= next;
            item.Cursor = reachable && !isCurrent ? Cursors.Hand : null;
            item.ToolTip = reachable && !isCurrent ? Loc.T("Home.StepGoTo") : null;

            if (i < _stepLines.Count)
                _stepLines[i].Background = done ? success : divider;
        }
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        _viewStep = null;
        RefreshAll();
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
    }

    private void SkipTest_Click(object sender, RoutedEventArgs e) => CompleteSetup();

    private void CompleteSetup()
    {
        Settings.SetupCompleted = true;
        _viewStep = null;
        _justCompleted = true;
        RefreshAll();
    }

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

        button.IsEnabled = false;
        var identity = string.IsNullOrEmpty(Settings.DefaultIdentityUri) ? null : Settings.DefaultIdentityUri;
        var success = await App.Services.Calls.DialAsync(box.Text, identity);
        button.IsEnabled = true;

        if (success)
        {
            box.Clear();
            if (button == TestCallButton)
                CompleteSetup();
        }
    }
}
