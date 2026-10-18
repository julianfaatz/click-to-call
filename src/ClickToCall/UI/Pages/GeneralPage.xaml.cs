using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClickToCall.Core;

namespace ClickToCall.UI.Pages;

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

        AboutCard.Title = $"{ProtocolRegistration.RegisteredAppName} {UpdateChecker.CurrentVersion}";

        Loaded += (_, _) =>
        {
            // Identities may have been read on the start page in the meantime
            RefreshIdentities();

            UpdateChecker.Checked += UpdateChecker_Checked;
            ShowLastUpdateCheck();
        };
        Unloaded += (_, _) => UpdateChecker.Checked -= UpdateChecker_Checked;
    }

    private static AppSettings Settings => SettingsStore.Current;

    // ---------- Updates ----------

    private enum UpdateState
    {
        UpToDate,
        Available,
        Checking,
        Failed,
        NotChecked,
    }

    private UpdateInfo? _availableUpdate;

    private void UpdateChecker_Checked() => Dispatcher.InvokeAsync(ShowLastUpdateCheck);

    /// <summary>Shows the result of the last check; checks right away if it is old (the page should never claim a stale "up to date").</summary>
    private void ShowLastUpdateCheck()
    {
        if (UpdateChecker.LastCheck is { } last && DateTime.Now - last.CheckedAt < TimeSpan.FromHours(1))
        {
            _availableUpdate = last.Update;
            SetUpdateState(last.Update is null ? UpdateState.UpToDate : UpdateState.Available, last.CheckedAt);
        }
        else if (Settings.CheckForUpdates && CheckNowButton.IsEnabled)
        {
            _ = CheckForUpdatesAsync(showToast: false);
        }
        else if (CheckNowButton.IsEnabled)
        {
            SetUpdateState(UpdateState.NotChecked);
        }
    }

    private void SetUpdateState(UpdateState state, DateTime? checkedAt = null)
    {
        var (background, iconBrush, icon, title, detail) = state switch
        {
            UpdateState.UpToDate => ("SystemFillColorSuccessBackgroundBrush", "SystemFillColorSuccessBrush", "\uE930",
                Loc.T("Update.UpToDate"), Loc.T("Update.UpToDateDetail", UpdateChecker.CurrentVersion, CheckedText(checkedAt))),
            UpdateState.Available => ("SystemFillColorAttentionBackgroundBrush", "AccentTextFillColorPrimaryBrush", "\uE896",
                Loc.T("Update.Available", _availableUpdate?.Version ?? new Version()), Loc.T("General.Installed", UpdateChecker.CurrentVersion)),
            UpdateState.Checking => ("SubtleFillColorSecondaryBrush", "TextFillColorSecondaryBrush", "\uE895",
                Loc.T("Update.Checking"), Loc.T("General.Installed", UpdateChecker.CurrentVersion)),
            UpdateState.Failed => ("SystemFillColorCautionBackgroundBrush", "SystemFillColorCautionBrush", "\uE7BA",
                Loc.T("Update.CheckFailed"), Loc.T("Update.CheckFailedDetail")),
            _ => ("SubtleFillColorSecondaryBrush", "TextFillColorSecondaryBrush", "\uE946",
                Loc.T("Update.NotChecked"), Loc.T("General.Installed", UpdateChecker.CurrentVersion)),
        };

        UpdateStatusBanner.SetResourceReference(Border.BackgroundProperty, background);
        UpdateStatusIcon.SetResourceReference(TextBlock.ForegroundProperty, iconBrush);
        UpdateStatusIcon.Text = icon;
        UpdateStatusTitle.Text = title;
        UpdateStatusDetail.Text = detail;
        InstallUpdateButton.Visibility = state == UpdateState.Available && _availableUpdate?.InstallerUrl is not null
            ? Visibility.Visible : Visibility.Collapsed;

        // The card's own icon turns green as well: visible without reading
        if (state == UpdateState.UpToDate)
        {
            UpdateCard.Icon = "\uE930";
            UpdateCard.SetResourceReference(SettingCard.IconBrushProperty, "SystemFillColorSuccessBrush");
        }
        else
        {
            UpdateCard.Icon = "\uE895";
            UpdateCard.ClearValue(SettingCard.IconBrushProperty);
        }
    }

    private static string CheckedText(DateTime? checkedAt) =>
        checkedAt is not { } time ? ""
        : time.Date == DateTime.Today ? Loc.T("Update.CheckedToday", time.ToString("HH:mm"))
        : Loc.T("Update.CheckedOn", time.ToString(Loc.Language == "de" ? "dd.MM.yyyy HH:mm" : "yyyy-MM-dd HH:mm"));

    private void CheckNow_Click(object sender, RoutedEventArgs e) => _ = CheckForUpdatesAsync(showToast: true);

    private async Task CheckForUpdatesAsync(bool showToast)
    {
        CheckNowButton.IsEnabled = false;
        SetUpdateState(UpdateState.Checking);
        try
        {
            var update = await UpdateChecker.CheckAsync(); // shows the result via UpdateChecker.Checked
            if (update is not null && showToast)
                App.Services?.ShowUpdate(update);
        }
        catch (Exception ex)
        {
            Log.Error("Update check failed", ex);
            SetUpdateState(UpdateState.Failed);
        }
        finally
        {
            CheckNowButton.IsEnabled = true;
        }
    }

    private void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_availableUpdate is { } update && App.Services is { } services)
        {
            InstallUpdateButton.IsEnabled = false;
            _ = services.InstallUpdateAsync(update);
        }
    }

    // ---------- Identities ----------

    private void RefreshIdentities()
    {
        IdentityList.ItemsSource = Settings.Identities.ToList();
        IdentityList.Visibility = Settings.Identities.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        var canRead = PhoneDrivers.For(Settings.Vendor).CanReadIdentities;
        IdentitiesCard.Description = Settings.Identities.Count > 0
            ? Loc.T("Identity.Count", Settings.Identities.Count)
            : Loc.T(canRead ? "Identity.Intro" : "Identity.IntroManual");
        LoadIdentitiesButton.IsEnabled = Settings.IsPhoneConfigured && canRead;
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

    private void ReportBug_Click(object sender, RoutedEventArgs e) => Open(IssueReport.BugReportUrl(Settings));

    private void Repo_Click(object sender, RoutedEventArgs e) => Open(UpdateChecker.RepositoryUrl);

    private static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
}
