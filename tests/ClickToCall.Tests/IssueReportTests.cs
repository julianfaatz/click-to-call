using ClickToCall.Core;

namespace ClickToCall.Tests;

public class IssueReportTests
{
    [Fact]
    public void Prefills_the_bug_report_form()
    {
        var url = IssueReport.BugReportUrl("snom D385", new Version(0, 9, 0), new Version(10, 0, 26200));

        Assert.Equal(
            "https://github.com/julianfaatz/click-to-call/issues/new?template=bug_report.yml"
            + "&phone=snom%20D385&version=0.9.0&windows=Windows%2011%20%28Build%2026200%29",
            url);
    }

    [Fact]
    public void Leaves_out_an_unknown_phone_and_detects_Windows_10()
    {
        var url = IssueReport.BugReportUrl("", new Version(0, 9, 0), new Version(10, 0, 19045));

        Assert.DoesNotContain("phone=", url);
        Assert.Contains("windows=Windows%2010%20%28Build%2019045%29", url);
    }

    [Fact]
    public void Escapes_the_phone_model()
    {
        var url = IssueReport.BugReportUrl("T46U&x=1#a", new Version(1, 0, 0), new Version(10, 0, 22631));

        Assert.Contains("phone=T46U%26x%3D1%23a&", url);
    }
}
