using ClickToCallForSnom.Core;

namespace ClickToCallForSnom;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        SettingsStore.Load();
        Loc.SetLanguage(SettingsStore.Current.Language);

        // Called by the installer / uninstaller – no UI
        if (args.Contains("--register"))
        {
            if (Environment.ProcessPath is { } exe)
                ProtocolRegistration.Register(SettingsStore.Current, exe);
            return 0;
        }

        if (args.Contains("--unregister"))
        {
            ProtocolRegistration.Unregister();
            if (Environment.ProcessPath is { } exe)
                Autostart.Set(false, exe);
            return 0;
        }

        using var instance = new SingleInstance();
        if (!instance.IsFirstInstance)
        {
            // Already running: hand over e.g. the clicked tel: link and exit
            SingleInstance.SendToFirstInstance(args.Length > 0 ? args : ["--show"]);
            return 0;
        }

        var app = new App(instance, args);
        return app.Run();
    }
}
