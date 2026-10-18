using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClickToCall.Core;

namespace ClickToCall.UI;

public enum ToastKind
{
    Info,
    Calling,
    Success,
    Error,
    Update,
}

public sealed record ToastAction(string Text, Action OnClick, bool IsPrimary = false, bool CloseAfter = true);

/// <summary>Small notification in the bottom right corner (call status, errors, updates). Does not steal focus.</summary>
public partial class ToastWindow : Window
{
    private static readonly List<ToastWindow> Open = [];
    private readonly DispatcherTimer _closeTimer = new();

    public string CloseText => Loc.T("Common.Close");

    private ToastWindow()
    {
        InitializeComponent();
        _closeTimer.Tick += (_, _) => Close();
        MouseEnter += (_, _) => _closeTimer.Stop();
        MouseLeave += (_, _) => { if (_closeTimer.Interval > TimeSpan.Zero) _closeTimer.Start(); };
        SizeChanged += (_, _) => Reposition();
        Closed += (_, _) =>
        {
            _closeTimer.Stop();
            Open.Remove(this);
            RepositionAll();
        };
    }

    public static ToastWindow ShowToast(string title, string message, ToastKind kind, TimeSpan? autoClose, params ToastAction[] actions)
    {
        var toast = new ToastWindow();
        toast.Update(title, message, kind, autoClose, actions);
        Open.Add(toast);
        toast.Show();
        toast.Reposition();
        return toast;
    }

    public void Update(string title, string message, ToastKind kind, TimeSpan? autoClose, params ToastAction[] actions)
    {
        TitleText.Text = title;
        MessageText.Text = message;
        MessageText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;

        (IconText.Text, IconText.Foreground) = kind switch
        {
            ToastKind.Calling => ("", AccentBrush()),
            ToastKind.Success => ("", new SolidColorBrush(Color.FromRgb(0x2E, 0xA0, 0x43))),
            ToastKind.Error => ("", new SolidColorBrush(Color.FromRgb(0xD1, 0x34, 0x38))),
            ToastKind.Update => ("", AccentBrush()),
            _ => ("", AccentBrush()),
        };

        ActionsPanel.Children.Clear();
        foreach (var action in actions)
        {
            var button = new Button
            {
                Content = action.Text,
                Margin = new Thickness(8, 0, 0, 0),
                MinWidth = 90,
                Focusable = false,
            };
            if (action.IsPrimary && TryFindResource("AccentButtonStyle") is Style accent)
                button.Style = accent;

            button.Click += (_, _) =>
            {
                if (action.CloseAfter)
                    Close();
                action.OnClick();
            };
            ActionsPanel.Children.Add(button);
        }

        ActionsPanel.Visibility = actions.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        _closeTimer.Stop();
        _closeTimer.Interval = autoClose ?? TimeSpan.Zero;
        if (autoClose is not null && !IsMouseOver)
            _closeTimer.Start();
    }

    private Brush AccentBrush() =>
        TryFindResource("AccentTextFillColorPrimaryBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x00, 0x67, 0xC0));

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _closeTimer.Stop();
    }

    private static void RepositionAll()
    {
        foreach (var toast in Open)
            toast.Reposition();
    }

    /// <summary>Stacks the toasts from the bottom right corner of the primary work area upwards.</summary>
    private void Reposition()
    {
        var area = SystemParameters.WorkArea;
        var bottom = area.Bottom;
        foreach (var toast in Open)
        {
            var height = toast.ActualHeight > 0 ? toast.ActualHeight : 140;
            if (toast == this)
            {
                Left = area.Right - (ActualWidth > 0 ? ActualWidth : Width);
                Top = bottom - height;
                return;
            }

            bottom -= height;
        }
    }
}
