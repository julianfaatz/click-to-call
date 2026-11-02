using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClickToCall.Core;
using ClickToCall.Core.Phones;

namespace ClickToCall.UI;

/// <summary>Runs the network scan and shows it: live as a radar, the phones found as tiles next to it.</summary>
public partial class PhoneScanPanel : UserControl
{
    private CancellationTokenSource? _cts;

    /// <summary>The user picked a phone, on the radar or in the list.</summary>
    public event Action<FoundPhone>? PhoneChosen;

    /// <summary>The phones of the last finished scan.</summary>
    public IReadOnlyList<FoundPhone> FoundPhones { get; private set; } = [];

    /// <summary>The user wants to scan again.</summary>
    public event Action? ScanAgainClicked;

    /// <summary>The user wants to type the address instead.</summary>
    public event Action? ManualClicked;

    public bool IsScanning { get; private set; }

    /// <summary>Status of connecting to a phone picked here, below the phones.</summary>
    public TextBlock MessageText => Message;

    public PhoneScanPanel()
    {
        InitializeComponent();
        ScanRadar.PhoneClicked += phone => PhoneChosen?.Invoke(phone);
        Unloaded += (_, _) => Cancel();
    }

    /// <summary>Scans (again) and shows the result. Returns the phones found, null if cancelled.</summary>
    public async Task<IReadOnlyList<FoundPhone>?> ScanAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        IsScanning = true;
        ScanAgainButton.Visibility = Visibility.Collapsed;
        ScanResults.Children.Clear();
        ScanStatus.Text = Loc.T("Home.Scanning");
        ScanDetail.Text = "";
        ScanRadar.Start();

        var networks = new HashSet<string>();
        var devices = 0;
        try
        {
            var progress = new Progress<ScanUpdate>(update =>
            {
                if (ct.IsCancellationRequested)
                    return;

                if (update.Network.Length > 0)
                    networks.Add(update.Network);
                if (update.Device is { } device)
                {
                    devices++;
                    ScanRadar.AddDevice(device, update.Phone);
                    if (update.Phone is { } spotted)
                    {
                        ScanStatus.Text = Loc.T("Home.ScanSpotted", spotted.Model);
                        ScanResults.Children.Add(CreateTile(spotted)); // clickable right away
                    }
                    return;
                }

                ScanRadar.SetProgress(update.Fraction);
                ScanDetail.Text = Loc.T("Home.ScanNetwork", update.Network, (int)(update.Fraction * 100));
            });
            var phones = await PhoneScanner.ScanAsync(progress, ct);
            FoundPhones = phones;
            ScanStatus.Text = phones.Count switch
            {
                0 => Loc.T("Home.ScanNoneTitle"),
                1 => Loc.T("Home.ScanFoundOne"),
                _ => Loc.T("Home.ScanFound"),
            };
            ScanDetail.Text = Loc.T("Home.ScanSummary", networks.Count, devices);

            // Final order; a single phone is the obvious choice: highlight it and let Enter connect
            ScanResults.Children.Clear();
            foreach (var phone in phones)
                ScanResults.Children.Add(CreateTile(phone, highlight: phones.Count == 1));
            if (phones.Count == 1 && IsVisible)
                ScanResults.Children[0].Focus();
            return phones;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Log.Error("Network scan failed", ex);
            FoundPhones = [];
            ScanStatus.Text = Loc.T("Home.ScanNoneTitle");
            ScanDetail.Text = "";
            return [];
        }
        finally
        {
            if (!ct.IsCancellationRequested) // otherwise a new scan already took over the radar
            {
                IsScanning = false;
                ScanAgainButton.Visibility = Visibility.Visible;
                ScanRadar.Finish();
            }
        }
    }

    public void Cancel()
    {
        _cts?.Cancel();
        IsScanning = false;
    }

    private void ScanAgain_Click(object sender, RoutedEventArgs e) => ScanAgainClicked?.Invoke();

    private void Manual_Click(object sender, RoutedEventArgs e) => ManualClicked?.Invoke();

    /// <summary>A phone as a list entry: icon, model, address and a chevron, like a clickable settings card.</summary>
    private Button CreateTile(FoundPhone phone, bool highlight = false)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(Icon("", 20, new Thickness(0, 0, 14, 0))); // phone

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = phone.Model, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        text.Children.Add(new TextBlock { Text = phone.Address, FontSize = 12, Opacity = 0.75 });
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var chevron = Icon("", 12, new Thickness(12, 0, 0, 0)); // chevron right
        Grid.SetColumn(chevron, 2);
        grid.Children.Add(chevron);

        var button = new Button
        {
            Content = grid,
            Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(14, 10, 14, 10),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ToolTip = Loc.T("Home.ScanConnectTip", phone.Model),
        };
        System.Windows.Automation.AutomationProperties.SetName(button, phone.DisplayName);
        if (highlight && TryFindResource("AccentButtonStyle") is Style accent)
            button.Style = accent;

        button.Click += (_, _) => PhoneChosen?.Invoke(phone);
        return button;
    }

    private TextBlock Icon(string glyph, double size, Thickness margin) => new()
    {
        Text = glyph,
        FontSize = size,
        FontFamily = (FontFamily)FindResource("IconFont"),
        Margin = margin,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
