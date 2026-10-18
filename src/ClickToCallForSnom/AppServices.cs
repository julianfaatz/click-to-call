using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using ClickToCallForSnom.Core;
using ClickToCallForSnom.Services;
using ClickToCallForSnom.UI;

namespace ClickToCallForSnom;

/// <summary>Owns the long living parts of the app, saves settings automatically and applies them.</summary>
public sealed class AppServices : IDisposable
{
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromHours(12) };
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private bool _updateToastShown;
    private bool _hotkeyRecording;

    public CallController Calls { get; } = new();
    public HotkeyManager Hotkeys { get; } = new();
    public TrayIcon Tray { get; }

    public AppServices()
    {
        Tray = new TrayIcon(this);
        Hotkeys.Pressed += (_, _) => _ = Calls.HandleHotkeyAsync();
        Calls.SetupRequired += (_, _) =>
        {
            MainWindow.ShowSingle(AppPage.Home);
            ToastWindow.ShowToast(Loc.T("Setup.RequiredTitle"), Loc.T("Setup.RequiredText"), ToastKind.Info, TimeSpan.FromSeconds(10));
        };
        SettingsStore.Changed += (_, _) => Tray.Rebuild();

        _updateTimer.Tick += (_, _) => _ = CheckForUpdatesAsync(manual: false);
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SettingsStore.SaveCurrent();
        };
    }

    /// <summary>While the user records a new hotkey in the settings, the global hotkey must not fire.</summary>
    public bool HotkeyRecording
    {
        get => _hotkeyRecording;
        set
        {
            _hotkeyRecording = value;
            RegisterHotkey();
        }
    }

    public void Start()
    {
        var settings = SettingsStore.Current;
        settings.Autostart = Autostart.IsEnabled;
        // Older versions had no SetupCompleted for users who simply started calling
        if (!settings.SetupCompleted && settings.IsPhoneConfigured && settings.RecentNumbers.Count > 0)
            settings.SetupCompleted = true;
        settings.PropertyChanged += Settings_PropertyChanged;
        settings.Identities.CollectionChanged += (_, _) => ScheduleSave();

        RegisterHotkey(notifyConflict: true);
        RegisterProtocols();

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
    public void HandleArguments(string[] args)
    {
        var callUri = args.FirstOrDefault(CallUriParser.IsCallUri);
        if (callUri is not null)
            Calls.HandleLink(callUri);
        else if (args.Contains("--dial"))
            Calls.ShowDialWindow();
        else if (args.Contains("--settings"))
            MainWindow.ShowSingle(AppPage.General);
        else if (!args.Contains("--autostart") || !SettingsStore.Current.IsPhoneConfigured)
            MainWindow.ShowSingle(); // normal start (start menu, desktop) or nothing set up yet
    }

    /// <summary>Every change is saved automatically and applied right away.</summary>
    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppSettings.Hotkey) or nameof(AppSettings.HotkeyEnabled):
                RegisterHotkey();
                break;
            case nameof(AppSettings.HandleTel) or nameof(AppSettings.HandleCallto) or nameof(AppSettings.HandleSip):
                RegisterProtocols();
                break;
            case nameof(AppSettings.Autostart):
                if (Environment.ProcessPath is { } exe)
                    Autostart.Set(SettingsStore.Current.Autostart, exe);
                return; // lives in the registry, not in the settings file
            case nameof(AppSettings.Language):
                Loc.SetLanguage(SettingsStore.Current.Language);
                Tray.Rebuild();
                // Rebuild after the ComboBox that triggered the change has finished
                Application.Current.Dispatcher.BeginInvoke(MainWindow.Reload, DispatcherPriority.Background);
                break;
        }

        ScheduleSave();
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void RegisterHotkey(bool notifyConflict = false)
    {
        var settings = SettingsStore.Current;
        if (!settings.HotkeyEnabled || _hotkeyRecording)
        {
            Hotkeys.Unregister();
            return;
        }

        if (!Hotkeys.Register(settings.Hotkey) && notifyConflict)
        {
            ToastWindow.ShowToast(Loc.T("Hotkey.InUseTitle"), Loc.T("Hotkey.InUseText", settings.Hotkey), ToastKind.Error,
                TimeSpan.FromSeconds(15), new ToastAction(Loc.T("Home.Change"), () => MainWindow.ShowSingle(AppPage.Dialing, "Hotkey")));
        }
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
        actions.Add(new ToastAction(Loc.T("Update.Details"), () => OpenUrl(update.HtmlUrl)));
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
            SettingsStore.SaveCurrent();
            UpdateChecker.RunInstaller(installer);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Log.Error("Update installation failed", ex);
            toast.Update(Loc.T("Update.Failed"), ex.Message, ToastKind.Error, TimeSpan.FromSeconds(15),
                new ToastAction(Loc.T("Update.Details"), () => OpenUrl(update.HtmlUrl)));
        }
    }

    private static void RegisterProtocols()
    {
        if (Environment.ProcessPath is not { } exe)
            return;

        try
        {
            ProtocolRegistration.Register(SettingsStore.Current, exe);
        }
        catch (Exception ex)
        {
            Log.Error("Protocol registration failed", ex);
        }
    }

    private static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    public void Dispose()
    {
        if (_saveTimer.IsEnabled)
            SettingsStore.SaveCurrent();
        _saveTimer.Stop();
        _updateTimer.Stop();
        Hotkeys.Dispose();
        Tray.Dispose();
    }
}
