using Serilog;

namespace PhotoC.Helpers;

/// <summary>
/// Utility methods for folder operations — size calculation, subfolder
/// discovery, and exclusion-pattern matching.
/// </summary>
public static class FolderHelper
{
    /// <summary>
    /// Calculates the total size in bytes of all files matching
    /// <paramref name="extensions"/> in a single folder (non-recursive).
    /// </summary>
    public static long CalculateFolderSize(string folderPath, List<string> extensions)
    {
        if (!Directory.Exists(folderPath)) return 0;
        try
        {
            return Directory.EnumerateFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Sum(f =>
                {
                    try { return new FileInfo(f).Length; }
                    catch { return 0L; }
                });
        }
        catch (Exception ex)
        {
            Log.Warning("Could not calculate folder size for '{Dir}': {Msg}", folderPath, ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// Calculates total size across a folder and all subfolders (recursive).
    /// </summary>
    public static long CalculateFolderSizeRecursive(string folderPath, List<string> extensions)
    {
        if (!Directory.Exists(folderPath)) return 0;
        try
        {
            return Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Sum(f =>
                {
                    try { return new FileInfo(f).Length; }
                    catch { return 0L; }
                });
        }
        catch (Exception ex)
        {
            Log.Warning("Could not calculate recursive folder size for '{Dir}': {Msg}", folderPath, ex.Message);
            return 0;
        }
    }
}
