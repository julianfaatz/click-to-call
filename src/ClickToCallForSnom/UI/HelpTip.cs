using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ClickToCallForSnom.UI;

/// <summary>Small (?) icon that explains a setting on hover or click: <c>&lt;ui:HelpTip Help="..." /&gt;</c>.</summary>
public sealed class HelpTip : TextBlock
{
    public static readonly DependencyProperty HelpProperty = DependencyProperty.Register(
        nameof(Help), typeof(string), typeof(HelpTip), new PropertyMetadata("", (d, e) => ((HelpTip)d).UpdateToolTip((string)e.NewValue)));

    public string Help
    {
        get => (string)GetValue(HelpProperty);
        set => SetValue(HelpProperty, value);
    }

    public HelpTip()
    {
        Text = "";
        FontSize = 14;
        Margin = new Thickness(6, 0, 0, 0);
        VerticalAlignment = VerticalAlignment.Center;
        Opacity = 0.6;
        Cursor = Cursors.Help;
        Focusable = false;
        SetResourceReference(FontFamilyProperty, "IconFont");

        ToolTipService.SetInitialShowDelay(this, 150);
        ToolTipService.SetShowDuration(this, 60_000);

        MouseEnter += (_, _) => Opacity = 1;
        MouseLeave += (_, _) => Opacity = 0.6;
        MouseLeftButtonUp += (_, _) =>
        {
            if (ToolTip is ToolTip tip)
                tip.IsOpen = !tip.IsOpen;
        };
    }

    private void UpdateToolTip(string text)
    {
        ToolTip = new ToolTip
        {
            MaxWidth = 340,
            Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontFamily = SystemFonts.MessageFontFamily },
            Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
        };
    }
}

/// <summary>A label followed by a (?) help icon.</summary>
public sealed class LabelWithHelp : StackPanel
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(LabelWithHelp), new PropertyMetadata("", (d, e) => ((LabelWithHelp)d)._label.Text = (string)e.NewValue));

    public static readonly DependencyProperty HelpProperty = DependencyProperty.Register(
        nameof(Help), typeof(string), typeof(LabelWithHelp), new PropertyMetadata("", (d, e) =>
        {
            var self = (LabelWithHelp)d;
            self._tip.Help = (string)e.NewValue;
            self._tip.Visibility = string.IsNullOrEmpty((string)e.NewValue) ? Visibility.Collapsed : Visibility.Visible;
        }));

    private readonly TextBlock _label = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly HelpTip _tip = new() { Visibility = Visibility.Collapsed };

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Help { get => (string)GetValue(HelpProperty); set => SetValue(HelpProperty, value); }

    public LabelWithHelp()
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;
        Children.Add(_label);
        Children.Add(_tip);
    }
}
