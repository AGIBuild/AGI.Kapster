using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Serilog;
using AGI.Kapster.Desktop.Services.Settings;
using AGI.Kapster.Desktop.Services.ErrorHandling;

namespace AGI.Kapster.Desktop.Services;

/// <summary>
/// Application controller for managing app lifecycle and startup
/// </summary>
public class ApplicationController : IApplicationController
{
    private readonly ISettingsService _settingsService;
    private readonly IStartupManager _startupManager;
    private readonly IErrorHandler _errorHandler;

    public ApplicationController(
        ISettingsService settingsService, 
        IStartupManager startupManager,
        IErrorHandler errorHandler)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _startupManager = startupManager ?? throw new ArgumentNullException(nameof(startupManager));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Settings are loaded in constructor now

            // No main window needed - application runs in background

            // Apply startup settings using platform-specific manager
            if (_startupManager.IsSupported)
            {
                var shouldStartWithWindows = _settingsService.Settings.General.StartWithWindows;
                var currentlyEnabled = await _startupManager.IsStartupEnabledAsync();

                if (shouldStartWithWindows != currentlyEnabled)
                {
                    await _startupManager.SetStartupAsync(shouldStartWithWindows);
                }

                Log.Debug("Application controller initialized. Startup enabled: {StartupEnabled}", shouldStartWithWindows);
            }
            else
            {
                Log.Warning("Startup with system is not supported on this platform");
            }
        }
        catch (Exception ex)
        {
            // Controller layer: catch and notify
            await _errorHandler.HandleErrorAsync(ex, ErrorSeverity.Error, "Application controller initialization");
        }
    }

    public async Task<bool> SetStartupWithWindowsAsync(bool enabled)
    {
        if (!_startupManager.IsSupported)
        {
            Log.Warning("Startup with system is not supported on this platform");
            return false;
        }

        try
        {
            var success = await _startupManager.SetStartupAsync(enabled);
            
            if (success)
            {
                // Update settings
                _settingsService.Settings.General.StartWithWindows = enabled;
                await _settingsService.SaveAsync();
                Log.Debug("System startup setting updated: {Enabled}", enabled);
            }

            return success;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set startup with system: {Enabled}", enabled);
            return false;
        }
    }

    public Task<bool> IsStartupWithWindowsEnabledAsync()
    {
        if (!_startupManager.IsSupported)
        {
            return Task.FromResult(false);
        }

        return _startupManager.IsStartupEnabledAsync();
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
