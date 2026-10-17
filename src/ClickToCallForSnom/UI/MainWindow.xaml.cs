using System.Windows;
using System.Windows.Controls;
using ClickToCallForSnom.Core;
using ClickToCallForSnom.UI.Pages;

namespace ClickToCallForSnom.UI;

public enum AppPage
{
    Home,
    Phone,
    Dialing,
    Hotkey,
    Links,
    General,
}

/// <summary>The app window: start page with the phone status plus all settings. Changes are saved automatically.</summary>
public partial class MainWindow : Window
{
    private static MainWindow? _current;
    private static bool _closedHintShown;

    private readonly Dictionary<AppPage, FrameworkElement> _pages;

    private MainWindow()
    {
        InitializeComponent();
        DataContext = SettingsStore.Current;
        VersionText.Text = $"Version {UpdateChecker.CurrentVersion}";

        _pages = new()
        {
            [AppPage.Home] = new HomePage(),
            [AppPage.Phone] = new PhonePage(),
            [AppPage.Dialing] = new DialingPage(),
            [AppPage.Hotkey] = new HotkeyPage(),
            [AppPage.Links] = new ProtocolsPage(),
            [AppPage.General] = new GeneralPage(),
        };

        AddNavItem(AppPage.Home, "", "Nav.Home");
        AddNavItem(AppPage.Phone, "", "Nav.Phone");
        AddNavItem(AppPage.Dialing, "", "Nav.Dialing");
        AddNavItem(AppPage.Hotkey, "", "Nav.Hotkey");
        AddNavItem(AppPage.Links, "", "Nav.Links");
        AddNavItem(AppPage.General, "", "Nav.General");

        Closed += (_, _) =>
        {
            if (_current != this)
                return;
            _current = null;

            if (!_closedHintShown)
            {
                _closedHintShown = true;
                ToastWindow.ShowToast(Loc.T("Ready.Title"), Loc.T("Ready.Background"), ToastKind.Info, TimeSpan.FromSeconds(6));
            }
        };
    }

    public static void ShowSingle(AppPage? page = null)
    {
        _current ??= new MainWindow();
        _current.Navigate(page ?? _current.CurrentPage ?? AppPage.Home);
        _current.Show();
        if (_current.WindowState == WindowState.Minimized)
            _current.WindowState = WindowState.Normal;
        _current.Activate();
    }

    /// <summary>Rebuilds the window, e.g. after the language changed (texts are resolved at creation).</summary>
    public static void Reload()
    {
        if (_current is not { } old)
            return;

        var page = old.CurrentPage;
        var replacement = new MainWindow
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = old.Left,
            Top = old.Top,
            Width = old.Width,
            Height = old.Height,
            WindowState = old.WindowState,
        };
        _current = replacement;
        replacement.Navigate(page ?? AppPage.Home);
        replacement.Show();
        old.Close();
    }

    private AppPage? CurrentPage => (Nav.SelectedItem as ListBoxItem)?.Tag as AppPage?;

    public void Navigate(AppPage page)
    {
        foreach (ListBoxItem item in Nav.Items)
        {
            if ((AppPage)item.Tag == page)
                Nav.SelectedItem = item;
        }
    }

    private void AddNavItem(AppPage page, string glyph, string textKey)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock
        {
            Text = glyph,
            FontFamily = (System.Windows.Media.FontFamily)FindResource("IconFont"),
            FontSize = 16,
            Width = 20,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        content.Children.Add(new TextBlock { Text = Loc.T(textKey), VerticalAlignment = VerticalAlignment.Center });

        var item = new ListBoxItem { Content = content, Tag = page, Padding = new Thickness(12, 9, 12, 9) };
        System.Windows.Automation.AutomationProperties.SetName(item, Loc.T(textKey));
        Nav.Items.Add(item);
    }

    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CurrentPage is { } page)
        {
            PageHost.Content = _pages[page];
            Scroller.ScrollToTop();
        }
    }
}
