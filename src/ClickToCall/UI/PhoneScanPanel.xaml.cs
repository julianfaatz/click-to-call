using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClickToCall.Core;
using ClickToCall.Core.Phones;

namespace ClickToCall.UI;

/// <summary>Runs the network scan and shows it: live as a radar, then the phones found as buttons.</summary>
public partial class PhoneScanPanel : UserControl
{
    private CancellationTokenSource? _cts;

    /// <summary>The user picked a phone, on the radar or below it.</summary>
    public event Action<FoundPhone>? PhoneChosen;

    /// <summary>The phones of the last scan.</summary>
    public IReadOnlyList<FoundPhone> FoundPhones { get; private set; } = [];

    public PhoneScanPanel()
    {
        InitializeComponent();
        ScanRadar.PhoneClicked += phone => PhoneChosen?.Invoke(phone);
        Unloaded += (_, _) => Cancel();
    }

    /// <summary>Scans (again) and shows the result. Returns the phones found – none if cancelled.</summary>
    public async Task<IReadOnlyList<FoundPhone>> ScanAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        Visibility = Visibility.Visible;
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
                        ScanStatus.Text = Loc.T("Home.ScanSpotted", spotted.Model);
                    return;
                }

                ScanRadar.SetProgress(update.Fraction);
                ScanDetail.Text = Loc.T("Home.ScanNetwork", update.Network, (int)(update.Fraction * 100));
            });
            var phones = await PhoneScanner.ScanAsync(progress, ct);
            FoundPhones = phones;
            ScanStatus.Text = phones.Count switch
            {
                0 => Loc.T("Home.ScanNone"),
                1 => Loc.T("Home.ScanFoundOne"),
                _ => Loc.T("Home.ScanFound"),
            };
            ScanDetail.Text = Loc.T("Home.ScanSummary", networks.Count, devices);

            foreach (var phone in phones)
                ScanResults.Children.Add(CreateResultButton(phone, highlight: phones.Count == 1));
            return phones;
        }
        catch (OperationCanceledException)
        {
            Visibility = Visibility.Collapsed;
            return [];
        }
        catch (Exception ex)
        {
            Log.Error("Network scan failed", ex);
            ScanStatus.Text = Loc.T("Home.ScanNone");
            ScanDetail.Text = "";
            return [];
        }
        finally
        {
            if (!ct.IsCancellationRequested) // otherwise a new scan already took over the radar
                ScanRadar.Finish();
        }
    }

    public void Cancel()
    {
        _cts?.Cancel();
        Visibility = Visibility.Collapsed;
    }

    private Button CreateResultButton(FoundPhone phone, bool highlight)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock
        {
            Text = "", // phone
            FontFamily = (FontFamily)FindResource("IconFont"),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        content.Children.Add(new TextBlock { Text = phone.DisplayName, VerticalAlignment = VerticalAlignment.Center });

        var button = new Button { Content = content, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(12, 6, 12, 6) };
        if (highlight && TryFindResource("AccentButtonStyle") is Style accent)
            button.Style = accent;

        button.Click += (_, _) => PhoneChosen?.Invoke(phone);
        return button;
    }
}
