using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ClickToCall.Core;
using ClickToCall.Core.Phones;
using ClickToCall.Services;

namespace ClickToCall.UI;

/// <summary>
/// Connecting a phone, one view at a time: find it in the network (or type its address), then only
/// if needed log in or pick the manufacturer. Saves the phone in the settings once it answers.
/// </summary>
public partial class PhoneConnectForm : UserControl
{
    private enum Stage
    {
        Search,
        Manual,
        Login,
        Vendor,
    }

    private Stage _stage = Stage.Search;
    private Stage _origin = Stage.Search; // where "back" leads from the login or manufacturer view
    private bool _busy;

    // The phone being connected
    private string _host = "";
    private string _model = "";
    private PhoneVendor? _vendor;

    /// <summary>The address the login fields were submitted (or saved) for – they are only sent to that phone.</summary>
    private string _loginHost = "";

    /// <summary>Entry of the manufacturer selection. Vendor null = recognize automatically.</summary>
    private sealed record VendorOption(PhoneVendor? Vendor, string Name);

    /// <summary>The phone answered and is saved in the settings; the form is closed.</summary>
    public event Action<ConnectResult>? Connected;

    /// <summary>The user closed the form without connecting.</summary>
    public event Action? Cancelled;

    public PhoneConnectForm()
    {
        InitializeComponent();
        VendorBox.ItemsSource = new[] { new VendorOption(null, Loc.T("Phone.VendorAuto")) }
            .Concat(PhoneDrivers.All.Select(d => new VendorOption(d.Vendor, d.Name)))
            .ToList();

        foreach (var driver in PhoneDrivers.All)
        {
            var button = new Button { Content = driver.Name, MinWidth = 120, Margin = new Thickness(0, 0, 8, 0) };
            button.Click += (_, _) =>
            {
                _vendor = driver.Vendor;
                _ = ConnectAsync();
            };
            VendorButtons.Children.Add(button);
        }

        SearchView.PhoneChosen += phone =>
        {
            _origin = Stage.Search;
            _host = phone.Address;
            _model = phone.Model;
            _vendor = phone.Vendor;
            _ = ConnectAsync();
        };

        SearchView.ScanAgainClicked += () => Scan_Click(SearchView, new RoutedEventArgs());
        SearchView.ManualClicked += () => ShowManual(nothingFound: false);

        // Coming back to the page: a scan that was stopped by leaving it starts over
        Loaded += (_, _) =>
        {
            if (IsOpen && _stage == Stage.Search && !SearchView.IsScanning && SearchView.FoundPhones.Count == 0)
                _ = ScanAsync();
        };
    }

    private static AppSettings Settings => SettingsStore.Current;

    public bool IsOpen => Visibility == Visibility.Visible;

    public void Open()
    {
        HostBox.Text = Settings.PhoneHost;
        UserBox.Text = Settings.Username;
        PasswordBox.Password = Settings.Password;
        _loginHost = Settings.Username.Length > 0 ? Settings.PhoneHost : "";
        VendorBox.SelectedIndex = 0;
        VendorPanel.Visibility = Settings.IsPhoneConfigured ? Visibility.Visible : Visibility.Collapsed;
        Visibility = Visibility.Visible;

        // Changing a connected phone is mostly a new address or password: start with the address.
        // Most people don't know their phone's IP address though: for a new phone just look for it.
        if (Settings.IsPhoneConfigured)
        {
            ShowManual(nothingFound: false);
        }
        else
        {
            Show(Stage.Search);
            _ = ScanAsync();
        }
    }

    /// <summary>Closes the form without connecting.</summary>
    public void Cancel()
    {
        Close();
        Cancelled?.Invoke();
    }

    private void Close()
    {
        SearchView.Cancel();
        Visibility = Visibility.Collapsed;
    }

    // ---------- Views ----------

    private void Show(Stage stage)
    {
        var changed = stage != _stage;
        _stage = stage;

        SearchView.Visibility = stage == Stage.Search ? Visibility.Visible : Visibility.Collapsed;
        ManualView.Visibility = stage == Stage.Manual ? Visibility.Visible : Visibility.Collapsed;
        LoginView.Visibility = stage == Stage.Login ? Visibility.Visible : Visibility.Collapsed;
        VendorView.Visibility = stage == Stage.Vendor ? Visibility.Visible : Visibility.Collapsed;

        var aboutPhone = stage is Stage.Login or Stage.Vendor;
        TargetBar.Visibility = aboutPhone ? Visibility.Visible : Visibility.Collapsed;
        TargetModel.Text = _model.Length > 0 ? _model : _host;
        TargetAddress.Text = _model.Length > 0 ? _host : "";

        foreach (var message in new[] { SearchView.MessageText, ManualMessage, LoginMessage, VendorMessage })
            message.Visibility = Visibility.Collapsed;

        if (changed)
        {
            EnterAnimation(CurrentView);
            if (aboutPhone)
                EnterAnimation(TargetBar);
        }

        Dispatcher.BeginInvoke(FocusCurrentView, System.Windows.Threading.DispatcherPriority.Input);
    }

    private FrameworkElement CurrentView => _stage switch
    {
        Stage.Manual => ManualView,
        Stage.Login => LoginView,
        Stage.Vendor => VendorView,
        _ => SearchView,
    };

    private TextBlock CurrentMessage => _stage switch
    {
        Stage.Manual => ManualMessage,
        Stage.Login => LoginMessage,
        Stage.Vendor => VendorMessage,
        _ => SearchView.MessageText,
    };

    private void FocusCurrentView()
    {
        switch (_stage)
        {
            case Stage.Manual:
                HostBox.Focus();
                HostBox.SelectAll();
                break;
            case Stage.Login when UserBox.Text.Length == 0:
                UserBox.Focus();
                break;
            case Stage.Login:
                PasswordBox.Focus();
                PasswordBox.SelectAll();
                break;
            case Stage.Vendor:
                VendorButtons.Children[0].Focus();
                break;
        }
    }

    /// <summary>The new view slides in a little, like a page in Windows – unless animations are turned off.</summary>
    private static void EnterAnimation(FrameworkElement element)
    {
        if (!SystemParameters.ClientAreaAnimation)
            return;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(250);
        var slide = new TranslateTransform();
        element.RenderTransform = slide;
        slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(12, 0, duration) { EasingFunction = ease });
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
    }

    private void ShowManual(bool nothingFound)
    {
        ManualTitle.Text = Loc.T(nothingFound ? "Home.ScanNoneTitle" : "Home.ManualTitle");
        Show(Stage.Manual);
    }

    private void ShowLogin()
    {
        // Fields typed for another phone don't belong to this one; "admin" is the factory default
        if (_host != _loginHost)
        {
            UserBox.Text = "admin";
            PasswordBox.Clear();
        }
        Show(Stage.Login);
    }

    private void SetMessage(string text, StatusText.Kind kind)
    {
        var message = CurrentMessage;
        StatusText.Set(message, text, kind);
        message.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_origin == Stage.Manual)
            ShowManual(nothingFound: false);
        else
            Show(Stage.Search);
    }

    // ---------- Search ----------

    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        Show(Stage.Search);
        _ = ScanAsync();
    }

    private async Task ScanAsync()
    {
        var phones = await SearchView.ScanAsync();
        // Nothing to click: go on with the address right away
        if (phones?.Count == 0 && _stage == Stage.Search && IsOpen)
            ShowManual(nothingFound: true);
    }

    // ---------- Address ----------

    private void HostBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            ManualConnect_Click(sender, e);
    }

    private void ManualConnect_Click(object sender, RoutedEventArgs e)
    {
        var host = HostBox.Text.Trim();
        if (host.Length == 0)
        {
            SetMessage(Loc.T("Home.HostRequired"), StatusText.Kind.Error);
            HostBox.Focus();
            return;
        }

        var found = SearchView.FoundPhones.FirstOrDefault(p => p.Address == host);
        _origin = Stage.Manual;
        _host = host;
        _model = found?.Model ?? "";
        _vendor = VendorPanel.IsVisible && (VendorBox.SelectedItem as VendorOption)?.Vendor is { } chosen
            ? chosen
            : found?.Vendor ?? PhoneSetup.KnownVendor(host);
        _ = ConnectAsync();
    }

    // ---------- Login ----------

    private void LoginBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Login_Click(sender, e);
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        _loginHost = _host;
        _ = ConnectAsync();
    }

    // ---------- Connecting ----------

    /// <summary>Connects to <see cref="_host"/> and shows what is still needed: a login, the manufacturer – or an error.</summary>
    private async Task ConnectAsync()
    {
        if (_busy)
            return; // already connecting (Enter in a text box, a double click)

        var host = _host;
        var (user, password) = host == _loginHost ? (UserBox.Text.Trim(), PasswordBox.Password) : ("", "");

        _busy = true;
        Views.IsEnabled = false;
        BackButton.IsEnabled = false;
        SetMessage(Loc.T("Home.ConnectingTo", _model.Length > 0 ? _model : host), StatusText.Kind.Neutral);
        try
        {
            var result = await PhoneConnector.ConnectAsync(_vendor, host, user, password, Settings.UseHttps && host == Settings.PhoneHost);
            if (_model.Length == 0)
                _model = result.Model;

            switch (result.Status)
            {
                case ConnectStatus.Connected:
                    PhoneSetup.Save(result, host, user, password);
                    Close();
                    Connected?.Invoke(result);
                    break;

                case ConnectStatus.UnknownVendor:
                    Show(Stage.Vendor);
                    break;

                case ConnectStatus.NeedsCredentials:
                    ShowLogin();
                    break;

                case ConnectStatus.WrongCredentials:
                    ShowLogin();
                    SetMessage(Loc.T("Error.Unauthorized"), StatusText.Kind.Error);
                    break;

                default:
                    SetMessage(result.Error?.UserMessage ?? Loc.T("Error.Unreachable"), StatusText.Kind.Error);
                    _ = Dispatcher.BeginInvoke(FocusCurrentView, System.Windows.Threading.DispatcherPriority.Input); // was disabled meanwhile
                    break;
            }
        }
        finally
        {
            _busy = false;
            Views.IsEnabled = true;
            BackButton.IsEnabled = true;
        }
    }
}
