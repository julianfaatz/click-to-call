using System.Windows.Controls;

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
            target.SetResourceReference(TextBlock.ForegroundProperty,
                kind == Kind.Success ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush");
    }
}
