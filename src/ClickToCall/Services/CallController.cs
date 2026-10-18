using ClickToCall.Core;
using ClickToCall.UI;

namespace ClickToCall.Services;

/// <summary>Central place for everything that leads to a call: links, the hotkey, the tray menu, redial.</summary>
public sealed class CallController
{
    private ToastWindow? _callToast;
    private string? _pendingNumber;
    private bool _hotkeyBusy;

    /// <summary>Raised when the phone is not configured yet and the user tried to call.</summary>
    public event EventHandler? SetupRequired;

    /// <summary>Number the user wanted to dial before a phone was connected. Returns it once.</summary>
    public string? TakePendingNumber()
    {
        var number = _pendingNumber;
        _pendingNumber = null;
        return number;
    }

    private static AppSettings Settings => SettingsStore.Current;

    public void HandleLink(string uri)
    {
        var target = CallUriParser.ExtractTarget(uri);
        Log.Info($"Link received: {uri}");

        if (!EnsureConfigured(target))
            return;

        // Links come from untrusted sources (web pages, mails). Feature codes like *21*0900...# (call forwarding)
        // and SIP addresses are never sent to the phone without confirmation.
        if (Settings.ConfirmLinkCalls || !IsPlainNumber(target))
            ShowDialWindow(target, fromLink: true);
        else
            _ = DialAsync(target, NullIfEmpty(Settings.DefaultIdentityUri));
    }

    public async Task HandleHotkeyAsync()
    {
        // A second press while the selection is still being copied would back up our own Ctrl+C
        // result as "the user's clipboard" and restore the wrong content
        if (_hotkeyBusy || !EnsureConfigured())
            return;

        _hotkeyBusy = true;
        try
        {
            var selection = await SelectionGrabber.GetSelectedTextAsync();
            var text = PhoneNumberNormalizer.ExtractFromText(selection);

            // Like a link without confirmation – but only for a selection that is exactly one plain number.
            // Anything ambiguous (a whole signature, the clipboard fallback, feature codes) still shows the dial window.
            if (!Settings.ConfirmHotkeyCalls && PhoneNumberNormalizer.IsSingleNumber(selection) && IsPlainNumber(text))
            {
                _ = DialAsync(text, NullIfEmpty(Settings.DefaultIdentityUri));
                return;
            }

            if (text.Length == 0 && Settings.HotkeyUseClipboardFallback)
            {
                var clipboard = SelectionGrabber.GetClipboardText().Trim();
                if (PhoneNumberNormalizer.LooksLikeNumber(clipboard))
                    text = clipboard;
            }

            ShowDialWindow(text, fromLink: false);
        }
        finally
        {
            _hotkeyBusy = false;
        }
    }

    public void ShowDialWindow(string prefill = "", bool fromLink = false)
    {
        if (!EnsureConfigured(prefill))
            return;

        DialWindow.ShowFor(prefill, fromLink, (number, identity) => _ = DialAsync(number, identity));
    }

    public void Redial()
    {
        if (Settings.RecentNumbers.FirstOrDefault() is { } last)
            _ = DialAsync(last, NullIfEmpty(Settings.DefaultIdentityUri));
    }

    /// <summary>Normalizes the number and sends it to the phone.</summary>
    /// <param name="settingsOverride">Unsaved settings, e.g. for the test call in the setup wizard.</param>
    public async Task<bool> DialAsync(string rawNumber, string? identityUri, AppSettings? settingsOverride = null)
    {
        var settings = settingsOverride ?? Settings;
        var number = PhoneNumberNormalizer.Normalize(rawNumber, NormalizationOptions.From(settings));
        if (number.Length == 0)
        {
            ShowCallToast(Loc.T("Call.InvalidTitle"), rawNumber, ToastKind.Error, TimeSpan.FromSeconds(8));
            return false;
        }

        Log.Info($"Dialing {number} (identity: {identityUri ?? "default"})");
        if (settings.ShowCallNotification)
            ShowCallToast(Loc.T("Call.Dialing"), number, ToastKind.Calling, null);

        try
        {
            using var client = PhoneDrivers.CreateClient(PhoneConnection.From(settings));
            await client.DialAsync(number, identityUri);

            if (settingsOverride is null)
            {
                // Stored without the outside line prefix: the list is normalized again on redial,
                // a stored "0" + "030..." would become "+30..." (Greece)
                Settings.AddRecentNumber(PhoneNumberNormalizer.Normalize(rawNumber, NormalizationOptions.From(settings) with { OutsideLinePrefix = "" }));
                SettingsStore.SaveCurrent();
            }

            if (settings.ShowCallNotification)
            {
                ShowCallToast(Loc.T("Call.Dialed"), number, ToastKind.Calling, TimeSpan.FromSeconds(settings.NotificationSeconds),
                    new ToastAction(Loc.T("Call.HangUp"), () => _ = HangUpAsync(settingsOverride), IsPrimary: true));
            }

            return true;
        }
        catch (PhoneException ex)
        {
            Log.Error($"Dialing {number} failed", ex);
            var actions = new List<ToastAction>
            {
                new(Loc.T("Call.Retry"), () => _ = DialAsync(rawNumber, identityUri, settingsOverride), IsPrimary: true),
            };
            if (ex.Kind is PhoneErrorKind.Unauthorized or PhoneErrorKind.Unreachable or PhoneErrorKind.Timeout)
                actions.Add(new ToastAction(Loc.T("Call.CheckPhone"), () => MainWindow.ShowSingle(AppPage.Home)));

            ShowCallToast(Loc.T("Call.Failed"), ex.UserMessage, ToastKind.Error, TimeSpan.FromSeconds(20), [.. actions]);
            return false;
        }
    }

    public async Task HangUpAsync(AppSettings? settingsOverride = null)
    {
        var settings = settingsOverride ?? Settings;
        if (!settings.IsPhoneConfigured)
            return;

        try
        {
            using var client = PhoneDrivers.CreateClient(PhoneConnection.From(settings));
            await client.HangUpAsync();
            Log.Info("Hung up");
        }
        catch (PhoneException ex)
        {
            Log.Error("Hang up failed", ex);
            ShowCallToast(Loc.T("Call.HangUpFailed"), ex.UserMessage, ToastKind.Error, TimeSpan.FromSeconds(10));
        }
    }

    private void ShowCallToast(string title, string message, ToastKind kind, TimeSpan? autoClose, params ToastAction[] actions)
    {
        if (_callToast is { IsVisible: true })
        {
            _callToast.Update(title, message, kind, autoClose, actions);
            return;
        }

        _callToast = ToastWindow.ShowToast(title, message, kind, autoClose, actions);
    }

    private bool EnsureConfigured(string? number = null)
    {
        if (Settings.IsPhoneConfigured)
            return true;

        _pendingNumber = string.IsNullOrWhiteSpace(number) ? null : number;
        SetupRequired?.Invoke(this, EventArgs.Empty);
        return false;
    }

    private static bool IsPlainNumber(string target)
    {
        var number = PhoneNumberNormalizer.Normalize(target, NormalizationOptions.From(Settings));
        return number.Length > 0 && number.TrimStart('+').All(char.IsAsciiDigit);
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
