using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClickToCall.Core;
using ClickToCall.Services;

namespace ClickToCall.UI;

/// <summary>Step indicator of the guided setup: numbered circles connected by lines, done steps with a check mark.</summary>
public sealed class SetupStepper : Grid
{
    private readonly List<(Border Circle, TextBlock Glyph, TextBlock Label, FrameworkElement Item)> _steps = [];
    private readonly List<Border> _lines = [];

    /// <summary>The user clicked a step (1-based) to go back to it.</summary>
    public event Action<int>? StepClicked;

    public SetupStepper()
    {
        for (var i = 0; i < SetupProgress.StepCount; i++)
        {
            var isLast = i == SetupProgress.StepCount - 1;
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (!isLast)
                ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var glyph = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
            };
            var circle = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(2), Child = glyph };
            var label = new TextBlock { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };
            var item = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Transparent };
            item.Children.Add(circle);
            item.Children.Add(label);

            var step = i + 1;
            item.MouseLeftButtonUp += (_, _) => StepClicked?.Invoke(step);
            SetColumn(item, i * 2);
            Children.Add(item);
            _steps.Add((circle, glyph, label, item));

            if (!isLast)
            {
                var line = new Border { Height = 2, Margin = new Thickness(16, 0, 16, 0), CornerRadius = new CornerRadius(1), VerticalAlignment = VerticalAlignment.Center };
                SetColumn(line, i * 2 + 1);
                Children.Add(line);
                _lines.Add(line);
            }
        }
    }

    /// <param name="current">The step shown below the indicator.</param>
    public void Update(SetupProgress progress, int current)
    {
        var success = Brush("SystemFillColorSuccessBrush");
        var muted = Brush("TextFillColorSecondaryBrush");

        for (var i = 0; i < _steps.Count; i++)
        {
            var step = i + 1;
            var (circle, glyph, label, item) = _steps[i];
            var done = progress.IsDone(step);
            var isCurrent = step == current;

            if (done)
            {
                circle.Background = success;
                circle.BorderBrush = success;
                glyph.Text = ""; // check mark
                glyph.FontFamily = (FontFamily)FindResource("IconFont");
                glyph.Foreground = Brush("TextOnAccentFillColorPrimaryBrush");
            }
            else
            {
                circle.Background = Brushes.Transparent;
                circle.BorderBrush = isCurrent ? Brush("AccentFillColorDefaultBrush") : Brush("ControlStrongStrokeColorDefaultBrush");
                glyph.Text = step.ToString();
                glyph.ClearValue(TextBlock.FontFamilyProperty);
                glyph.Foreground = isCurrent ? Brush("AccentTextFillColorPrimaryBrush") : muted;
            }

            label.FontWeight = isCurrent ? FontWeights.SemiBold : FontWeights.Normal;
            label.Foreground = isCurrent || done ? Brush("TextFillColorPrimaryBrush") : muted;
            label.Text = Label(step, progress);

            // Steps can only be done in order
            var reachable = step <= progress.NextOpenStep;
            item.Cursor = reachable && !isCurrent ? Cursors.Hand : null;
            item.ToolTip = reachable && !isCurrent ? Loc.T("Home.StepGoTo") : null;

            if (i < _lines.Count)
                _lines[i].Background = done ? success : Brush("DividerStrokeColorDefaultBrush");
        }
    }

    private static string Label(int step, SetupProgress progress) => step switch
    {
        1 => Loc.T("Home.StepPhone"),
        2 => Loc.T("Home.StepLinksShort") + (progress.LinksSkipped ? " " + Loc.T("Home.Skipped") : ""),
        _ => Loc.T("Home.StepTest"),
    };

    private Brush Brush(string key) => (Brush)FindResource(key);
}
