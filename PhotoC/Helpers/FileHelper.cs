using Serilog;

namespace PhotoC.Helpers;

/// <summary>
/// Utility methods for safe file operations with retry logic.
/// </summary>
public static class FileHelper
{
    /// <summary>
    /// Retries <paramref name="action"/> up to <paramref name="maxAttempts"/> times
    /// with exponential back-off starting at <paramref name="baseDelayMs"/> ms.
    /// </summary>
    public static async Task<bool> RetryAsync(
        Func<Task> action,
        int maxAttempts = 3,
        int baseDelayMs = 1000,
        string? context = null)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await action();
                return true;
            }
            catch (IOException ex) when (attempt < maxAttempts)
            {
                int delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                Log.Warning("Retry {Attempt}/{Max} for '{Context}': {Msg}. Waiting {Delay}ms.",
                    attempt, maxAttempts, context ?? "unknown", ex.Message, delay);
                await Task.Delay(delay);
            }
        }
        return false;
    }

    /// <summary>
    /// Cleans up any orphaned *.photoc.tmp files left over from a previous crash.
    /// </summary>
    public static void SweepOrphanedTempFiles(string directory)
    {
        if (!Directory.Exists(directory)) return;
        try
        {
            foreach (var tmp in Directory.EnumerateFiles(directory, "*.photoc.tmp"))
            {
                try
                {
                    File.Delete(tmp);
                    Log.Information("Swept orphaned temp file: {File}", tmp);
                }
                catch (Exception ex)
                {
                    Log.Warning("Could not delete orphan {File}: {Msg}", tmp, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning("Could not sweep temp files in {Dir}: {Msg}", directory, ex.Message);
        }
    }

    /// <summary>Returns true if the file exists and is not locked by another process.</summary>
    public static bool IsFileReady(string path)
    {
        try
        {
            using var fs = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException) { return false; }
    }
}
