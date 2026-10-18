using System.Windows;
using ClickToCall.Core;
using Forms = System.Windows.Forms;

namespace ClickToCall.UI;

/// <summary>Icon in the notification area with the main menu of the app.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly AppServices _services;
    private readonly Forms.NotifyIcon _icon;

    public TrayIcon(AppServices services)
    {
        _services = services;

        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))!.Stream;
        _icon = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize),
            Text = ProtocolRegistration.RegisteredAppName,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
                MainWindow.ShowSingle();
        };

        Rebuild();
    }

    /// <summary>Recreates the menu (language, hotkey text and last number may have changed).</summary>
    public void Rebuild()
    {
        var settings = SettingsStore.Current;
        var menu = new Forms.ContextMenuStrip();

        var dial = new Forms.ToolStripMenuItem(Loc.T("Tray.Dial"), null, (_, _) => _services.Calls.ShowDialWindow())
        {
            Font = new System.Drawing.Font(menu.Font, System.Drawing.FontStyle.Bold),
        };
        if (settings.HotkeyEnabled)
            dial.ShortcutKeyDisplayString = settings.Hotkey.ToString();
        menu.Items.Add(dial);

        var last = settings.RecentNumbers.FirstOrDefault();
        menu.Items.Add(new Forms.ToolStripMenuItem(
            last is null ? Loc.T("Tray.Redial") : Loc.T("Tray.RedialNumber", last), null, (_, _) => _services.Calls.Redial())
        {
            Enabled = last is not null,
        });

        menu.Items.Add(new Forms.ToolStripMenuItem(Loc.T("Call.HangUp"), null, (_, _) => _ = _services.Calls.HangUpAsync())
        {
            Enabled = settings.IsPhoneConfigured,
        });

        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Loc.T("Tray.Open"), null, (_, _) => MainWindow.ShowSingle(AppPage.Home));
        menu.Items.Add(Loc.T("Tray.CheckUpdates"), null, (_, _) => _ = _services.CheckForUpdatesAsync(manual: true));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Loc.T("Tray.Exit"), null, (_, _) => Application.Current.Shutdown());

        var old = _icon.ContextMenuStrip;
        _icon.ContextMenuStrip = menu;
        old?.Dispose();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }
}
