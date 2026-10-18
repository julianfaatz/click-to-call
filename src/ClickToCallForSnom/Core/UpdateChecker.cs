using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace ClickToCallForSnom.Core;

public sealed record UpdateInfo(Version Version, string HtmlUrl, string? InstallerUrl, string Notes);

/// <summary>Result of a successful check. <see cref="Update"/> null = this is the newest version.</summary>
public sealed record UpdateCheckResult(DateTime CheckedAt, UpdateInfo? Update);

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

    /// <summary>The last successful check (manual or automatic), null if there was none yet.</summary>
    public static UpdateCheckResult? LastCheck { get; private set; }

    /// <summary>Raised after every successful check.</summary>
    public static event Action? Checked;

    /// <returns>The newer version, or null if this is the newest one. Throws if GitHub could not be asked.</returns>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        var update = await FetchAsync(ct);
        LastCheck = new UpdateCheckResult(DateTime.Now, update);
        Checked?.Invoke();
        return update;
    }

    private static async Task<UpdateInfo?> FetchAsync(CancellationToken ct)
    {
        using var http = CreateClient();
        using var response = await http.GetAsync($"https://api.github.com/repos/{Owner}/{Repository}/releases/latest", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null; // nothing released yet
        // Anything else (rate limit, server error) must not look like "you are up to date"
        response.EnsureSuccessStatusCode();

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
            var url = asset.GetProperty("browser_download_url").GetString() ?? "";
            if (name.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase) && IsOwnUrl(url, "/releases/download/"))
                installerUrl = url;
        }

        // Opened with the shell: only ever our own release page, never whatever the response contains
        var htmlUrl = root.GetProperty("html_url").GetString() ?? "";

        return new UpdateInfo(
            latest,
            IsOwnUrl(htmlUrl, "/releases/") ? htmlUrl : RepositoryUrl + "/releases",
            installerUrl,
            root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "");
    }

    public static async Task<string> DownloadInstallerAsync(UpdateInfo update, CancellationToken ct = default)
    {
        if (update.InstallerUrl is null)
            throw new InvalidOperationException("Release has no installer");

        // Own fresh folder: no other file can sit there under the installer's name
        var directory = Directory.CreateTempSubdirectory("ClickToCallForSnom-update-");
        var path = Path.Combine(directory.FullName, $"ClickToCallForSnom-{update.Version}-setup.exe");
        try
        {
            using var http = CreateClient();
            http.Timeout = TimeSpan.FromMinutes(5);
            await using var source = await http.GetStreamAsync(update.InstallerUrl, ct);
            await using var target = File.Create(path);
            await source.CopyToAsync(target, ct);
            return path;
        }
        catch
        {
            directory.Delete(recursive: true); // no half downloaded installer left behind
            throw;
        }
    }

    /// <summary>Starts the installer in silent mode. The installer closes this app and restarts it afterwards.</summary>
    public static void RunInstaller(string installerPath)
    {
        Process.Start(new ProcessStartInfo(installerPath, "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS")
        {
            UseShellExecute = true,
        });
    }

    private static bool IsOwnUrl(string url, string pathPrefix) =>
        url.StartsWith(RepositoryUrl + pathPrefix, StringComparison.OrdinalIgnoreCase)
        && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ClickToCallForSnom", CurrentVersion.ToString()));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return http;
    }
}
