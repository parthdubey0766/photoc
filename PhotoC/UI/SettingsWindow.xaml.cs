using PhotoC.Models;
using PhotoC.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PhotoC.UI;

public partial class SettingsWindow : Window
{
    private readonly ConfigurationService _config;
    private readonly FolderWatcherService _watcher;
    private readonly QueueService _queue;
    private readonly DispatcherTimer _statusTimer;

    public SettingsWindow(ConfigurationService config, FolderWatcherService watcher, QueueService queue)
    {
        _config = config;
        _watcher = watcher;
        _queue = queue;

        InitializeComponent();
        LoadValues();

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += (_, _) => RefreshStatus();
        _statusTimer.Start();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void LoadValues()
    {
        var s = _config.Current;
        FolderPathBox.Text = s.WatchedFolderPath;
        JpegSlider.Value = Math.Clamp(s.JPEGQuality, 60, 100);
        WebpSlider.Value = Math.Clamp(s.WebPQuality, 1, 100);
        PngLevelBox.Text = Math.Clamp(s.PNGCompressionLevel, 0, 9).ToString();
        MinSizeBox.Text = Math.Clamp(s.MinFileSizeKB, 0, 102400).ToString();
        MaxDimensionBox.Text = Math.Max(s.MaxImageDimension, 0).ToString();
        StartupCheckBox.IsChecked = StartupService.IsStartupEnabled();

        // Output folder
        SaveToOutputFolderCheckBox.IsChecked = s.SaveToOutputFolder;
        OutputFolderPathBox.Text = s.OutputFolderPath;
        UpdateOutputFolderVisibility(s.SaveToOutputFolder);

        RefreshStatus();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select the folder where phone photos are synced",
            DefaultDirectory = string.IsNullOrEmpty(FolderPathBox.Text) ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) : FolderPathBox.Text
        };

        if (dialog.ShowDialog() == true)
        {
            FolderPathBox.Text = dialog.FolderName;
        }
    }

    private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select the folder where compressed photos will be saved",
            DefaultDirectory = string.IsNullOrEmpty(OutputFolderPathBox.Text) ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) : OutputFolderPathBox.Text
        };

        if (dialog.ShowDialog() == true)
        {
            OutputFolderPathBox.Text = dialog.FolderName;
        }
    }

    private void OutputFolderToggle_Changed(object sender, RoutedEventArgs e)
    {
        bool isChecked = SaveToOutputFolderCheckBox.IsChecked == true;
        UpdateOutputFolderVisibility(isChecked);
    }

    private void UpdateOutputFolderVisibility(bool showOutputFolder)
    {
        OutputFolderGrid.Visibility = showOutputFolder ? Visibility.Visible : Visibility.Collapsed;
        OutputFolderHint.Text = showOutputFolder
            ? "Compressed copies will be saved here. Originals stay untouched."
            : "Originals will be replaced in-place.";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = _config.Current;
        settings.WatchedFolderPath = FolderPathBox.Text.Trim();
        settings.JPEGQuality = (int)JpegSlider.Value;
        settings.WebPQuality = (int)WebpSlider.Value;
        
        if (int.TryParse(PngLevelBox.Text, out int pngLevel))
            settings.PNGCompressionLevel = Math.Clamp(pngLevel, 0, 9);
            
        if (int.TryParse(MinSizeBox.Text, out int minSize))
            settings.MinFileSizeKB = Math.Clamp(minSize, 0, 102400);

        if (int.TryParse(MaxDimensionBox.Text, out int maxDim))
            settings.MaxImageDimension = Math.Max(maxDim, 0);

        settings.RunOnStartup = StartupCheckBox.IsChecked == true;

        // Output folder
        settings.SaveToOutputFolder = SaveToOutputFolderCheckBox.IsChecked == true;
        settings.OutputFolderPath = OutputFolderPathBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(settings.WatchedFolderPath))
        {
            MessageBox.Show("Please select a folder to watch.", "PhotoC", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (settings.SaveToOutputFolder && string.IsNullOrWhiteSpace(settings.OutputFolderPath))
        {
            MessageBox.Show("Please select an output folder for compressed photos, or disable the output folder option.", "PhotoC", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _config.Save(settings);
        StartupService.SetStartupEnabled(settings.RunOnStartup);

        if (!_watcher.IsRunning)
        {
            _queue.Start();
            _watcher.Start(settings);
        }
        else
        {
            _watcher.UpdateSettings(settings);
        }

        RefreshStatus();
        
        MessageBox.Show("Settings Saved successfully! The watcher has been restarted.", "PhotoC", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ViewLogButton_Click(object sender, RoutedEventArgs e)
    {
        new LogWindow(_config.GetLogDirectory()).Show();
    }

    private void RefreshStatus()
    {
        bool running = _watcher.IsRunning;
        StatusDot.Foreground = running ? new SolidColorBrush(Colors.LightGreen) : new SolidColorBrush(Colors.OrangeRed);
        StatusText.Text = $"{(running ? "Watching Active" : "Stopped")}  •  Queue: {_queue.QueueDepth}  •  Handled: {_queue.TotalHandled}  •  Compressed: {_queue.TotalProcessed}  •  Skipped: {_queue.TotalSkipped}  •  Errors: {_queue.TotalErrors}";
    }

    protected override void OnClosed(EventArgs e)
    {
        _statusTimer.Stop();
        base.OnClosed(e);
    }
}
