using PhotoC.Helpers;
using PhotoC.Models;
using Serilog;
using System.Collections.Concurrent;
using System.IO;

namespace PhotoC.Services;

/// <summary>
/// Wraps <see cref="FileSystemWatcher"/> with debounce logic so that
/// files are only queued after writes have settled.
/// </summary>
public class FolderWatcherService : IDisposable
{
    private readonly QueueService _queue;
    private FileSystemWatcher? _watcher;

    // Debounce dictionary: filePath → timer that fires when quiet period ends
    private readonly ConcurrentDictionary<string, System.Threading.Timer> _debounceTimers = new();
    private readonly ConcurrentDictionary<string, DateTime> _recentlyCompressed = new();

    private AppSettings _settings;
    private bool _isRunning;

    public bool IsRunning => _isRunning;

    /// <summary>Raised when the watched folder becomes inaccessible.</summary>
    public event Action<string>? WatcherError;

    public FolderWatcherService(QueueService queue, AppSettings settings)
    {
        _queue = queue;
        _settings = settings;
    }

    // -----------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------

    public void Start(AppSettings? settings = null)
    {
        if (settings != null) _settings = settings;

        Stop(); // Dispose any existing watcher first

        if (string.IsNullOrWhiteSpace(_settings.WatchedFolderPath) ||
            !Directory.Exists(_settings.WatchedFolderPath))
        {
            Log.Warning("Watched folder is not set or does not exist: '{Path}'",
                _settings.WatchedFolderPath);
            return;
        }

        // Sweep any leftover temps from a previous run
        FileHelper.SweepOrphanedTempFiles(_settings.WatchedFolderPath);

        _watcher = new FileSystemWatcher(_settings.WatchedFolderPath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };

        // Add all configured extensions as filters (using * for all then we filter in handler)
        _watcher.Filter = "*.*";

        _watcher.Created += OnFileEvent;
        _watcher.Changed += OnFileEvent;
        _watcher.Renamed += OnFileRenamed;
        _watcher.Error += OnWatcherError;

        _isRunning = true;
        Log.Information("Watching folder: {Path}", _settings.WatchedFolderPath);

        // Queue pre-existing files once at startup/settings apply so users do not
        // need to re-copy old phone photos for processing.
        EnqueueExistingFiles();
    }

    public void Stop()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnFileEvent;
            _watcher.Changed -= OnFileEvent;
            _watcher.Renamed -= OnFileRenamed;
            _watcher.Error -= OnWatcherError;
            _watcher.Dispose();
            _watcher = null;
        }

        // Cancel all pending debounce timers
        foreach (var (_, timer) in _debounceTimers)
            timer.Dispose();
        _debounceTimers.Clear();

        _isRunning = false;
        Log.Information("Folder watcher stopped.");
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        if (_isRunning)
        {
            Stop();
            Start();
        }
    }

    // -----------------------------------------------------------------
    // Event handlers
    // -----------------------------------------------------------------

    private void OnFileEvent(object sender, FileSystemEventArgs e)
        => ScheduleDebounce(e.FullPath);

    private void OnFileRenamed(object sender, RenamedEventArgs e)
        => ScheduleDebounce(e.FullPath);

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        Log.Error(ex, "FileSystemWatcher error in '{Path}'", _settings.WatchedFolderPath);
        WatcherError?.Invoke(ex.Message);
        _isRunning = false;
    }

    // -----------------------------------------------------------------
    // Debounce logic
    // -----------------------------------------------------------------

    private void ScheduleDebounce(string fullPath)
    {
        // Skip temp files we created ourselves
        if (fullPath.EndsWith(".photoc.tmp", StringComparison.OrdinalIgnoreCase))
            return;

        // Skip files that are inside the output folder (avoid processing our own output)
        if (_settings.SaveToOutputFolder && !string.IsNullOrWhiteSpace(_settings.OutputFolderPath))
        {
            var outputDir = Path.GetFullPath(_settings.OutputFolderPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fileFull  = Path.GetFullPath(fullPath);
            if (fileFull.StartsWith(outputDir, StringComparison.OrdinalIgnoreCase))
                return;
        }

        // Suppress immediate re-processing loops caused by our own atomic replace.
        // (Not needed in output folder mode since originals are never modified.)
        if (!_settings.SaveToOutputFolder && _recentlyCompressed.TryGetValue(fullPath, out var compressedAtUtc))
        {
            var suppressMs = Math.Max(_settings.DebounceMilliseconds * 2, 15000);
            if ((DateTime.UtcNow - compressedAtUtc).TotalMilliseconds < suppressMs)
                return;

            _recentlyCompressed.TryRemove(fullPath, out _);
        }

        // Skip extensions not in the watched list
        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        if (!_settings.FileExtensions.Contains(ext))
            return;

        // Reset (or create) the debounce timer for this file
        _debounceTimers.AddOrUpdate(
            fullPath,
            // New timer
            _ => new System.Threading.Timer(OnDebounceElapsed, fullPath, _settings.DebounceMilliseconds, Timeout.Infinite),
            // Reset existing timer
            (_, existingTimer) =>
            {
                existingTimer.Change(_settings.DebounceMilliseconds, Timeout.Infinite);
                return existingTimer;
            });
    }

    public void MarkRecentlyCompressed(string fullPath)
    {
        _recentlyCompressed[fullPath] = DateTime.UtcNow;
    }

    private void OnDebounceElapsed(object? state)
    {
        var fullPath = (string)state!;

        // Remove + dispose the timer
        if (_debounceTimers.TryRemove(fullPath, out var timer))
            timer.Dispose();

        Log.Debug("Debounce elapsed, enqueuing: {File}", Path.GetFileName(fullPath));
        _queue.Enqueue(fullPath);
    }

    private void EnqueueExistingFiles()
    {
        try
        {
            var files = Directory.EnumerateFiles(_settings.WatchedFolderPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(path => _settings.FileExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
                .Where(path => !path.EndsWith(".photoc.tmp", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var file in files)
                _queue.Enqueue(file);

            if (files.Count > 0)
                Log.Information("Queued {Count} existing files from watched folder.", files.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not enqueue existing files from watched folder.");
        }
    }

    // -----------------------------------------------------------------
    // IDisposable
    // -----------------------------------------------------------------

    public void Dispose() => Stop();
}
