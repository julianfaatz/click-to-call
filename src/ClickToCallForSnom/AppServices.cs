using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using ClickToCallForSnom.Core;
using ClickToCallForSnom.Services;
using ClickToCallForSnom.UI;

namespace ClickToCallForSnom;

/// <summary>Owns the long living parts of the app and applies settings to them.</summary>
public sealed class AppServices : IDisposable
{
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromHours(12) };
    private bool _updateToastShown;

    public CallController Calls { get; } = new();
    public HotkeyManager Hotkeys { get; } = new();
    public TrayIcon Tray { get; }

    public AppServices()
    {
        Tray = new TrayIcon(this);
        Hotkeys.Pressed += (_, _) => _ = Calls.HandleHotkeyAsync();
        Calls.SetupRequired += (_, _) => SetupWizardWindow.ShowSingle();
        SettingsStore.Changed += (_, _) => Tray.Rebuild();

        _updateTimer.Tick += (_, _) => _ = CheckForUpdatesAsync(manual: false);
    }

    public void Start()
    {
        RegisterHotkey();
        RegisterProtocols(SettingsStore.Current);

        // First update check shortly after start, then periodically
        var first = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        first.Tick += (_, _) =>
        {
            first.Stop();
            _ = CheckForUpdatesAsync(manual: false);
            _updateTimer.Start();
        };
        first.Start();
    }

    /// <summary>Handles a command line, either from the own start or forwarded by a later start.</summary>
    public void HandleArguments(string[] args, bool forwarded)
    {
        var callUri = args.FirstOrDefault(CallUriParser.IsCallUri);
        if (callUri is not null)
        {
            Calls.HandleLink(callUri);
            return;
        }

        if (args.Contains("--settings"))
            SettingsWindow.ShowSingle();
        else if (args.Contains("--dial"))
            Calls.ShowDialWindow();
        else if (!SettingsStore.Current.SetupCompleted)
            SetupWizardWindow.ShowSingle();
        else if (forwarded)
            SettingsWindow.ShowSingle(); // started again from the start menu: show something
        else if (!args.Contains("--autostart"))
            ShowReadyNotification();
    }

    public void SaveAndApply(AppSettings settings)
    {
        var languageChanged = settings.Language != SettingsStore.Current.Language;

        SettingsStore.Save(settings);
        Loc.SetLanguage(settings.Language);

        if (Environment.ProcessPath is { } exe)
            Autostart.Set(settings.Autostart, exe);

        RegisterHotkey();
        RegisterProtocols(settings);

        if (languageChanged)
            Tray.Rebuild();
    }

    public void RegisterHotkey()
    {
        var settings = SettingsStore.Current;
        if (!settings.HotkeyEnabled)
        {
            Hotkeys.Unregister();
            return;
        }

        if (!Hotkeys.Register(settings.Hotkey))
        {
            ToastWindow.ShowToast(Loc.T("Hotkey.InUseTitle"), Loc.T("Hotkey.InUseText", settings.Hotkey), ToastKind.Error,
                TimeSpan.FromSeconds(15), new ToastAction(Loc.T("Tray.Settings"), () => SettingsWindow.ShowSingle(2)));
        }
    }

    public void ShowSetupWizard() => SetupWizardWindow.ShowSingle();

    public void ShowReadyNotification()
    {
        var settings = SettingsStore.Current;
        var text = settings.HotkeyEnabled ? Loc.T("Ready.TextHotkey", settings.Hotkey) : Loc.T("Ready.Text");
        ToastWindow.ShowToast(Loc.T("Ready.Title"), text, ToastKind.Info, TimeSpan.FromSeconds(8));
    }

    public async Task CheckForUpdatesAsync(bool manual)
    {
        if (!manual && !SettingsStore.Current.CheckForUpdates)
            return;

        try
        {
            var update = await UpdateChecker.CheckAsync();
            if (update is null)
            {
                if (manual)
                    ToastWindow.ShowToast(Loc.T("Update.UpToDate"), $"Version {UpdateChecker.CurrentVersion}", ToastKind.Success, TimeSpan.FromSeconds(6));
                return;
            }

            if (!manual && (_updateToastShown || SettingsStore.Current.SkippedUpdateVersion == update.Version.ToString()))
                return;

            ShowUpdate(update);
        }
        catch (Exception ex)
        {
            Log.Error("Update check failed", ex);
            if (manual)
                ToastWindow.ShowToast(Loc.T("Update.CheckFailed"), "", ToastKind.Error, TimeSpan.FromSeconds(8));
        }
    }

    public void ShowUpdate(UpdateInfo update)
    {
        _updateToastShown = true;
        var actions = new List<ToastAction>();
        if (update.InstallerUrl is not null)
            actions.Add(new ToastAction(Loc.T("Update.Install"), () => _ = InstallUpdateAsync(update), IsPrimary: true));
        actions.Add(new ToastAction(Loc.T("Update.Details"), () => Process.Start(new ProcessStartInfo(update.HtmlUrl) { UseShellExecute = true })));
        actions.Add(new ToastAction(Loc.T("Update.Skip"), () =>
        {
            SettingsStore.Current.SkippedUpdateVersion = update.Version.ToString();
            SettingsStore.SaveCurrent();
        }));

        ToastWindow.ShowToast(Loc.T("Update.AvailableTitle"), Loc.T("Update.AvailableText", update.Version, UpdateChecker.CurrentVersion),
            ToastKind.Update, null, [.. actions]);
    }

    private async Task InstallUpdateAsync(UpdateInfo update)
    {
        var toast = ToastWindow.ShowToast(Loc.T("Update.Downloading"), $"Version {update.Version}", ToastKind.Update, null);
        try
        {
            var installer = await UpdateChecker.DownloadInstallerAsync(update);
            UpdateChecker.RunInstaller(installer);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Log.Error("Update installation failed", ex);
            toast.Update(Loc.T("Update.Failed"), ex.Message, ToastKind.Error, TimeSpan.FromSeconds(15),
                new ToastAction(Loc.T("Update.Details"), () => Process.Start(new ProcessStartInfo(update.HtmlUrl) { UseShellExecute = true })));
        }
    }

    private static void RegisterProtocols(AppSettings settings)
    {
        if (Environment.ProcessPath is not { } exe)
            return;

        try
        {
            ProtocolRegistration.Register(settings, exe);
        }
        catch (Exception ex)
        {
            Log.Error("Protocol registration failed", ex);
        }
    }

    public void Dispose()
    {
        _updateTimer.Stop();
        Hotkeys.Dispose();
        Tray.Dispose();
    }
}
