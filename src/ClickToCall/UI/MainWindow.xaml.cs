using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ClickToCall.Core;
using ClickToCall.UI.Pages;

namespace ClickToCall.UI;

public enum AppPage
{
    Home,
    Dialing,
    General,
}

/// <summary>A page that can scroll to one of its sections, e.g. "Hotkey" on the dialing page.</summary>
public interface ISectionPage
{
    void ShowSection(string section);
}

/// <summary>The app window: start page with the phone status plus all settings. Changes are saved automatically.</summary>
public partial class MainWindow : Window
{
    private static MainWindow? _current;

    private readonly Dictionary<AppPage, FrameworkElement> _pages;

    private MainWindow()
    {
        InitializeComponent();
        Tagline.Text = Loc.T("App.Tagline");
        DataContext = SettingsStore.Current;

        _pages = new()
        {
            [AppPage.Home] = new HomePage(),
            [AppPage.Dialing] = new DialingPage(),
            [AppPage.General] = new GeneralPage(),
        };

        AddNavItem(AppPage.Home, "", "Nav.Home");
        AddNavItem(AppPage.Dialing, "", "Nav.Dialing");
        AddNavItem(AppPage.General, "", "Nav.General");

        RestoreSize();
        Closing += (_, _) => RememberSize();

        Closed += (_, _) =>
        {
            if (_current != this)
                return;
            _current = null;

            // Explain once that closing the window does not quit the app
            var settings = SettingsStore.Current;
            var showHint = !settings.BackgroundHintShown && settings.IsPhoneConfigured;
            settings.BackgroundHintShown |= showHint;
            SettingsStore.SaveCurrent(); // window size, hint flag
            if (showHint)
                ToastWindow.ShowToast(Loc.T("Ready.Title"), Loc.T("Ready.Background"), ToastKind.Info, TimeSpan.FromSeconds(8));
        };
    }

    /// <param name="section">Optional section of the page to scroll to (see <see cref="ISectionPage"/>).</param>
    public static void ShowSingle(AppPage? page = null, string? section = null)
    {
        _current ??= new MainWindow();
        _current.Navigate(page ?? _current.CurrentPage ?? AppPage.Home);
        _current.Show();
        if (_current.WindowState == WindowState.Minimized)
            _current.WindowState = WindowState.Normal;
        _current.Activate();

        if (section is not null && _current.PageHost.Content is ISectionPage sectionPage)
            _current.Dispatcher.BeginInvoke(() => sectionPage.ShowSection(section), DispatcherPriority.Loaded);
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

    /// <summary>Opens with the last used size, but never larger than the screen.</summary>
    private void RestoreSize()
    {
        var settings = SettingsStore.Current;
        var area = SystemParameters.WorkArea;
        if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
        {
            Width = settings.WindowWidth;
            Height = settings.WindowHeight;
        }

        Width = Math.Max(MinWidth, Math.Min(Width, area.Width - 40));
        Height = Math.Max(MinHeight, Math.Min(Height, area.Height - 40));
        if (settings.WindowMaximized)
            WindowState = WindowState.Maximized;
    }

    private void RememberSize()
    {
        var settings = SettingsStore.Current;
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            settings.WindowWidth = Math.Round(bounds.Width);
            settings.WindowHeight = Math.Round(bounds.Height);
        }
        settings.WindowMaximized = WindowState == WindowState.Maximized;
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
