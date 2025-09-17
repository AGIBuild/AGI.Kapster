using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Win32;
using Serilog;

namespace AGI.Captor.Desktop.Services;

/// <summary>
/// Application controller for managing app lifecycle and startup
/// </summary>
public class ApplicationController : IApplicationController
{
    private const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "AGI.Captor";
    
    private readonly ISettingsService _settingsService;

    public ApplicationController(ISettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Settings are loaded in constructor now
            
            // No main window needed - application runs in background
            
            // Apply startup settings
            var shouldStartWithWindows = _settingsService.Settings.General.StartWithWindows;
            var currentlyEnabled = await IsStartupWithWindowsEnabledAsync();
            
            if (shouldStartWithWindows != currentlyEnabled)
            {
                await SetStartupWithWindowsAsync(shouldStartWithWindows);
            }
            
            Log.Debug("Application controller initialized. Startup with Windows: {StartupEnabled}", shouldStartWithWindows);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize application controller");
        }
    }

    public async Task<bool> SetStartupWithWindowsAsync(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
        {
            Log.Warning("Startup with Windows is only supported on Windows platform");
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
            if (key == null)
            {
                Log.Error("Could not open Windows startup registry key");
                return false;
            }

            if (enabled)
            {
                // Add to startup
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath))
                {
                    Log.Error("Could not determine application executable path");
                    return false;
                }

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

            // Update settings
            _settingsService.Settings.General.StartWithWindows = enabled;
            await _settingsService.SaveAsync();

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set startup with Windows: {Enabled}", enabled);
            return false;
        }
    }

    public Task<bool> IsStartupWithWindowsEnabledAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(false);
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false);
            var value = key?.GetValue(AppName) as string;
            var isEnabled = !string.IsNullOrEmpty(value);
            
            Log.Debug("Startup with Windows status checked: {Enabled}", isEnabled);
            return Task.FromResult(isEnabled);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to check startup with Windows status");
            return Task.FromResult(false);
        }
    }


    public void RestartApplication()
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true
                });
                
                ExitApplication();
                
                Log.Information("Application restart initiated");
            }
            else
            {
                Log.Error("Could not determine application executable path for restart");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to restart application");
        }
    }

    public void ExitApplication()
    {
        try
        {
            Log.Information("Application exit initiated");
            
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.TryShutdown();
            }
            else
            {
                Environment.Exit(0);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to exit application gracefully");
            Environment.Exit(1);
        }
    }
}
