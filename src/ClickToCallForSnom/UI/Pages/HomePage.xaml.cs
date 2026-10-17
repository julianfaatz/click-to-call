using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClickToCallForSnom.Core;

namespace ClickToCallForSnom.UI.Pages;

/// <summary>Start page: shows the connected phone (or lets the user connect one) and the most important shortcuts.</summary>
public partial class HomePage : UserControl
{
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _statusCts;
    private bool _updatingLines;

    public HomePage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Settings.PropertyChanged += Settings_PropertyChanged;
            RefreshAll();
            if (Settings.IsPhoneConfigured)
                _ = CheckStatusAsync();
            else
                OpenConnectForm();

            if (Window.GetWindow(this) is { } window)
                window.Activated += Window_Activated;
        };
        Unloaded += (_, _) =>
        {
            Settings.PropertyChanged -= Settings_PropertyChanged;
            if (Window.GetWindow(this) is { } window)
                window.Activated -= Window_Activated;
            _scanCts?.Cancel();
            _statusCts?.Cancel();
        };
    }

    private static AppSettings Settings => SettingsStore.Current;

    private void Window_Activated(object? sender, EventArgs e) => RefreshLinksCard(); // user may come back from Windows settings

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSettings.Hotkey) or nameof(AppSettings.HotkeyEnabled))
            RefreshHotkeyCard();
    }

    private void RefreshAll()
    {
        RefreshPhoneCard();
        RefreshLinksCard();
        RefreshHotkeyCard();
    }

    // ---------- Phone card ----------

    private void RefreshPhoneCard()
    {
        var configured = Settings.IsPhoneConfigured;

        PhoneTitle.Text = configured
            ? (string.IsNullOrEmpty(Settings.PhoneModel) ? "Snom" : Settings.PhoneModel)
            : Loc.T("Home.NoPhone");
        PhoneSubtitle.Text = configured
            ? $"{Settings.PhoneHost} · {(Settings.UseHttps ? "HTTPS" : "HTTP")}"
            : Loc.T("Home.NoPhoneText");

        PhoneIcon.Foreground = configured ? (Brush)FindResource("AccentTextFillColorPrimaryBrush") : (Brush)FindResource("TextFillColorSecondaryBrush");
        StatusPill.Visibility = configured ? Visibility.Visible : Visibility.Collapsed;
        ConnectButton.Visibility = configured ? Visibility.Collapsed : Visibility.Visible;
        ChangeButton.Visibility = configured ? Visibility.Visible : Visibility.Collapsed;
        RecheckButton.Visibility = configured ? Visibility.Visible : Visibility.Collapsed;
        TestCallButton.IsEnabled = configured;

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
            using var client = new SnomPhoneClient(PhoneConnection.From(Settings), TimeSpan.FromSeconds(5));
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
        ConnectMessage.Text = "";

        ConnectForm.Visibility = Visibility.Visible;
        ConnectButton.Visibility = Visibility.Collapsed;
        ChangeButton.Visibility = Visibility.Collapsed;

        Dispatcher.BeginInvoke(() =>
        {
            HostBox.Focus();
            HostBox.SelectAll();
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void CloseConnectForm()
    {
        _scanCts?.Cancel();
        ConnectForm.Visibility = Visibility.Collapsed;
        ScanPanel.Visibility = Visibility.Collapsed;
        RefreshPhoneCard();
    }

    private void CancelConnect_Click(object sender, RoutedEventArgs e) => CloseConnectForm();

    private void FormBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Connect_Click(sender, e);
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        var host = HostBox.Text.Trim();
        if (host.Length == 0)
        {
            StatusText.Set(ConnectMessage, Loc.T("Home.HostRequired"), StatusText.Kind.Error);
            HostBox.Focus();
            return;
        }

        var user = CredentialsPanel.IsVisible ? UserBox.Text.Trim() : "";
        var password = CredentialsPanel.IsVisible ? PasswordBox.Password : "";

        DoConnectButton.IsEnabled = false;
        StatusText.Set(ConnectMessage, Loc.T("Phone.Testing"), StatusText.Kind.Neutral);
        try
        {
            var result = await PhoneConnector.ConnectAsync(host, user, password, Settings.UseHttps && host == Settings.PhoneHost);
            switch (result.Status)
            {
                case ConnectStatus.Connected:
                    Settings.PhoneHost = host;
                    Settings.UseHttps = result.UseHttps;
                    Settings.IgnoreCertificateErrors = true;
                    Settings.Username = user;
                    Settings.Password = password;
                    Settings.PhoneModel = result.Model;
                    CloseConnectForm();
                    SetStatus(PhoneState.Online);
                    await LoadIdentitiesAsync();
                    break;

                case ConnectStatus.NeedsCredentials:
                    ConnectMessage.Text = "";
                    CredentialsPanel.Visibility = Visibility.Visible;
                    if (UserBox.Text.Length == 0)
                        UserBox.Text = "admin";
                    PasswordBox.Focus();
                    break;

                case ConnectStatus.WrongCredentials:
                    CredentialsPanel.Visibility = Visibility.Visible;
                    StatusText.Set(ConnectMessage, Loc.T("Error.Unauthorized"), StatusText.Kind.Error);
                    PasswordBox.Focus();
                    PasswordBox.SelectAll();
                    break;

                default:
                    StatusText.Set(ConnectMessage, result.Error?.UserMessage ?? Loc.T("Error.Unreachable"), StatusText.Kind.Error);
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
        try
        {
            using var client = new SnomPhoneClient(PhoneConnection.From(Settings));
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
            ScanStatus.Text = phones.Count == 0 ? Loc.T("Home.ScanNone") : Loc.T("Home.ScanFound");
            foreach (var phone in phones)
            {
                var button = new Button { Content = phone.DisplayName, Margin = new Thickness(0, 0, 8, 8) };
                button.Click += (_, _) =>
                {
                    HostBox.Text = phone.Address;
                    Connect_Click(button, new RoutedEventArgs());
                };
                ScanResults.Children.Add(button);
            }
        }
        catch (OperationCanceledException)
        {
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
        }
    }

    // ---------- Quick setup cards ----------

    private void RefreshLinksCard()
    {
        var isDefault = ProtocolRegistration.IsDefaultHandler("tel");
        LinksDescription.Text = Loc.T(isDefault ? "Home.LinksActive" : "Home.LinksInactive");
        LinksButton.Visibility = isDefault ? Visibility.Collapsed : Visibility.Visible;
        LinksOk.Visibility = isDefault ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Links_Click(object sender, RoutedEventArgs e)
    {
        if (Environment.ProcessPath is { } exe)
            ProtocolRegistration.Register(Settings, exe);
        ProtocolRegistration.OpenDefaultAppsSettings();
    }

    private void RefreshHotkeyCard() =>
        HotkeyDescription.Text = Settings.HotkeyEnabled
            ? Loc.T("Home.HotkeyActive", Settings.Hotkey)
            : Loc.T("Home.HotkeyOff");

    private void Hotkey_Click(object sender, RoutedEventArgs e) => MainWindow.ShowSingle(AppPage.Hotkey);

    private void TestNumberBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            TestCall_Click(sender, e);
    }

    private async void TestCall_Click(object sender, RoutedEventArgs e)
    {
        if (App.Services is null || string.IsNullOrWhiteSpace(TestNumberBox.Text))
        {
            TestNumberBox.Focus();
            return;
        }

        TestCallButton.IsEnabled = false;
        var identity = string.IsNullOrEmpty(Settings.DefaultIdentityUri) ? null : Settings.DefaultIdentityUri;
        await App.Services.Calls.DialAsync(TestNumberBox.Text, identity);
        TestCallButton.IsEnabled = true;
    }
}
