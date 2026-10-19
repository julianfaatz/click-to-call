using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using ClickToCall.Core;

namespace ClickToCall.UI;

/// <summary>
/// Shows the network scan as a radar: a sweep turns and pings go out from the PC in the middle,
/// every device that answers appears as a blip, phones light up with a phone icon and can be clicked.
/// The ring around it fills with the progress.
/// </summary>
public sealed class ScanRadar : Canvas
{
    private const double Size = 168;
    private const double Center = Size / 2;
    private const double Radius = Center - 6;
    private const int TrailSlices = 18;
    private const double SliceDegrees = 4;

    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(ScanRadar), new PropertyMetadata(0.0, (d, _) => ((ScanRadar)d).UpdateArc()));

    private static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };

    private readonly Canvas _sweep = new() { Width = Size, Height = Size, IsHitTestVisible = false };
    private readonly RotateTransform _sweepRotation = new(0, Center, Center);
    private readonly Canvas _pings = new() { Width = Size, Height = Size, IsHitTestVisible = false };
    private readonly Canvas _blips = new() { Width = Size, Height = Size };
    private readonly Path _arc = new() { StrokeThickness = 3, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly DispatcherTimer _pingTimer = new() { Interval = TimeSpan.FromMilliseconds(1300) };
    private readonly HashSet<IPAddress> _shown = [];

    /// <summary>A phone blip was clicked.</summary>
    public event Action<FoundPhone>? PhoneClicked;

    /// <summary>Share of the scan done, 0 to 1. Set it with <see cref="SetProgress"/> to animate.</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public ScanRadar()
    {
        Width = Size;
        Height = Size;
        ClipToBounds = false;

        // Rings and cross hair
        foreach (var fraction in new[] { 1.0, 2.0 / 3, 1.0 / 3 })
            Children.Add(Themed(Circle(Radius * fraction, fill: false), Shape.StrokeProperty, "DividerStrokeColorDefaultBrush"));
        Children.Add(Themed(new Line { X1 = Center, Y1 = Center - Radius, X2 = Center, Y2 = Center + Radius, StrokeThickness = 1, Opacity = 0.5 }, Shape.StrokeProperty, "DividerStrokeColorDefaultBrush"));
        Children.Add(Themed(new Line { X1 = Center - Radius, Y1 = Center, X2 = Center + Radius, Y2 = Center, StrokeThickness = 1, Opacity = 0.5 }, Shape.StrokeProperty, "DividerStrokeColorDefaultBrush"));

        // Sweep: a bright leading edge with a fading trail behind it (WPF has no conic gradient)
        for (var i = 0; i < TrailSlices; i++)
        {
            var slice = new Path
            {
                Data = Wedge(-(i + 1) * SliceDegrees, -i * SliceDegrees),
                Opacity = 0.32 * Math.Pow(1 - (double)i / TrailSlices, 2),
            };
            _sweep.Children.Add(Themed(slice, Shape.FillProperty, "AccentFillColorDefaultBrush"));
        }
        _sweep.Children.Add(Themed(new Line { X1 = Center, Y1 = Center, X2 = Center, Y2 = Center - Radius, StrokeThickness = 1.5, Opacity = 0.8 },
            Shape.StrokeProperty, "AccentFillColorDefaultBrush"));
        _sweep.RenderTransform = _sweepRotation;

        Children.Add(_pings);
        Children.Add(_sweep);
        Children.Add(Themed(_arc, Shape.StrokeProperty, "AccentFillColorDefaultBrush"));
        Children.Add(_blips);

        // The PC in the middle
        var halo = Themed(Circle(9, fill: true), Shape.FillProperty, "AccentFillColorDefaultBrush");
        halo.Opacity = 0.25;
        Children.Add(halo);
        Children.Add(Themed(Circle(4.5, fill: true), Shape.FillProperty, "AccentFillColorDefaultBrush"));

        _pingTimer.Tick += (_, _) => Ping();
        Unloaded += (_, _) => StopMotion();
    }

    /// <summary>Clears the radar and starts sweeping.</summary>
    public void Start()
    {
        _blips.Children.Clear();
        _shown.Clear();
        BeginAnimation(ProgressProperty, null);
        Progress = 0;
        SetProgress(0.02); // the first answers take a moment: show right away that something happens

        _sweep.BeginAnimation(OpacityProperty, null);
        _sweep.Opacity = 1;
        if (!SystemParameters.ClientAreaAnimation)
        {
            _sweep.Visibility = Visibility.Collapsed; // "Show animations in Windows" is off
            return;
        }

        _sweep.Visibility = Visibility.Visible;
        _sweepRotation.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(0, 360, TimeSpan.FromSeconds(2.4)) { RepeatBehavior = RepeatBehavior.Forever });
        Ping();
        _pingTimer.Start();
    }

    /// <summary>Glides to the new progress. Updates arrive in bursts: a bit longer than their ~0.8 s spacing, so it never stops.</summary>
    public void SetProgress(double fraction)
    {
        if (fraction > Progress)
            BeginAnimation(ProgressProperty, new DoubleAnimation(fraction, TimeSpan.FromMilliseconds(900)) { EasingFunction = EaseOut });
    }

    /// <summary>Fills the ring, stops the sweep and keeps the blips.</summary>
    public void Finish()
    {
        _pingTimer.Stop();
        BeginAnimation(ProgressProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(300)) { EasingFunction = EaseOut });

        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(500)) { BeginTime = TimeSpan.FromMilliseconds(200) };
        fade.Completed += (_, _) =>
        {
            if (!_pingTimer.IsEnabled) // not restarted meanwhile
                _sweepRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        };
        _sweep.BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>A device that answered: a small blip, or a highlighted phone.</summary>
    public void AddDevice(IPAddress address, FoundPhone? phone)
    {
        if (!_shown.Add(address))
            return;

        var (x, y) = Position(address);
        var blip = phone is null ? DeviceBlip() : PhoneBlip(phone);
        SetLeft(blip, x - blip.Width / 2);
        SetTop(blip, y - blip.Height / 2);
        _blips.Children.Add(blip);

        // Pop in, devices then settle to a dim dot
        var scale = new ScaleTransform(0, 0);
        blip.RenderTransformOrigin = new Point(0.5, 0.5);
        blip.RenderTransform = scale;
        var pop = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(phone is null ? 350 : 600))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = phone is null ? 0.6 : 1.2 },
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        if (phone is null)
            blip.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.45, TimeSpan.FromSeconds(1.5)) { BeginTime = TimeSpan.FromMilliseconds(400) });
        else
            Ripple(x, y);
    }

    private void StopMotion()
    {
        _pingTimer.Stop();
        _sweepRotation.BeginAnimation(RotateTransform.AngleProperty, null);
    }

    private static FrameworkElement DeviceBlip() =>
        Themed(new Ellipse { Width = 6, Height = 6 }, Shape.FillProperty, "TextFillColorSecondaryBrush");

    private FrameworkElement PhoneBlip(FoundPhone phone)
    {
        var icon = new TextBlock
        {
            Text = "", // phone
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        icon.SetResourceReference(TextBlock.FontFamilyProperty, "IconFont");
        icon.SetResourceReference(TextBlock.ForegroundProperty, "TextOnAccentFillColorPrimaryBrush");

        var blip = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(13),
            Child = icon,
            Cursor = Cursors.Hand,
            ToolTip = phone.DisplayName,
        };
        blip.SetResourceReference(Border.BackgroundProperty, "AccentFillColorDefaultBrush");
        blip.MouseLeftButtonUp += (_, _) => PhoneClicked?.Invoke(phone);
        return blip;
    }

    /// <summary>A ping wave from the PC in the middle.</summary>
    private void Ping() => Wave(Center, Center, Radius, TimeSpan.FromSeconds(1.8), 0.35);

    /// <summary>A short wave around a found phone.</summary>
    private void Ripple(double x, double y) => Wave(x, y, 26, TimeSpan.FromSeconds(0.9), 0.6);

    private void Wave(double x, double y, double radius, TimeSpan duration, double opacity)
    {
        var ring = Themed(new Ellipse { Width = radius * 2, Height = radius * 2, StrokeThickness = 1.5 }, Shape.StrokeProperty, "AccentFillColorDefaultBrush");
        SetLeft(ring, x - radius);
        SetTop(ring, y - radius);
        var scale = new ScaleTransform(0, 0, radius, radius);
        ring.RenderTransform = scale;
        _pings.Children.Add(ring);

        var grow = new DoubleAnimation(0.05, 1, duration) { EasingFunction = EaseOut };
        var fade = new DoubleAnimation(opacity, 0, duration);
        fade.Completed += (_, _) => _pings.Children.Remove(ring);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        ring.BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>A fixed spot per address, spread over the radar, so the picture stays the same on every scan.</summary>
    private static (double X, double Y) Position(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        var hash = (uint)(bytes[3] * 2654435761u ^ bytes[2] * 40503u ^ bytes[1] * 97u);
        var angle = hash % 360 * Math.PI / 180;
        var distance = Radius * (0.28 + hash / 360 % 1000 / 1000.0 * 0.5); // stays clear of the progress ring
        return (Center + distance * Math.Sin(angle), Center - distance * Math.Cos(angle));
    }

    private void UpdateArc()
    {
        var fraction = Math.Clamp(Progress, 0, 1);
        if (fraction >= 0.9999)
        {
            _arc.Data = new EllipseGeometry(new Point(Center, Center), Radius, Radius);
            return;
        }

        var end = PointAt(fraction * 360, Radius);
        var figure = new PathFigure { StartPoint = PointAt(0, Radius), IsClosed = false };
        figure.Segments.Add(new ArcSegment(end, new Size(Radius, Radius), 0, fraction > 0.5, SweepDirection.Clockwise, true));
        _arc.Data = new PathGeometry([figure]);
    }

    /// <summary>A pie slice from the center, angles in degrees clockwise from 12 o'clock.</summary>
    private static Geometry Wedge(double fromDegrees, double toDegrees)
    {
        var figure = new PathFigure { StartPoint = new Point(Center, Center), IsClosed = true };
        figure.Segments.Add(new LineSegment(PointAt(fromDegrees, Radius), false));
        figure.Segments.Add(new ArcSegment(PointAt(toDegrees, Radius), new Size(Radius, Radius), 0, false, SweepDirection.Clockwise, false));
        var geometry = new PathGeometry([figure]);
        geometry.Freeze();
        return geometry;
    }

    private static Point PointAt(double degrees, double radius)
    {
        var radians = degrees * Math.PI / 180;
        return new Point(Center + radius * Math.Sin(radians), Center - radius * Math.Cos(radians));
    }

    private static Ellipse Circle(double radius, bool fill)
    {
        var circle = new Ellipse { Width = radius * 2, Height = radius * 2, StrokeThickness = fill ? 0 : 1 };
        SetLeft(circle, Center - radius);
        SetTop(circle, Center - radius);
        return circle;
    }

    private static T Themed<T>(T element, DependencyProperty property, string resourceKey) where T : FrameworkElement
    {
        element.SetResourceReference(property, resourceKey);
        return element;
    }
}
