using System.Windows.Controls;
using System.Windows.Media;

namespace ClickToCallForSnom.UI;

/// <summary>Colored "✓ ok" / "✗ error" status texts next to buttons.</summary>
public static class StatusText
{
    public enum Kind
    {
        Neutral,
        Success,
        Error,
    }

    public static readonly Brush SuccessBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x2E, 0xA0, 0x43)));
    public static readonly Brush ErrorBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD1, 0x34, 0x38)));

    public static void Set(TextBlock target, string text, Kind kind)
    {
        target.Text = kind switch
        {
            Kind.Success => "✓ " + text,
            Kind.Error => "✗ " + text,
            _ => text,
        };

        if (kind == Kind.Neutral)
            target.ClearValue(TextBlock.ForegroundProperty);
        else
            target.Foreground = kind == Kind.Success ? SuccessBrush : ErrorBrush;
    }

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
