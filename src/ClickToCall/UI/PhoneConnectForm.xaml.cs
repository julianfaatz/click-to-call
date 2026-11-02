using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClickToCall.Core;
using ClickToCall.Core.Phones;
using ClickToCall.Services;

namespace ClickToCall.UI;

/// <summary>
/// Connecting a phone: its address (typed or found by the network scan), and only if needed
/// the manufacturer and the login. Saves the phone in the settings once it answers.
/// </summary>
public partial class PhoneConnectForm : UserControl
{
    private bool _autoScanDone;

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
        ScanPanel.PhoneChosen += phone => ConnectTo(phone.Address);
    }

    private static AppSettings Settings => SettingsStore.Current;

    public bool IsOpen => Visibility == Visibility.Visible;

    public void Open()
    {
        HostBox.Text = Settings.PhoneHost;
        UserBox.Text = Settings.Username;
        PasswordBox.Password = Settings.Password;
        CredentialsPanel.Visibility = Settings.Username.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        // The manufacturer is recognized automatically; changing it is offered once a phone is connected
        VendorBox.SelectedIndex = 0;
        VendorPanel.Visibility = Settings.IsPhoneConfigured ? Visibility.Visible : Visibility.Collapsed;
        CancelConnectButton.Visibility = Settings.IsPhoneConfigured ? Visibility.Visible : Visibility.Collapsed;
        SetMessage("", StatusText.Kind.Neutral);
        Visibility = Visibility.Visible;

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

    public void Close()
    {
        ScanPanel.Cancel();
        Visibility = Visibility.Collapsed;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
        Cancelled?.Invoke();
    }

    private void SetMessage(string text, StatusText.Kind kind)
    {
        StatusText.Set(ConnectMessage, text, kind);
        ConnectMessage.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FormBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Connect_Click(sender, e);
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        ScanButton.IsEnabled = false;
        var phones = await ScanPanel.ScanAsync();
        ScanButton.IsEnabled = true;
        ScanButtonText.Text = Loc.T("Home.ScanAgain");

        // Exactly one phone and nothing typed yet: it is almost certainly the right one
        if (phones.Count == 1 && HostBox.Text.Trim().Length == 0)
            HostBox.Text = phones[0].Address;
    }

    private void ConnectTo(string host)
    {
        HostBox.Text = host;
        Connect_Click(ScanPanel, new RoutedEventArgs());
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (!DoConnectButton.IsEnabled)
            return; // already connecting (Enter in a text box, a scan result)

        var host = HostBox.Text.Trim();
        if (host.Length == 0)
        {
            SetMessage(Loc.T("Home.HostRequired"), StatusText.Kind.Error);
            HostBox.Focus();
            return;
        }

        var user = CredentialsPanel.IsVisible ? UserBox.Text.Trim() : "";
        var password = CredentialsPanel.IsVisible ? PasswordBox.Password : "";

        DoConnectButton.IsEnabled = false;
        SetMessage(Loc.T("Phone.Testing"), StatusText.Kind.Neutral);
        try
        {
            var result = await PhoneConnector.ConnectAsync(VendorFor(host), host, user, password, Settings.UseHttps && host == Settings.PhoneHost);
            switch (result.Status)
            {
                case ConnectStatus.Connected:
                    PhoneSetup.Save(result, host, user, password);
                    Close();
                    Connected?.Invoke(result);
                    break;

                case ConnectStatus.UnknownVendor:
                    VendorPanel.Visibility = Visibility.Visible;
                    SetMessage(Loc.T("Home.VendorUnknown"), StatusText.Kind.Error);
                    VendorBox.Focus();
                    VendorBox.IsDropDownOpen = true;
                    break;

                case ConnectStatus.NeedsCredentials:
                    SetMessage("", StatusText.Kind.Neutral);
                    CredentialsPanel.Visibility = Visibility.Visible;
                    if (UserBox.Text.Length == 0)
                        UserBox.Text = "admin";
                    PasswordBox.Focus();
                    break;

                case ConnectStatus.WrongCredentials:
                    CredentialsPanel.Visibility = Visibility.Visible;
                    SetMessage(Loc.T("Error.Unauthorized"), StatusText.Kind.Error);
                    PasswordBox.Focus();
                    PasswordBox.SelectAll();
                    break;

                default:
                    SetMessage(result.Error?.UserMessage ?? Loc.T("Error.Unreachable"), StatusText.Kind.Error);
                    break;
            }
        }
        finally
        {
            DoConnectButton.IsEnabled = true;
        }
    }

    /// <summary>The manufacturer to connect with, or null to recognize it from the phone.</summary>
    private PhoneVendor? VendorFor(string host)
    {
        if (VendorPanel.IsVisible && (VendorBox.SelectedItem as VendorOption)?.Vendor is { } chosen)
            return chosen;
        if (ScanPanel.FoundPhones.FirstOrDefault(p => p.Address == host) is { } found)
            return found.Vendor;
        return PhoneSetup.KnownVendor(host);
    }
}
