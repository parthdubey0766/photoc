using Serilog;
using System.Collections.Concurrent;

namespace PhotoC.Services;

/// <summary>
/// Thread-safe producer-consumer queue.
/// A single background worker dequeues paths and calls <see cref="ImageProcessorService"/>.
/// </summary>
public class QueueService : IDisposable
{
    private readonly BlockingCollection<string> _queue = new(boundedCapacity: 100);
    private readonly ImageProcessorService _processor;
    private CancellationTokenSource _cts = new();
    private readonly List<Task> _workerTasks = new();
    private readonly ConcurrentDictionary<string, byte> _queuedOrProcessing = new(StringComparer.OrdinalIgnoreCase);
    // Single worker — keeps only one image's native pixel buffers in memory at a time
    private readonly int _maxWorkers = 1;

    // Stats
    public int TotalProcessed { get; private set; }
    public int TotalHandled { get; private set; }
    public int TotalErrors { get; private set; }
    public int TotalSkipped { get; private set; }
    public int QueueDepth => _queue.Count;

    public event Action<string, long, long>? FileCompressed;
    public event Action<string, string>? FileSkipped;
    public event Action<string, string>? FileError;
    public event Action<bool>? PauseStateChanged;
    /// <summary>Raised when the queue becomes empty after processing at least one file.</summary>
    public event Action? QueueDrained;

    private bool _isPaused;
    public bool IsPaused => _isPaused;

    public QueueService(ImageProcessorService processor)
    {
        _processor = processor;

        // Wire up processor events → our events + stats counters
        _processor.FileCompressed += (path, orig, newSize) =>
        {
            TotalHandled++;
            TotalProcessed++;
            FileCompressed?.Invoke(path, orig, newSize);
        };
        _processor.FileSkipped += (path, reason) =>
        {
            TotalHandled++;
            TotalSkipped++;
            FileSkipped?.Invoke(path, reason);
        };
        _processor.FileError += (path, msg) =>
        {
            TotalHandled++;
            TotalErrors++;
            FileError?.Invoke(path, msg);
        };
    }

    // -----------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------

    public void Start()
    {
        if (_workerTasks.Any(t => !t.IsCompleted))
            return;

        _cts = new CancellationTokenSource();
        _isPaused = false;
        _workerTasks.Clear();

        for (int i = 0; i < _maxWorkers; i++)
            _workerTasks.Add(Task.Run(() => WorkerLoop(i + 1)));

        Log.Information("Queue workers started. Count={Count}", _maxWorkers);
    }

    public void Pause()
    {
        _isPaused = true;
        PauseStateChanged?.Invoke(true);
        Log.Information("Processing paused.");
    }

    public void Resume()
    {
        _isPaused = false;
        PauseStateChanged?.Invoke(false);
        Log.Information("Processing resumed.");
    }

    public void Stop()
    {
        _cts.Cancel();
        Log.Information("Queue worker stopping.");
    }

    // -----------------------------------------------------------------
    // Enqueue
    // -----------------------------------------------------------------

    public void Enqueue(string filePath)
    {
        if (_queue.IsAddingCompleted) return;

        var fullPath = Path.GetFullPath(filePath);

        // Prevent duplicate enqueues while a file is queued/processing.
        if (!_queuedOrProcessing.TryAdd(fullPath, 0))
            return;

        if (_queue.TryAdd(fullPath))
        {
            Log.Debug("Enqueued: {File}", Path.GetFileName(fullPath));
        }
        else
        {
            _queuedOrProcessing.TryRemove(fullPath, out _);
            Log.Warning("Queue full — dropped: {File}", Path.GetFileName(filePath));
        }
    }

    // -----------------------------------------------------------------
    // Worker loop
    // -----------------------------------------------------------------

    private async Task WorkerLoop(int workerId)
    {
        var token = _cts.Token;
        bool wasBusy = false; // track busy→idle transition
        while (!token.IsCancellationRequested)
        {
            string? path = null;
            try
            {
                // When paused, spin and wait
                if (_isPaused)
                {
                    await Task.Delay(500, token);
                    continue;
                }

                if (_queue.TryTake(out path, millisecondsTimeout: 500))
                {
                    wasBusy = true;
                    Log.Debug("Worker {Worker} processing: {File}", workerId, Path.GetFileName(path));
                    await _processor.ProcessAsync(path);
                }
                else if (wasBusy && _queue.Count == 0 && TotalHandled > 0)
                {
                    // Queue just drained after real work — notify once
                    wasBusy = false;
                    QueueDrained?.Invoke();
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error in queue worker loop (worker {Worker}).", workerId);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(path))
                    _queuedOrProcessing.TryRemove(path, out _);
            }
        }
        Log.Information("Queue worker {Worker} stopped.", workerId);
    }

    // -----------------------------------------------------------------
    // IDisposable
    // -----------------------------------------------------------------

    public void Dispose()
    {
        _cts.Cancel();
        _queue.CompleteAdding();
        try
        {
            Task.WaitAll(_workerTasks.ToArray(), TimeSpan.FromSeconds(5));
        }
        catch { /* best effort on shutdown */ }
        _queue.Dispose();
        _cts.Dispose();
    }
}
