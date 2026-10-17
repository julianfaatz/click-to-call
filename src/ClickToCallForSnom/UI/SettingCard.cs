using System.Windows;
using System.Windows.Controls;

namespace ClickToCallForSnom.UI;

/// <summary>
/// A settings row in the style of the Windows 11 settings app: icon, title, description and a control on the right.
/// Optional <see cref="Footer"/> content is shown below, separated by a divider. The template lives in App.xaml.
/// </summary>
public sealed class SettingCard : ContentControl
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(string), typeof(SettingCard), new PropertyMetadata(""));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(SettingCard), new PropertyMetadata(""));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(SettingCard), new PropertyMetadata(""));

    public static readonly DependencyProperty IconBrushProperty = DependencyProperty.Register(
        nameof(IconBrush), typeof(System.Windows.Media.Brush), typeof(SettingCard), new PropertyMetadata(null));

    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
        nameof(Footer), typeof(object), typeof(SettingCard), new PropertyMetadata(null));

    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public System.Windows.Media.Brush? IconBrush { get => (System.Windows.Media.Brush?)GetValue(IconBrushProperty); set => SetValue(IconBrushProperty, value); }
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    static SettingCard()
    {
        FocusableProperty.OverrideMetadata(typeof(SettingCard), new FrameworkPropertyMetadata(false));
    }
}
