using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ClickToCall.UI;

/// <summary>Status texts next to buttons: a green check or red error icon in front, like a Windows info bar.</summary>
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
        target.Inlines.Clear();
        target.ClearValue(TextBlock.ForegroundProperty);
        if (kind != Kind.Neutral && text.Length > 0)
        {
            var icon = new TextBlock
            {
                Text = kind == Kind.Success ? "" : "", // filled circle with check / with cross
                FontSize = target.FontSize + 2,
                Margin = new Thickness(0, 0, 8, 0),
            };
            icon.SetResourceReference(TextBlock.FontFamilyProperty, "IconFont");
            icon.SetResourceReference(TextBlock.ForegroundProperty, kind == Kind.Success ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush");
            target.Inlines.Add(new InlineUIContainer(icon) { BaselineAlignment = BaselineAlignment.Center });
        }
        target.Inlines.Add(new Run(text));
    }
}
