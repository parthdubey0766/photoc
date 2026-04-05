using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace PhotoC.UI;

public partial class LogWindow : Window
{
    private const int MaxLogLines = 500;
    private readonly string _logDir;
    private readonly DispatcherTimer _refreshTimer;
    private long _lastPos;
    private string? _currentFile;
    private readonly Paragraph _paragraph;

    public LogWindow(string logDir)
    {
        _logDir = logDir;
        InitializeComponent();
        
        _paragraph = new Paragraph();
        LogBox.Document = new FlowDocument(_paragraph);

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += (_, _) => RefreshLog();
        _refreshTimer.Start();
        RefreshLog();
    }

    private void RefreshLog()
    {
        if (!Directory.Exists(_logDir)) return;

        var today = DateTime.Now.ToString("yyyyMMdd");
        var logFile = Directory.EnumerateFiles(_logDir, $"photoc-{today}.log").FirstOrDefault()
            ?? Directory.EnumerateFiles(_logDir, "photoc-*.log").OrderByDescending(f => f).FirstOrDefault();

        if (logFile == null) return;

        if (logFile != _currentFile)
        {
            _currentFile = logFile;
            _lastPos = 0;
        }

        try
        {
            using var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(_lastPos, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);
            
            string? line;
            bool added = false;
            while ((line = reader.ReadLine()) != null)
            {
                AppendColoredLine(line);
                added = true;
            }
            _lastPos = fs.Position;

            if (added)
            {
                LogBox.ScrollToEnd();
            }
        }
        catch { /* best effort */ }
    }

    private void AppendColoredLine(string line)
    {
        var run = new Run(line + "\n");
        if (line.Contains("[WRN]") || line.Contains("[WAR]"))
            run.Foreground = Brushes.Yellow;
        else if (line.Contains("[ERR]") || line.Contains("[FAT]"))
            run.Foreground = Brushes.Red;
        else if (line.Contains("[DBG]"))
            run.Foreground = Brushes.LightSkyBlue;
        else
            run.Foreground = Brushes.White;

        _paragraph.Inlines.Add(run);

        // Trim old lines to keep memory bounded
        while (_paragraph.Inlines.Count > MaxLogLines)
            _paragraph.Inlines.Remove(_paragraph.Inlines.FirstInline);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _paragraph.Inlines.Clear();
        _lastPos = 0;
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(_logDir))
        {
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = _logDir, UseShellExecute = true });
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _refreshTimer.Stop();
        base.OnClosed(e);
    }
}
