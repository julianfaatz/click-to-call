using System.Windows;
using System.Windows.Controls;
using ClickToCallForSnom.Core;

namespace ClickToCallForSnom.UI.Pages;

public partial class PhonePage : UserControl
{
    private bool _updatingIdentities;

    public PhonePage()
    {
        InitializeComponent();
        // Values may have been changed on the start page in the meantime
        Loaded += (_, _) =>
        {
            PasswordBox.Password = Settings.Password;
            RefreshIdentities();
        };
    }

    private static AppSettings Settings => SettingsStore.Current;

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (PasswordBox.Password != Settings.Password)
            Settings.Password = PasswordBox.Password;
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        TestButton.IsEnabled = false;
        StatusText.Set(TestResult, Loc.T("Phone.Testing"), StatusText.Kind.Neutral);
        try
        {
            using var client = new SnomPhoneClient(PhoneConnection.From(Settings));
            var model = await client.TestConnectionAsync();
            Settings.PhoneModel = model;
            StatusText.Set(TestResult, Loc.T("Phone.TestOk", model), StatusText.Kind.Success);
        }
        catch (PhoneException ex)
        {
            StatusText.Set(TestResult, ex.UserMessage, StatusText.Kind.Error);
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    private async void LoadIdentities_Click(object sender, RoutedEventArgs e)
    {
        LoadIdentitiesButton.IsEnabled = false;
        StatusText.Set(IdentityResult, Loc.T("Identity.Loading"), StatusText.Kind.Neutral);
        try
        {
            using var client = new SnomPhoneClient(PhoneConnection.From(Settings));
            var identities = await client.GetIdentitiesAsync();
            if (identities.Count == 0)
            {
                StatusText.Set(IdentityResult, Loc.T("Identity.NoneFound"), StatusText.Kind.Error);
                return;
            }

            Settings.Identities.Clear();
            foreach (var identity in identities)
                Settings.Identities.Add(identity);

            RefreshIdentities();
            StatusText.Set(IdentityResult, Loc.T("Identity.Loaded", identities.Count), StatusText.Kind.Success);
        }
        catch (PhoneException ex)
        {
            StatusText.Set(IdentityResult, ex.UserMessage + " " + Loc.T("Identity.ManualFallback"), StatusText.Kind.Error);
        }
        catch (System.Xml.XmlException)
        {
            StatusText.Set(IdentityResult, Loc.T("Identity.ParseError"), StatusText.Kind.Error);
        }
        finally
        {
            LoadIdentitiesButton.IsEnabled = true;
        }
    }

    private void AddIdentity_Click(object sender, RoutedEventArgs e)
    {
        var uri = ManualIdentityBox.Text.Trim();
        if (uri.StartsWith("sip:", StringComparison.OrdinalIgnoreCase))
            uri = uri[4..];
        if (!uri.Contains('@') || Settings.Identities.Any(i => i.Uri == uri))
            return;

        var index = Settings.Identities.Count == 0 ? 1 : Settings.Identities.Max(i => i.Index) + 1;
        Settings.Identities.Add(new PhoneIdentity(index, "", uri));
        ManualIdentityBox.Clear();
        RefreshIdentities();
    }

    private void RemoveIdentity_Click(object sender, RoutedEventArgs e)
    {
        if (DefaultIdentityBox.SelectedItem is PhoneIdentity { Uri.Length: > 0 } selected)
        {
            Settings.Identities.Remove(selected);
            Settings.DefaultIdentityUri = "";
            RefreshIdentities();
        }
    }

    private void DefaultIdentityBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingIdentities)
            return;

        Settings.DefaultIdentityUri = (DefaultIdentityBox.SelectedItem as PhoneIdentity)?.Uri ?? "";
        RemoveIdentityButton.IsEnabled = Settings.DefaultIdentityUri.Length > 0;
    }

    private void RefreshIdentities()
    {
        _updatingIdentities = true;
        var options = new List<PhoneIdentity> { new(0, Loc.T("Identity.PhoneDefault"), "") };
        options.AddRange(Settings.Identities);
        DefaultIdentityBox.ItemsSource = options;
        DefaultIdentityBox.SelectedItem = options.FirstOrDefault(i => i.Uri == Settings.DefaultIdentityUri) ?? options[0];
        RemoveIdentityButton.IsEnabled = Settings.DefaultIdentityUri.Length > 0;
        _updatingIdentities = false;
    }
}
