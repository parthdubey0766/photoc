using Microsoft.Win32;
using Serilog;

namespace PhotoC.Services;

/// <summary>
/// Manages the Windows startup registry entry for PhotoC.
/// Key: HKCU\Software\Microsoft\Windows\CurrentVersion\Run
/// </summary>
public static class StartupService
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "PhotoC";

    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: false);
            return key?.GetValue(AppName) != null;
        }
        catch (Exception ex)
        {
            Log.Warning("Could not read startup registry key: {Msg}", ex.Message);
            return false;
        }
    }

    public static void SetStartupEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
            if (key == null)
            {
                Log.Error("Startup registry key not found.");
                return;
            }

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                key.SetValue(AppName, $"\"{exePath}\"");
                Log.Information("Run-on-startup enabled: {Path}", exePath);
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
                Log.Information("Run-on-startup disabled.");
            }
        }
        catch (Exception ex)
        {
            Log.Error("Could not update startup registry: {Msg}", ex.Message);
        }
    }
}
