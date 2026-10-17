using System.Windows;
using System.Windows.Controls;
using ClickToCallForSnom.Core;

namespace ClickToCallForSnom.UI.Pages;

public partial class ProtocolsPage : UserControl
{
    public ProtocolsPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            RefreshStatus();
            // The user usually comes back from the Windows settings app
            if (Window.GetWindow(this) is { } window)
                window.Activated += (_, _) => RefreshStatus();
        };
    }

    private void OpenDefaultApps_Click(object sender, RoutedEventArgs e)
    {
        // Make sure Windows knows about the currently selected link types before the user picks the app
        if (DataContext is AppSettings settings && Environment.ProcessPath is { } exe)
            ProtocolRegistration.Register(settings, exe);

        ProtocolRegistration.OpenDefaultAppsSettings();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshStatus();

    private void RefreshStatus()
    {
        Show(TelStatus, "tel");
        Show(CalltoStatus, "callto");
        Show(SipStatus, "sip");
    }

    private static void Show(TextBlock target, string scheme)
    {
        var isDefault = ProtocolRegistration.IsDefaultHandler(scheme);
        StatusText.Set(target,
            Loc.T(isDefault ? "Protocols.IsDefault" : "Protocols.NotDefault"),
            isDefault ? StatusText.Kind.Success : StatusText.Kind.Neutral);
    }
}
