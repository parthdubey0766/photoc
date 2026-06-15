using Serilog;
using System.Text.Json;

namespace PhotoC.Helpers;

/// <summary>
/// Tracks folder sizes across restarts so the watcher can skip folders
/// that haven't changed since the last run. Persists data to a JSON file
/// in %APPDATA%\PhotoC.
/// </summary>
public class HistoryManager
{
    private readonly string _historyFilePath;
    private Dictionary<string, long> _folderSizes;

    public HistoryManager()
    {
        var appDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhotoC");
        Directory.CreateDirectory(appDir);
        _historyFilePath = Path.Combine(appDir, "folder_history.json");
        _folderSizes = LoadHistory();
    }

    /// <summary>
    /// Returns <c>true</c> if the folder's current size matches the last
    /// recorded size — meaning no new files have been added/changed.
    /// </summary>
    public bool HasUnchangedFolderSize(string folderPath, long currentSize)
    {
        var key = NormalizeKey(folderPath);
        return _folderSizes.TryGetValue(key, out long saved) && saved == currentSize;
    }

    /// <summary>
    /// Records the current folder size so future runs can detect changes.
    /// </summary>
    public void UpdateFolderSize(string folderPath, long currentSize)
    {
        var key = NormalizeKey(folderPath);
        _folderSizes[key] = currentSize;
        SaveHistory();
    }

    // -----------------------------------------------------------------

    private static string NormalizeKey(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
               .ToLowerInvariant();

    private Dictionary<string, long> LoadHistory()
    {
        try
        {
            if (File.Exists(_historyFilePath))
            {
                var json = File.ReadAllText(_historyFilePath);
                return JsonSerializer.Deserialize<Dictionary<string, long>>(json)
                       ?? new Dictionary<string, long>();
            }
        }
        catch (Exception ex)
        {
            Log.Warning("Could not load folder history: {Msg}", ex.Message);
        }
        return new Dictionary<string, long>();
    }

    private void SaveHistory()
    {
        try
        {
            var json = JsonSerializer.Serialize(_folderSizes, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_historyFilePath, json);
        }
        catch (Exception ex)
        {
            Log.Warning("Could not save folder history: {Msg}", ex.Message);
        }
    }
}
