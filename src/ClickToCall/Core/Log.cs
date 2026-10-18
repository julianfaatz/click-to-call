using System.IO;

namespace ClickToCall.Core;

/// <summary>Minimal file log in %APPDATA%\ClickToCall\log.txt (rotated at 1 MB).</summary>
public static class Log
{
    private static readonly object Sync = new();
    private const long MaxSize = 1024 * 1024;

    public static string FilePath { get; } = Path.Combine(SettingsStore.Directory, "log.txt");

    public static void Info(string message) => Write("INFO ", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(SettingsStore.Directory);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxSize)
                    File.Move(FilePath, FilePath + ".old", overwrite: true);

                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never crash the app
        }
    }
}
