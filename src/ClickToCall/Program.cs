using ClickToCall.Core;

namespace ClickToCall;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
#if DEBUG
        // Development: fake phone on localhost with its own settings (see Core/MockPhone.cs)
        if (MockPhone.IsRequested(args))
            MockPhone.Prepare(args);
#endif
        SettingsStore.Load();
        Loc.SetLanguage(SettingsStore.Current.Language);

        // Called by the installer / uninstaller – no UI. Only as the sole argument, so that a crafted
        // link (tel:1" --unregister) from an app that does not escape quotes cannot trigger it.
        if (args is ["--register"])
        {
            if (Environment.ProcessPath is { } exe)
                ProtocolRegistration.Register(SettingsStore.Current, exe);
            return 0;
        }

        if (args is ["--unregister"])
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
