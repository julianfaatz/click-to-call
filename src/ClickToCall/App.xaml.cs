using System.Windows;
using System.Windows.Threading;
using ClickToCall.Core;

namespace ClickToCall;

public partial class App : Application
{
    private readonly SingleInstance _instance;
    private readonly string[] _args;

    public static AppServices? Services { get; private set; }

    public App(SingleInstance instance, string[] args)
    {
        _instance = instance;
        _args = args;
        InitializeComponent();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            // Fire-and-forget tasks (_ = DialAsync(...)) must not fail silently
            Log.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

#if DEBUG
        if (MockPhone.IsEnabled)
        {
            MockPhone.Start();
            UI.MockPhoneWindow.ShowWindow();
        }
#endif
        Services = new AppServices();
        Services.Start();

        _instance.ArgumentsReceived += args => Dispatcher.InvokeAsync(() => Services.HandleArguments(args));
        _instance.StartListening();

        Services.HandleArguments(_args);
        Log.Info($"Started version {UpdateChecker.CurrentVersion}");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Services?.Dispose();
        base.OnExit(e);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Unhandled exception", e.Exception);
        e.Handled = true;
    }
}
