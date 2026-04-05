using PhotoC.Models;
using System.Text.Json;

namespace PhotoC.Services;

/// <summary>
/// Loads and persists <see cref="AppSettings"/> to %APPDATA%\PhotoC\appsettings.json.
/// Fires <see cref="SettingsChanged"/> whenever settings are saved.
/// </summary>
public class ConfigurationService
{
    private static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhotoC");

    private static readonly string SettingsPath =
        Path.Combine(AppDataDir, "appsettings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null   // PascalCase keys to match class properties
    };

    public AppSettings Current { get; private set; }

    public event EventHandler<AppSettings>? SettingsChanged;

    public ConfigurationService()
    {
        Directory.CreateDirectory(AppDataDir);
        Current = Load();
    }

    // -----------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------

    public void Save(AppSettings settings)
    {
        Current = settings;
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
        SettingsChanged?.Invoke(this, settings);
    }

    public AppSettings Load()
    {
        // Seed from the embedded default if no user file exists yet
        if (!File.Exists(SettingsPath))
            SeedDefaults();

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings != null)
            {
                // Resolve empty LogPath to the default
                if (string.IsNullOrWhiteSpace(settings.LogPath))
                    settings.LogPath = Path.Combine(AppDataDir, "logs");

                return settings;
            }
        }
        catch { /* fall through to defaults */ }

        return CreateDefaults();
    }

    public string GetLogDirectory()
    {
        var dir = string.IsNullOrWhiteSpace(Current.LogPath)
            ? Path.Combine(AppDataDir, "logs")
            : Current.LogPath;
        Directory.CreateDirectory(dir);
        return dir;
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static void SeedDefaults()
    {
        var defaults = CreateDefaults();
        Directory.CreateDirectory(AppDataDir);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(defaults, JsonOptions));
    }

    private static AppSettings CreateDefaults() => new()
    {
        LogPath = Path.Combine(AppDataDir, "logs")
    };
}
