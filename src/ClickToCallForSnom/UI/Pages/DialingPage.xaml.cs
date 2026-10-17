using System.ComponentModel;
using System.Windows.Controls;
using ClickToCallForSnom.Core;

namespace ClickToCallForSnom.UI.Pages;

public partial class DialingPage : UserControl
{
    private AppSettings? _settings;

    public DialingPage()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_settings is not null)
                _settings.PropertyChanged -= Settings_PropertyChanged;

            _settings = DataContext as AppSettings;
            if (_settings is not null)
                _settings.PropertyChanged += Settings_PropertyChanged;

            UpdateTry();
        };
    }

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateTry();

    private void TryInput_TextChanged(object sender, TextChangedEventArgs e) => UpdateTry();

    private void UpdateTry()
    {
        if (_settings is null || TryOutput is null)
            return;

        var result = PhoneNumberNormalizer.Normalize(TryInput.Text, NormalizationOptions.From(_settings));
        TryOutput.Text = result.Length > 0 ? result : "–";
    }
}
