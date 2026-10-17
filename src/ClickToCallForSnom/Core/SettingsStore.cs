using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClickToCallForSnom.Core;

/// <summary>Loads and saves <see cref="AppSettings"/> to %APPDATA%\ClickToCallForSnom\settings.json.</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Directory { get; private set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClickToCallForSnom");

    public static string FilePath => Path.Combine(Directory, "settings.json");

    /// <summary>Stores settings (and the log) in another folder, e.g. for the development mock. Call before <see cref="Load"/>.</summary>
    internal static void UseDirectory(string directory) => Directory = directory;

    public static AppSettings Current { get; private set; } = new();

    /// <summary>Raised after <see cref="Save"/> replaced the current settings.</summary>
    public static event EventHandler? Changed;

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                Current = Deserialize(File.ReadAllText(FilePath));
            else
                Current = new AppSettings { OwnCountryCode = AppSettings.CountryCodeForRegion() };
        }
        catch (Exception ex)
        {
            // Keep the broken file: the next automatic save would otherwise overwrite the user's configuration
            Log.Error("Could not load settings, using defaults", ex);
            try
            {
                File.Copy(FilePath, FilePath + ".broken", overwrite: true);
            }
            catch (Exception copyEx)
            {
                Log.Error("Could not back up broken settings", copyEx);
            }

            Current = new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var tempFile = FilePath + ".tmp";
        File.WriteAllText(tempFile, Serialize(settings));
        File.Move(tempFile, FilePath, overwrite: true);

        Current = settings;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Saves the current settings again (e.g. after the call history changed).</summary>
    public static void SaveCurrent()
    {
        try
        {
            Save(Current);
        }
        catch (Exception ex)
        {
            Log.Error("Could not save settings", ex);
        }
    }

    internal static string Serialize(AppSettings settings) => JsonSerializer.Serialize(settings, Options);

    internal static AppSettings Deserialize(string json) => JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
}
