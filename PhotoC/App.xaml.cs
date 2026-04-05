using H.NotifyIcon;
using ImageMagick;
using PhotoC.Helpers;
using PhotoC.Services;
using PhotoC.UI;
using Serilog;
using System.IO;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;

namespace PhotoC;

public partial class App : Application
{
    private const string MutexName = "Global\\PhotoC_SingleInstance_7F3A9B21";
    private System.Threading.Mutex? _mutex;
    
    private TaskbarIcon _taskbarIcon = null!;
    private ConfigurationService _config = null!;
    private FolderWatcherService _watcher = null!;
    private QueueService _queue = null!;
    private ImageProcessorService _processor = null!;

    // ── Idle auto-shutdown: exit after 2 min of empty queue to free RAM ──
    private const int IdleShutdownMinutes = 2;
    private System.Threading.Timer? _idleShutdownTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 1. Single Instance Check
        _mutex = new System.Threading.Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("PhotoC is already running.\nCheck your system tray.", "PhotoC", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
            Current.Shutdown();
            return;
        }

        // ── Memory optimisation: cap ImageMagick native allocations ──
        ResourceLimits.Memory = 256UL * 1024 * 1024;   // 256 MB max managed pixel cache
        ResourceLimits.Area   = 128UL * 1024 * 1024;   // 128 MP pixel area limit
        ResourceLimits.Disk   = 512UL * 1024 * 1024;   // 512 MB disk-spill cache
        ResourceLimits.Thread = 1;                      // single IM thread (we serialise work ourselves)

        // 2. Initialize Core Services
        _config = new ConfigurationService();
        LoggingHelper.Configure(_config.GetLogDirectory());
        Log.Information("PhotoC WPF starting. Version 1.0.0");

        _processor = new ImageProcessorService(_config);
        _queue = new QueueService(_processor);
        _watcher = new FolderWatcherService(_queue, _config.Current);

        // 3. Initialize Tray Icon UI
        CreateTrayIcon();
        WireEvents();

        // 4. Autostart logic
        if (!string.IsNullOrWhiteSpace(_config.Current.WatchedFolderPath) && Directory.Exists(_config.Current.WatchedFolderPath))
        {
            _queue.Start();
            _watcher.Start();
        }
        else
        {
            OpenSettings();
        }

        base.OnStartup(e);
    }

    private void CreateTrayIcon()
    {
        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "PhotoC — Phone Photo Auto-Compressor",
            Icon = LoadAppIcon()
        };

        var contextMenu = new ContextMenu();
        
        var titleItem = new MenuItem { Header = "PhotoC", IsEnabled = false };
        var settingsItem = new MenuItem { Header = "⚙ Open Settings" };
        settingsItem.Click += (_, _) => OpenSettings();
        
        var pauseItem = new MenuItem { Header = "⏸ Pause Monitoring", Name = "PauseMenuItem" };
        pauseItem.Click += TogglePause_Click;
        
        var logItem = new MenuItem { Header = "📋 View Log" };
        logItem.Click += (_, _) => new LogWindow(_config.GetLogDirectory()).Show();
        
        var exitItem = new MenuItem { Header = "✕ Exit" };
        exitItem.Click += (_, _) => Current.Shutdown();

        contextMenu.Items.Add(titleItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(pauseItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(logItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(exitItem);

        _taskbarIcon.ContextMenu = contextMenu;
        _taskbarIcon.TrayMouseDoubleClick += (_, _) => OpenSettings();
        try
        {
            _taskbarIcon.ForceCreate();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Tray icon could not be created immediately.");
        }
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/PhotoC.ico", UriKind.Absolute));
            if (resource?.Stream != null)
                return new Icon(resource.Stream);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not load packaged app icon; falling back to the system icon.");
        }

        return SystemIcons.Application;
    }

    private void WireEvents()
    {
        _queue.FileCompressed += (path, orig, newSz) =>
        {
            _watcher.MarkRecentlyCompressed(path);
            Log.Information(
                "Compressed {File}: {Old} KB -> {New} KB",
                Path.GetFileName(path), orig / 1024, newSz / 1024);
            // New work arrived — cancel any pending idle shutdown
            CancelIdleShutdown();
        };

        _queue.FileError += (path, msg) => ShowBalloon("Error", $"Could not compress {Path.GetFileName(path)}:\n{msg}");
        _watcher.WatcherError += msg => ShowBalloon("Watcher Error", $"Monitoring stopped:\n{msg}");
        _queue.PauseStateChanged += isPaused => UpdatePauseMenu(isPaused);

        // ── Idle auto-shutdown: start countdown when queue empties ──
        _queue.QueueDrained += OnQueueDrained;
    }

    // -----------------------------------------------------------------
    // Idle auto-shutdown
    // -----------------------------------------------------------------

    private void OnQueueDrained()
    {
        // Don't auto-exit if the user has any windows open
        bool hasOpenWindows = false;
        Dispatcher.Invoke(() =>
        {
            hasOpenWindows = Current.Windows.OfType<Window>().Any();
        });
        if (hasOpenWindows) return;

        Log.Information("Queue empty — starting {Min}-minute idle shutdown timer.", IdleShutdownMinutes);
        CancelIdleShutdown();
        _idleShutdownTimer = new System.Threading.Timer(
            _ => PerformIdleShutdown(),
            null,
            TimeSpan.FromMinutes(IdleShutdownMinutes),
            Timeout.InfiniteTimeSpan);
    }

    private void CancelIdleShutdown()
    {
        if (_idleShutdownTimer != null)
        {
            _idleShutdownTimer.Dispose();
            _idleShutdownTimer = null;
            Log.Debug("Idle shutdown timer cancelled — new work detected.");
        }
    }

    private void PerformIdleShutdown()
    {
        Log.Information("Idle for {Min} minutes — auto-shutting down to free system resources.", IdleShutdownMinutes);
        Dispatcher.Invoke(() => Current.Shutdown());
    }

    private void ShowBalloon(string title, string text)
    {
        try
        {
            _taskbarIcon.ForceCreate();
            _taskbarIcon.ShowNotification(title, text, H.NotifyIcon.Core.NotificationIcon.Info);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Tray notification skipped: {Title}", title);
        }
    }

    private void OpenSettings()
    {
        CancelIdleShutdown(); // user interaction — stay alive
        var existing = Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
        if (existing != null)
        {
            existing.Activate();
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            return;
        }
        new SettingsWindow(_config, _watcher, _queue).Show();
    }

    private void TogglePause_Click(object sender, RoutedEventArgs e)
    {
        if (_queue.IsPaused)
        {
            _queue.Resume();
            _watcher.Start();
        }
        else
        {
            _queue.Pause();
            _watcher.Stop();
        }
    }

    private void UpdatePauseMenu(bool isPaused)
    {
        if (_taskbarIcon.ContextMenu == null) return;
        foreach (var item in _taskbarIcon.ContextMenu.Items)
        {
            if (item is MenuItem mi && mi.Name == "PauseMenuItem")
            {
                mi.Header = isPaused ? "▶ Resume Monitoring" : "⏸ Pause Monitoring";
                break;
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        CancelIdleShutdown();
        _taskbarIcon?.Dispose();
        _watcher?.Dispose();
        _queue?.Dispose();
        LoggingHelper.CloseAndFlush();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
