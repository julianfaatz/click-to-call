using ClickToCall.Core;

namespace ClickToCall.Tests;

public class AppSettingsResetTests
{
    private static AppSettings Changed() => new()
    {
        PhoneHost = "192.168.1.50",
        Username = "admin",
        DefaultIdentityUri = "1234@pbx.example.com",
        Language = "en",
        SetupCompleted = true,
        ConfirmLinkCalls = false,
        HotkeyEnabled = false,
        Hotkey = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x44),
        HangUpHotkey = Hotkey.Default, // swapped onto the dial combination
        HotkeyUseClipboardFallback = false,
        HandleSip = false,
        OutsideLinePrefix = "0",
        OutsideLineMinLength = 7,
        NotificationSeconds = 60,
    };

    [Fact]
    public void Resets_the_dialing_settings_to_the_defaults()
    {
        var settings = Changed();

        settings.ResetDialingSettings();

        var defaults = new AppSettings();
        Assert.Equal(defaults.ConfirmLinkCalls, settings.ConfirmLinkCalls);
        Assert.True(settings.HotkeyEnabled);
        Assert.Equal(Hotkey.Default, settings.Hotkey);
        Assert.Equal(Hotkey.DefaultHangUp, settings.HangUpHotkey);
        Assert.Equal(defaults.HotkeyUseClipboardFallback, settings.HotkeyUseClipboardFallback);
        Assert.True(settings.HandleSip);
        Assert.Equal("", settings.OutsideLinePrefix);
        Assert.Equal(defaults.OutsideLineMinLength, settings.OutsideLineMinLength);
        Assert.Equal(defaults.NotificationSeconds, settings.NotificationSeconds);
    }

    [Fact]
    public void Keeps_the_phone_language_and_setup()
    {
        var settings = Changed();

        settings.ResetDialingSettings();

        Assert.Equal("192.168.1.50", settings.PhoneHost);
        Assert.Equal("admin", settings.Username);
        Assert.Equal("1234@pbx.example.com", settings.DefaultIdentityUri);
        Assert.Equal("en", settings.Language);
        Assert.True(settings.SetupCompleted);
    }

    [Fact]
    public void Undo_brings_back_the_previous_settings()
    {
        var settings = Changed();
        var before = settings.Clone();

        settings.ResetDialingSettings();
        settings.CopyDialingSettingsFrom(before);

        Assert.False(settings.HotkeyEnabled);
        Assert.Equal(before.Hotkey, settings.Hotkey);
        Assert.Equal(Hotkey.Default, settings.HangUpHotkey);
        Assert.Equal("0", settings.OutsideLinePrefix);
        Assert.False(settings.HandleSip);
    }
}
