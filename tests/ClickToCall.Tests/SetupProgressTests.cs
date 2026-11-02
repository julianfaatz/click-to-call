using ClickToCall.Core;
using ClickToCall.Services;

namespace ClickToCall.Tests;

public class SetupProgressTests
{
    private static AppSettings Settings(bool phone = false, bool linksSkipped = false, bool completed = false) => new()
    {
        PhoneHost = phone ? "192.168.1.50" : "",
        SetupLinksSkipped = linksSkipped,
        SetupCompleted = completed,
    };

    [Fact]
    public void Starts_with_the_phone()
    {
        var progress = new SetupProgress(Settings(), linksDone: false);

        Assert.Equal(1, progress.NextOpenStep);
        Assert.False(progress.IsDone(1));
        Assert.False(progress.IsReady);
    }

    [Fact]
    public void Continues_with_the_links_once_the_phone_is_connected()
    {
        var progress = new SetupProgress(Settings(phone: true), linksDone: false);

        Assert.Equal(2, progress.NextOpenStep);
        Assert.True(progress.IsDone(1));
    }

    [Fact]
    public void Skipped_links_lead_to_the_test_call_but_do_not_count_as_done()
    {
        var progress = new SetupProgress(Settings(phone: true, linksSkipped: true), linksDone: false);

        Assert.Equal(3, progress.NextOpenStep);
        Assert.True(progress.LinksSkipped);
        Assert.False(progress.IsDone(2));
    }

    [Fact]
    public void Shows_steps_as_done_only_in_order()
    {
        // Links set up before the phone: step 2 must not look done while step 1 is open
        var progress = new SetupProgress(Settings(), linksDone: true);

        Assert.False(progress.IsDone(2));
    }

    [Fact]
    public void Is_ready_after_the_test_call()
    {
        var progress = new SetupProgress(Settings(phone: true, completed: true), linksDone: true);

        Assert.True(progress.IsReady);
        Assert.True(progress.IsDone(3));
    }
}
