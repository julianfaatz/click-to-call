using System.Windows;
using System.Windows.Threading;
using ClickToCallForSnom.Core;

namespace ClickToCallForSnom;

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

        Services = new AppServices();
        Services.Start();

        _instance.ArgumentsReceived += args => Dispatcher.InvokeAsync(() => Services.HandleArguments(args, forwarded: true));
        _instance.StartListening();

        Services.HandleArguments(_args, forwarded: false);
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
