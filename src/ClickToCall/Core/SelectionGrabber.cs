using System.Runtime.InteropServices;
using System.Windows;

namespace ClickToCall.Core;

/// <summary>
/// Reads the text currently selected in the foreground application by simulating Ctrl+C.
/// The previous clipboard content is restored afterwards.
/// </summary>
public static class SelectionGrabber
{
    public static async Task<string> GetSelectedTextAsync()
    {
        await WaitForModifierReleaseAsync();

        var sequenceBefore = NativeMethods.GetClipboardSequenceNumber();
        var backup = Snapshot();

        SendCtrlC();

        // Wait for the application to put the selection on the clipboard
        var changed = false;
        for (var i = 0; i < 25 && !changed; i++)
        {
            await Task.Delay(20);
            changed = NativeMethods.GetClipboardSequenceNumber() != sequenceBefore;
        }

        if (!changed)
            return "";

        await Task.Delay(30);
        var text = Retry(() => Clipboard.ContainsText() ? Clipboard.GetText() : "") ?? "";
        Restore(backup);
        return text;
    }

    public static string GetClipboardText() => Retry(() => Clipboard.ContainsText() ? Clipboard.GetText() : "") ?? "";

    /// <summary>The hotkey modifiers are still held down when we get the hotkey – Ctrl+C would become Ctrl+Alt+C.</summary>
    private static async Task WaitForModifierReleaseAsync()
    {
        int[] keys = [NativeMethods.VK_CONTROL, NativeMethods.VK_MENU, NativeMethods.VK_SHIFT, NativeMethods.VK_LWIN, NativeMethods.VK_RWIN];
        for (var i = 0; i < 100; i++)
        {
            if (keys.All(k => (NativeMethods.GetAsyncKeyState(k) & 0x8000) == 0))
                return;
            await Task.Delay(15);
        }
    }

    private static void SendCtrlC()
    {
        NativeMethods.INPUT[] inputs =
        [
            NativeMethods.KeyInput(NativeMethods.VK_CONTROL, up: false),
            NativeMethods.KeyInput(NativeMethods.VK_C, up: false),
            NativeMethods.KeyInput(NativeMethods.VK_C, up: true),
            NativeMethods.KeyInput(NativeMethods.VK_CONTROL, up: true),
        ];
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static DataObject? Snapshot()
    {
        return Retry(() =>
        {
            var current = Clipboard.GetDataObject();
            if (current is null)
                return null;

            var copy = new DataObject();
            var any = false;
            foreach (var format in current.GetFormats(autoConvert: false))
            {
                try
                {
                    if (current.GetData(format, autoConvert: false) is { } data)
                    {
                        copy.SetData(format, data);
                        any = true;
                    }
                }
                catch
                {
                    // Some formats (e.g. delayed rendering) cannot be copied – skip them
                }
            }

            return any ? copy : null;
        });
    }

    private static void Restore(DataObject? backup)
    {
        Retry(() =>
        {
            if (backup is null)
                Clipboard.Clear();
            else
                Clipboard.SetDataObject(backup, copy: true);
            return true;
        });
    }

    /// <summary>The clipboard is often briefly locked by other processes.</summary>
    private static T? Retry<T>(Func<T> action)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                return action();
            }
            catch (COMException)
            {
                Thread.Sleep(30);
            }
            catch (Exception ex)
            {
                Log.Error("Clipboard access failed", ex);
                return default;
            }
        }

        return default;
    }
}
