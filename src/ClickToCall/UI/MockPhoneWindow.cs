#if DEBUG
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClickToCall.Core;

namespace ClickToCall.UI;

/// <summary>Development only: shows what the <see cref="MockPhone"/> is doing and lets you switch it "off".</summary>
public sealed class MockPhoneWindow : Window
{
    private readonly TextBlock _display;
    private readonly ListBox _log;

    private MockPhoneWindow()
    {
        Title = "Mock-Telefon";
        Width = 360;
        Height = 440;
        ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 24;
        Top = area.Top + 24;

        var root = new DockPanel { Margin = new Thickness(16) };

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(new TextBlock { Text = $"{MockPhone.Model} (Mock)", FontSize = 16, FontWeight = FontWeights.SemiBold });
        header.Children.Add(Hint($"IP-Adresse: {MockPhone.Address}"));
        header.Children.Add(Hint(MockPhone.RequiresLogin
            ? $"Login: {MockPhone.Username} / {MockPhone.Password}"
            : "Kein Login (mit --mock-login testen)"));
        if (MockPhone.Vendor == PhoneVendor.Yealink)
            header.Children.Add(Hint("Wählt immer – die IP-Freigabe eines echten Yealink wird nicht simuliert"));
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        _display = new TextBlock
        {
            Text = "Bereit",
            FontSize = 22,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xF7, 0xC5)),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var screen = new Border
        {
            Margin = new Thickness(0, 0, 0, 12),
            Padding = new Thickness(16, 18, 16, 18),
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x2A, 0x22)),
            Child = _display,
        };
        DockPanel.SetDock(screen, Dock.Top);
        root.Children.Add(screen);

        var offline = new CheckBox { Content = "Telefon ausgeschaltet (nicht erreichbar)", Margin = new Thickness(0, 0, 0, 12) };
        offline.Checked += (_, _) => { MockPhone.Offline = true; _display.Text = "— aus —"; };
        offline.Unchecked += (_, _) => { MockPhone.Offline = false; _display.Text = "Bereit"; };
        DockPanel.SetDock(offline, Dock.Top);
        root.Children.Add(offline);

        if (MockPhone.Vendor == PhoneVendor.Snom)
        {
            var hiddenTags = new CheckBox { Content = "Versteckte Sicherheits-Tags an (blockiert das Wählen)", Margin = new Thickness(0, 0, 0, 12) };
            hiddenTags.Checked += (_, _) => MockPhone.HiddenTags = true;
            hiddenTags.Unchecked += (_, _) => MockPhone.HiddenTags = false;
            DockPanel.SetDock(hiddenTags, Dock.Top);
            root.Children.Add(hiddenTags);
        }

        _log = new ListBox { FontSize = 12 };
        root.Children.Add(_log);

        Content = root;

        MockPhone.Activity += OnActivity;
        Closed += (_, _) => MockPhone.Activity -= OnActivity;
    }

    public static void ShowWindow() => new MockPhoneWindow().Show();

    private static TextBlock Hint(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Opacity = 0.7,
    };

    private void OnActivity(MockPhoneEvent e) => Dispatcher.BeginInvoke(() =>
    {
        if (e.Display is not null)
            _display.Text = e.Display;

        _log.Items.Insert(0, $"{e.Time:HH:mm:ss}  {e.Text}");
        while (_log.Items.Count > 100)
            _log.Items.RemoveAt(_log.Items.Count - 1);
    });
}
#endif
