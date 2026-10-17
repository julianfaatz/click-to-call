using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClickToCallForSnom.Core;

namespace ClickToCallForSnom.UI.Pages;

public partial class GeneralPage : UserControl
{
    public GeneralPage()
    {
        InitializeComponent();

        LanguageBox.ItemsSource = new Dictionary<string, string>
        {
            [""] = Loc.T("General.LanguageSystem"),
            ["de"] = "Deutsch",
            ["en"] = "English",
        };

        UpdateCard.Description = Loc.T("General.Installed", UpdateChecker.CurrentVersion);
        AboutCard.Title = $"Click to Call for Snom {UpdateChecker.CurrentVersion}";

        // Identities may have been read on the start page in the meantime
        Loaded += (_, _) => RefreshIdentities();
    }

    private static AppSettings Settings => SettingsStore.Current;

    private async void CheckNow_Click(object sender, RoutedEventArgs e)
    {
        CheckNowButton.IsEnabled = false;
        UpdateCard.Description = Loc.T("Update.Checking");
        try
        {
            var update = await UpdateChecker.CheckAsync();
            if (update is null)
            {
                UpdateCard.Description = "✓ " + Loc.T("Update.UpToDate");
                return;
            }

            UpdateCard.Description = Loc.T("Update.Available", update.Version);
            App.Services?.ShowUpdate(update);
        }
        catch (Exception ex)
        {
            Log.Error("Update check failed", ex);
            UpdateCard.Description = Loc.T("Update.CheckFailed");
        }
        finally
        {
            CheckNowButton.IsEnabled = true;
        }
    }

    // ---------- Identities ----------

    private void RefreshIdentities()
    {
        IdentityList.ItemsSource = Settings.Identities.ToList();
        IdentityList.Visibility = Settings.Identities.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        IdentitiesCard.Description = Settings.Identities.Count > 0
            ? Loc.T("Identity.Count", Settings.Identities.Count)
            : Loc.T("Identity.Intro");
        LoadIdentitiesButton.IsEnabled = Settings.IsPhoneConfigured && PhoneDrivers.For(Settings.Vendor).CanReadIdentities;
    }

    private void ShowIdentityResult(string text, StatusText.Kind kind)
    {
        StatusText.Set(IdentityResult, text, kind);
        IdentityResult.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void LoadIdentities_Click(object sender, RoutedEventArgs e)
    {
        LoadIdentitiesButton.IsEnabled = false;
        ShowIdentityResult(Loc.T("Identity.Loading"), StatusText.Kind.Neutral);
        try
        {
            using var client = PhoneDrivers.CreateClient(PhoneConnection.From(Settings));
            var identities = await client.GetIdentitiesAsync();
            if (identities.Count == 0)
            {
                ShowIdentityResult(Loc.T("Identity.NoneFound"), StatusText.Kind.Error);
                return;
            }

            Settings.Identities.Clear();
            foreach (var identity in identities)
                Settings.Identities.Add(identity);
            if (identities.All(i => i.Uri != Settings.DefaultIdentityUri))
                Settings.DefaultIdentityUri = "";

            ShowIdentityResult(Loc.T("Identity.Loaded", identities.Count), StatusText.Kind.Success);
        }
        catch (PhoneException ex)
        {
            ShowIdentityResult(ex.UserMessage + " " + Loc.T("Identity.ManualFallback"), StatusText.Kind.Error);
        }
        catch (System.Xml.XmlException)
        {
            ShowIdentityResult(Loc.T("Identity.ParseError"), StatusText.Kind.Error);
        }
        finally
        {
            RefreshIdentities();
        }
    }

    private void ManualIdentityBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            AddIdentity_Click(sender, e);
    }

    private void AddIdentity_Click(object sender, RoutedEventArgs e)
    {
        var uri = ManualIdentityBox.Text.Trim();
        if (uri.StartsWith("sip:", StringComparison.OrdinalIgnoreCase))
            uri = uri[4..];
        if (!uri.Contains('@'))
        {
            ShowIdentityResult(Loc.T("Identity.Invalid"), StatusText.Kind.Error);
            ManualIdentityBox.Focus();
            return;
        }

        if (Settings.Identities.All(i => i.Uri != uri))
        {
            var index = Settings.Identities.Count == 0 ? 1 : Settings.Identities.Max(i => i.Index) + 1;
            Settings.Identities.Add(new PhoneIdentity(index, "", uri));
        }

        ManualIdentityBox.Clear();
        ShowIdentityResult("", StatusText.Kind.Neutral);
        RefreshIdentities();
    }

    private void RemoveIdentity_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not PhoneIdentity identity)
            return;

        Settings.Identities.Remove(identity);
        if (Settings.DefaultIdentityUri == identity.Uri)
            Settings.DefaultIdentityUri = "";
        RefreshIdentities();
    }

    // ---------- Troubleshooting ----------

    private void Log_Click(object sender, RoutedEventArgs e)
    {
        if (File.Exists(Log.FilePath))
            Open(Log.FilePath);
        else
            Folder_Click(sender, e);
    }

    private void Folder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(SettingsStore.Directory);
        Open(SettingsStore.Directory);
    }

    private void Repo_Click(object sender, RoutedEventArgs e) => Open(UpdateChecker.RepositoryUrl);

    private static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
}
