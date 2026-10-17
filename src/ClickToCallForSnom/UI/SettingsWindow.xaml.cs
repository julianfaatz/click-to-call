using System.Windows;
using ClickToCallForSnom.Core;

namespace ClickToCallForSnom.UI;

/// <summary>Settings dialog. Edits a copy of the settings that only replaces the current ones on save.</summary>
public partial class SettingsWindow : Window
{
    private static SettingsWindow? _current;
    private readonly AppSettings _working;

    private SettingsWindow()
    {
        InitializeComponent();
        _working = SettingsStore.Current.Clone();
        _working.Autostart = Autostart.IsEnabled;
        DataContext = _working;
        Closed += (_, _) => _current = null;
    }

    public static void ShowSingle(int tab = 0)
    {
        _current ??= new SettingsWindow();
        _current.Tabs.SelectedIndex = tab;
        _current.Show();
        if (_current.WindowState == WindowState.Minimized)
            _current.WindowState = WindowState.Normal;
        _current.Activate();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            App.Services?.SaveAndApply(_working);
            Close();
        }
        catch (Exception ex)
        {
            Log.Error("Saving settings failed", ex);
            MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
