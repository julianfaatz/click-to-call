using ClickToCall.Core.Network;

namespace ClickToCall.Tests;

public class ProbeResponseTests
{
    [Fact]
    public void Summarizes_a_device_for_the_log()
    {
        var response = new ProbeResponse("http://192.168.1.5/", 401, "embedded", "Digest realm=\"Phone\"", Body: "<title>\n  Login\n</title>");

        Assert.Equal("http://192.168.1.5/: 401, server \"embedded\", login \"Digest realm=\"Phone\"\", title \"Login\", 24 chars", response.ToString());
    }

    [Fact]
    public void Shows_the_start_of_pages_without_a_title()
    {
        var response = new ProbeResponse("http://192.168.1.5/", 200, Body: "<html>\n<script src=\"app.js\"></script>");

        Assert.Equal("http://192.168.1.5/: 200, page \"<html> <script src=\"app.js\"></script>\", 37 chars", response.ToString());
    }

    [Fact]
    public void Tells_why_a_device_did_not_answer() =>
        Assert.Equal("http://192.168.1.5/: timeout", new ProbeResponse("http://192.168.1.5/", Error: "timeout").ToString());

    [Fact]
    public void Keeps_a_redirect_whose_target_does_not_answer()
    {
        var redirect = new ProbeResponse("http://192.168.2.9/", 302, Location: "https://192.168.2.9/login");
        var response = new ProbeResponse("https://192.168.2.9/login", Error: "TLS failed", Previous: redirect);

        Assert.True(response.Answered);
        Assert.Contains("https://192.168.2.9/login", response.Text);
        Assert.Equal("http://192.168.2.9/: 302, redirect to https://192.168.2.9/login, page \"\", 0 chars -> https://192.168.2.9/login: TLS failed",
            response.ToString());
    }
}
