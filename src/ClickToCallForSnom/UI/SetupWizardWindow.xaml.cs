using System.Windows;
using System.Windows.Controls;
using ClickToCallForSnom.Core;

namespace ClickToCallForSnom.UI;

/// <summary>First run assistant. Reuses the settings pages and saves everything at the end.</summary>
public partial class SetupWizardWindow : Window
{
    private static SetupWizardWindow? _current;

    private readonly AppSettings _working;
    private readonly FrameworkElement[] _pages;
    private int _index;
    private bool _initializing = true;
    private bool _rebuilding;
    private bool _finished;

    private SetupWizardWindow(AppSettings working, int startIndex)
    {
        InitializeComponent();
        _working = working;
        DataContext = _working;
        _pages = [WelcomePage, PhonePage, DialingPage, HotkeyPage, ProtocolsPage, FinishPage];

        LanguageBox.ItemsSource = new Dictionary<string, string>
        {
            [""] = Loc.T("General.LanguageSystem"),
            ["de"] = "Deutsch",
            ["en"] = "English",
        };
        LanguageBox.SelectedValue = _working.Language;

        Closed += (_, _) =>
        {
            if (_current == this)
                _current = null;
            // Cancelled: switch back to the saved language
            if (!_finished && !_rebuilding)
                Loc.SetLanguage(SettingsStore.Current.Language);
        };
        ShowPage(startIndex);
        _initializing = false;
    }

    public static void ShowSingle()
    {
        if (_current is null)
        {
            var working = SettingsStore.Current.Clone();
            working.Autostart = SettingsStore.Current.SetupCompleted ? Autostart.IsEnabled : true;
            _current = new SetupWizardWindow(working, 0);
            _current.Show();
        }

        _current.Activate();
    }

    private void ShowPage(int index)
    {
        _index = index;
        for (var i = 0; i < _pages.Length; i++)
            _pages[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;

        BackButton.IsEnabled = index > 0;
        NextButton.Content = Loc.T(index == _pages.Length - 1 ? "Wizard.Finish" : "Wizard.Next");
        StepText.Text = Loc.T("Wizard.Step", index + 1, _pages.Length);
        Scroller.ScrollToTop();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_pages[_index] == PhonePage && !_working.IsPhoneConfigured)
        {
            MessageBox.Show(this, Loc.T("Wizard.HostRequired"), Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_index < _pages.Length - 1)
        {
            ShowPage(_index + 1);
            return;
        }

        Finish();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_index > 0)
            ShowPage(_index - 1);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Finish()
    {
        try
        {
            _working.SetupCompleted = true;
            App.Services?.SaveAndApply(_working);
            _finished = true;
            Close();
            App.Services?.ShowReadyNotification();
        }
        catch (Exception ex)
        {
            Log.Error("Saving settings failed", ex);
            MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _rebuilding)
            return;

        _working.Language = LanguageBox.SelectedValue as string ?? "";
        Loc.SetLanguage(_working.Language);

        // All texts are resolved when a window is built – rebuild the wizard in the new language
        _rebuilding = true;
        var replacement = new SetupWizardWindow(_working, _index) { Left = Left, Top = Top };
        _current = replacement;
        replacement.Show();
        Close();
    }

    private async void TestCall_Click(object sender, RoutedEventArgs e)
    {
        if (App.Services is null || string.IsNullOrWhiteSpace(TestNumberBox.Text))
            return;

        TestCallButton.IsEnabled = false;
        StatusText.Set(TestCallStatus, Loc.T("Call.Dialing"), StatusText.Kind.Neutral);
        var identity = string.IsNullOrEmpty(_working.DefaultIdentityUri) ? null : _working.DefaultIdentityUri;
        var ok = await App.Services.Calls.DialAsync(TestNumberBox.Text, identity, _working);
        StatusText.Set(TestCallStatus,
            Loc.T(ok ? "Wizard.TestCallOk" : "Wizard.TestCallFailed"),
            ok ? StatusText.Kind.Success : StatusText.Kind.Error);
        TestCallButton.IsEnabled = true;
    }

    private async void HangUp_Click(object sender, RoutedEventArgs e)
    {
        if (App.Services is not null)
            await App.Services.Calls.HangUpAsync(_working);
    }
}
