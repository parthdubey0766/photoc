using Serilog;

namespace PhotoC.Helpers;

/// <summary>
/// Configures the global Serilog logger with a rolling daily file sink.
/// </summary>
public static class LoggingHelper
{
    public static void Configure(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);

        var logFile = Path.Combine(logDirectory, "photoc-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: logFile,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    public static void CloseAndFlush() => Log.CloseAndFlush();
}
