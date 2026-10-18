using System.Windows.Interop;

namespace ClickToCall.Core;

/// <summary>Registers a system wide hotkey with RegisterHotKey on a hidden message window.</summary>
public sealed class HotkeyManager : IDisposable
{
    private const int HotkeyId = 0xC0DE;
    private const int ProbeId = 0xC0DF;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly HwndSource _source;
    private Hotkey? _registered;

    public event EventHandler? Pressed;

    public HotkeyManager()
    {
        _source = new HwndSource(new HwndSourceParameters("ClickToCall.Hotkeys")
        {
            ParentWindow = HwndMessage,
            WindowStyle = 0,
        });
        _source.AddHook(WndProc);
    }

    public Hotkey? Registered => _registered;

    public bool Register(Hotkey hotkey)
    {
        Unregister();
        if (!hotkey.IsValid)
            return false;

        if (!NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, (uint)hotkey.Modifiers | NativeMethods.MOD_NOREPEAT, hotkey.VirtualKey))
        {
            Log.Error($"Hotkey {hotkey} is already in use by another application");
            return false;
        }

        _registered = hotkey;
        return true;
    }

    public void Unregister()
    {
        if (_registered is null)
            return;

        NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
        _registered = null;
    }

    /// <summary>Checks whether a hotkey could be registered (i.e. is not used by another app).</summary>
    public bool IsAvailable(Hotkey hotkey)
    {
        if (hotkey == _registered)
            return true;

        if (!NativeMethods.RegisterHotKey(_source.Handle, ProbeId, (uint)hotkey.Modifiers, hotkey.VirtualKey))
            return false;

        NativeMethods.UnregisterHotKey(_source.Handle, ProbeId);
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
