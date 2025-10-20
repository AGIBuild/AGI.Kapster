using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Win32;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Startup;

/// <summary>
/// Windows startup manager using registry
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class WindowsStartupManager : IStartupManager
{
    private const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "AGI.Kapster";

    public bool IsSupported => OperatingSystem.IsWindows();

    public Task<bool> SetStartupAsync(bool enabled)
    {
        if (!IsSupported)
        {
            Log.Warning("Windows startup manager is only supported on Windows platform");
            return Task.FromResult(false);
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
            if (key == null)
            {
                Log.Error("Could not open Windows startup registry key");
                return Task.FromResult(false);
            }

            if (enabled)
            {
                // Add to startup
                var exePath = Process.GetCurrentProcess().MainModule?.FileName
                    ?? throw new InvalidOperationException("Could not determine application executable path");

                // Add --minimized flag for startup
                var startupCommand = $"\"{exePath}\" --minimized";
                key.SetValue(AppName, startupCommand, RegistryValueKind.String);
                Log.Debug("Added application to Windows startup: {Command}", startupCommand);
            }
            else
            {
                // Remove from startup
                key.DeleteValue(AppName, false);
                Log.Debug("Removed application from Windows startup");
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set Windows startup: {Enabled}", enabled);
            return Task.FromResult(false);
        }
    }

    public Task<bool> IsStartupEnabledAsync()
    {
        if (!IsSupported)
        {
            return Task.FromResult(false);
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false);
            var value = key?.GetValue(AppName) as string;
            var isEnabled = !string.IsNullOrEmpty(value);

            Log.Debug("Windows startup status checked: {Enabled}", isEnabled);
            return Task.FromResult(isEnabled);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to check Windows startup status");
            return Task.FromResult(false);
        }
    }
}

