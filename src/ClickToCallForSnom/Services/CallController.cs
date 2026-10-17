using ClickToCallForSnom.Core;
using ClickToCallForSnom.UI;

namespace ClickToCallForSnom.Services;

/// <summary>Central place for everything that leads to a call: links, the hotkey, the tray menu, redial.</summary>
public sealed class CallController
{
    private ToastWindow? _callToast;

    /// <summary>Raised when the phone is not configured yet and the user tried to call.</summary>
    public event EventHandler? SetupRequired;

    private static AppSettings Settings => SettingsStore.Current;

    public void HandleLink(string uri)
    {
        var target = CallUriParser.ExtractTarget(uri);
        Log.Info($"Link received: {uri}");

        if (!EnsureConfigured())
            return;

        if (Settings.ConfirmLinkCalls)
            ShowDialWindow(target, fromLink: true);
        else
            _ = DialAsync(target, NullIfEmpty(Settings.DefaultIdentityUri));
    }

    public async Task HandleHotkeyAsync()
    {
        if (!EnsureConfigured())
            return;

        var text = PhoneNumberNormalizer.ExtractFromText(await SelectionGrabber.GetSelectedTextAsync());
        if (text.Length == 0 && Settings.HotkeyUseClipboardFallback)
        {
            var clipboard = SelectionGrabber.GetClipboardText().Trim();
            if (PhoneNumberNormalizer.LooksLikeNumber(clipboard))
                text = clipboard;
        }

        ShowDialWindow(text, fromLink: false);
    }

    public void ShowDialWindow(string prefill = "", bool fromLink = false)
    {
        if (!EnsureConfigured())
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
            using var client = new SnomPhoneClient(PhoneConnection.From(settings));
            await client.DialAsync(number, identityUri);

            if (settingsOverride is null)
            {
                Settings.AddRecentNumber(number);
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
            ShowCallToast(Loc.T("Call.Failed"), ex.UserMessage, ToastKind.Error, TimeSpan.FromSeconds(20),
                new ToastAction(Loc.T("Call.Retry"), () => _ = DialAsync(rawNumber, identityUri, settingsOverride)));
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
            using var client = new SnomPhoneClient(PhoneConnection.From(settings));
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

    private bool EnsureConfigured()
    {
        if (Settings.IsPhoneConfigured)
            return true;

        SetupRequired?.Invoke(this, EventArgs.Empty);
        return false;
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
