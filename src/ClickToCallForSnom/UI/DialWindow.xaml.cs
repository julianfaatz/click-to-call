using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ClickToCallForSnom.Core;

namespace ClickToCallForSnom.UI;

/// <summary>Small "dial this number?" popup. Enter dials, Esc cancels.</summary>
public partial class DialWindow : Window
{
    private static DialWindow? _current;
    private readonly Action<string, string?> _onDial;

    private DialWindow(Action<string, string?> onDial)
    {
        _onDial = onDial;
        InitializeComponent();

        var settings = SettingsStore.Current;
        if (settings.ShowIdentitySelection && settings.Identities.Count > 0)
        {
            var options = new List<PhoneIdentity> { new(0, Loc.T("Identity.PhoneDefault"), "") };
            options.AddRange(settings.Identities);
            IdentityBox.ItemsSource = options;
            IdentityBox.SelectedItem = options.FirstOrDefault(i => i.Uri == settings.DefaultIdentityUri) ?? options[0];
            IdentityPanel.Visibility = Visibility.Visible;
        }

        Closed += (_, _) => { if (_current == this) _current = null; };
        Deactivated += (_, _) => { if (string.IsNullOrWhiteSpace(NumberBox.Text)) Close(); };
    }

    /// <summary>Shows the dial window (or reuses an open one) prefilled with a number.</summary>
    public static void ShowFor(string number, bool fromLink, Action<string, string?> onDial)
    {
        var window = _current ?? new DialWindow(onDial);
        _current = window;

        window.HeaderText.Text = Loc.T(fromLink ? "Dial.TitleLink" : "Dial.Title");
        window.NumberBox.Text = number;
        window.UpdatePreview();

        if (!window.IsVisible)
        {
            window.PlaceNearCursor();
            window.Show();
        }

        window.BringToFront();
    }

    private void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();
        NativeMethods.SetForegroundWindow(new WindowInteropHelper(this).Handle);
        NumberBox.Focus();
        Keyboard.Focus(NumberBox);
        NumberBox.SelectAll();
    }

    private void PlaceNearCursor()
    {
        // Center horizontally on the screen that contains the mouse cursor (screen pixels → WPF units)
        var area = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
        double scale;
        using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
            scale = g.DpiX / 96.0;

        Left = area.Left / scale + (area.Width / scale - Width) / 2;
        Top = area.Top / scale + area.Height / scale * 0.3;
    }

    private void UpdatePreview()
    {
        var input = NumberBox.Text;
        var normalized = PhoneNumberNormalizer.Normalize(input, NormalizationOptions.From(SettingsStore.Current));

        DialButton.IsEnabled = normalized.Length > 0;
        if (normalized.Length == 0 || normalized == input.Trim())
        {
            PreviewText.Visibility = Visibility.Collapsed;
            return;
        }

        PreviewText.Text = Loc.T("Dial.Preview", normalized);
        PreviewText.Visibility = Visibility.Visible;
    }

    private void NumberBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdatePreview();

    private void Dial_Click(object sender, RoutedEventArgs e)
    {
        var number = NumberBox.Text;
        if (PhoneNumberNormalizer.Normalize(number, NormalizationOptions.From(SettingsStore.Current)).Length == 0)
            return;

        var identity = IdentityPanel.Visibility == Visibility.Visible
            ? (IdentityBox.SelectedItem as PhoneIdentity)?.Uri
            : SettingsStore.Current.DefaultIdentityUri;

        Close();
        _onDial(number, string.IsNullOrEmpty(identity) ? null : identity);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
