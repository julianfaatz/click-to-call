using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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

        VersionText.Text = $"Click to Call for Snom {UpdateChecker.CurrentVersion}";
        UpdateStatus.Text = Loc.T("General.Installed", UpdateChecker.CurrentVersion);
    }

    private async void CheckNow_Click(object sender, RoutedEventArgs e)
    {
        CheckNowButton.IsEnabled = false;
        StatusText.Set(UpdateStatus, Loc.T("Update.Checking"), StatusText.Kind.Neutral);
        try
        {
            var update = await UpdateChecker.CheckAsync();
            if (update is null)
            {
                StatusText.Set(UpdateStatus, Loc.T("Update.UpToDate"), StatusText.Kind.Success);
                return;
            }

            StatusText.Set(UpdateStatus, Loc.T("Update.Available", update.Version), StatusText.Kind.Success);
            App.Services?.ShowUpdate(update);
        }
        catch (Exception ex)
        {
            Log.Error("Update check failed", ex);
            StatusText.Set(UpdateStatus, Loc.T("Update.CheckFailed"), StatusText.Kind.Error);
        }
        finally
        {
            CheckNowButton.IsEnabled = true;
        }
    }

    private void Log_Click(object sender, RoutedEventArgs e)
    {
        if (File.Exists(Log.FilePath))
            Open(Log.FilePath);
    }

    private void Folder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(SettingsStore.Directory);
        Open(SettingsStore.Directory);
    }

    private void Repo_Click(object sender, RoutedEventArgs e) => Open(UpdateChecker.RepositoryUrl);

    private static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
}
