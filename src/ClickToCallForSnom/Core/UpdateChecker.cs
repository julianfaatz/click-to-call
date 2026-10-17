using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace ClickToCallForSnom.Core;

public sealed record UpdateInfo(Version Version, string HtmlUrl, string? InstallerUrl, string Notes);

/// <summary>Checks GitHub Releases for a newer version and runs the new installer.</summary>
public static class UpdateChecker
{
    public const string Owner = "julianfaatz";
    public const string Repository = "click-to-call-for-snom";
    public static string RepositoryUrl => $"https://github.com/{Owner}/{Repository}";

    public static Version CurrentVersion
    {
        get
        {
            var v = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
        }
    }

    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        using var http = CreateClient();
        using var response = await http.GetAsync($"https://api.github.com/repos/{Owner}/{Repository}/releases/latest", ct);
        if (!response.IsSuccessStatusCode)
            return null; // e.g. 404 when nothing is released yet

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest))
            return null;

        latest = new Version(latest.Major, latest.Minor, Math.Max(latest.Build, 0));
        if (latest <= CurrentVersion)
            return null;

        string? installerUrl = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (name.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase))
                installerUrl = asset.GetProperty("browser_download_url").GetString();
        }

        return new UpdateInfo(
            latest,
            root.GetProperty("html_url").GetString() ?? RepositoryUrl,
            installerUrl,
            root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "");
    }

    public static async Task<string> DownloadInstallerAsync(UpdateInfo update, CancellationToken ct = default)
    {
        if (update.InstallerUrl is null)
            throw new InvalidOperationException("Release has no installer");

        var path = Path.Combine(Path.GetTempPath(), $"ClickToCallForSnom-{update.Version}-setup.exe");
        using var http = CreateClient();
        http.Timeout = TimeSpan.FromMinutes(5);
        await using var source = await http.GetStreamAsync(update.InstallerUrl, ct);
        await using var target = File.Create(path);
        await source.CopyToAsync(target, ct);
        return path;
    }

    /// <summary>Starts the installer in silent mode. The installer closes this app and restarts it afterwards.</summary>
    public static void RunInstaller(string installerPath)
    {
        Process.Start(new ProcessStartInfo(installerPath, "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS")
        {
            UseShellExecute = true,
        });
    }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ClickToCallForSnom", CurrentVersion.ToString()));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return http;
    }
}
