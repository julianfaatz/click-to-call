using System.Windows.Interop;

namespace ClickToCall.Core;

/// <summary>Registers system wide hotkeys with RegisterHotKey on a hidden message window, one per <see cref="HotkeyAction"/>.</summary>
public sealed class HotkeyManager : IDisposable
{
    private const int FirstId = 0xC0DE;
    private const int ProbeId = 0xBEEF;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly HwndSource _source;
    private readonly Dictionary<HotkeyAction, Hotkey> _registered = [];

    public event Action<HotkeyAction>? Pressed;

    public HotkeyManager()
    {
        _source = new HwndSource(new HwndSourceParameters("ClickToCall.Hotkeys")
        {
            ParentWindow = HwndMessage,
            WindowStyle = 0,
        });
        _source.AddHook(WndProc);
    }

    private static int IdFor(HotkeyAction action) => FirstId + (int)action;

    public bool Register(HotkeyAction action, Hotkey hotkey)
    {
        Unregister(action);
        if (!hotkey.IsValid)
            return false;

        if (!NativeMethods.RegisterHotKey(_source.Handle, IdFor(action), (uint)hotkey.Modifiers | NativeMethods.MOD_NOREPEAT, hotkey.VirtualKey))
        {
            Log.Error($"Hotkey {hotkey} ({action}) is already in use by another application");
            return false;
        }

        _registered[action] = hotkey;
        return true;
    }

    public void Unregister(HotkeyAction action)
    {
        if (!_registered.Remove(action))
            return;

        NativeMethods.UnregisterHotKey(_source.Handle, IdFor(action));
    }

    public void UnregisterAll()
    {
        foreach (var action in _registered.Keys.ToList())
            Unregister(action);
    }

    /// <summary>Checks whether a hotkey could be registered (i.e. is not used by another app).</summary>
    public bool IsAvailable(Hotkey hotkey)
    {
        if (_registered.ContainsValue(hotkey))
            return true;

        if (!NativeMethods.RegisterHotKey(_source.Handle, ProbeId, (uint)hotkey.Modifiers, hotkey.VirtualKey))
            return false;

        NativeMethods.UnregisterHotKey(_source.Handle, ProbeId);
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            var action = (HotkeyAction)(wParam.ToInt32() - FirstId);
            if (_registered.ContainsKey(action))
            {
                handled = true;
                Pressed?.Invoke(action);
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
