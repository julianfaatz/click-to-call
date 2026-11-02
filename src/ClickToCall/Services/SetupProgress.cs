using ClickToCall.Core;

namespace ClickToCall.Services;

/// <summary>Where the user is in the guided setup (phone, links, test call). A snapshot: take a new one to refresh.</summary>
public sealed class SetupProgress(AppSettings settings, bool linksDone)
{
    public const int StepCount = 3;

    public static SetupProgress Current() => new(SettingsStore.Current, ProtocolRegistration.IsDefaultHandler("tel"));

    /// <summary>Not "a number was dialed": the phone accepting the request does not mean it dialed (see the test call).</summary>
    public bool IsReady => settings.IsPhoneConfigured && settings.SetupCompleted;

    /// <summary>Click to Call is the Windows default app for tel: links.</summary>
    public bool LinksDone => linksDone;

    public bool LinksSkipped => !linksDone && settings.SetupLinksSkipped;

    /// <summary>The first step that still has to be done.</summary>
    public int NextOpenStep =>
        !settings.IsPhoneConfigured ? 1
        : !linksDone && !settings.SetupLinksSkipped ? 2
        : 3;

    /// <summary>Done, and so are all steps before it – the steps never look like "2 done, 1 open".</summary>
    public bool IsDone(int step) => Enumerable.Range(1, step).All(IsStepDone);

    private bool IsStepDone(int step) => step switch
    {
        1 => settings.IsPhoneConfigured,
        2 => linksDone,
        _ => IsReady,
    };
}
