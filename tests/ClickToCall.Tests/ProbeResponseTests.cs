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
}
