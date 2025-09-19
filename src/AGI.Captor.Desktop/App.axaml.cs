using System;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Serilog;

using AGI.Captor.Desktop.Overlays;
using AGI.Captor.Desktop.Services;
using AGI.Captor.Desktop.Services.Hotkeys;
using AGI.Captor.Desktop.Services.Update;
using AGI.Captor.Desktop.Services.Overlay;
using AGI.Captor.Desktop.Services.Settings;
using AGI.Captor.Desktop.Views;

namespace AGI.Captor.Desktop;

public partial class App : Application
{
    public static IServiceProvider? Services { get; internal set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Setup global exception handling
        SetupGlobalExceptionHandling();
    }

    /// <summary>
    /// Setup global exception handling
    /// </summary>
    private void SetupGlobalExceptionHandling()
    {
        // Handle unhandled application domain exceptions
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var exception = e.ExceptionObject as Exception;
            Log.Fatal(exception, "Unhandled domain exception occurred. IsTerminating: {IsTerminating}", e.IsTerminating);

            if (!e.IsTerminating)
            {
                // Try to continue running
                Log.Warning("Application will attempt to continue running after unhandled exception");
            }
        };

        // Handle unobserved task exceptions
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Log.Error(e.Exception, "Unobserved task exception occurred");
            e.SetObserved(); // Mark exception as observed to prevent application crash
        };

        // Note: Avalonia doesn't provide a global OnError event in current version
        Log.Information("Global exception handling initialized successfully");
    }


    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Change shutdown mode to only exit when explicitly requested
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

            // Initialize services asynchronously
            Task.Run(async () =>
            {
                try
                {
                    // Settings service initializes automatically
                    Log.Debug("Settings service initialized");

                    // Initialize application controller
                    var appController = Services!.GetRequiredService<IApplicationController>();
                    await appController.InitializeAsync();
                    Log.Debug("Application controller initialized");

                    // Initialize hotkey manager
                    Log.Debug("Getting hotkey manager service...");
                    var hotkeyManager = Services!.GetRequiredService<IHotkeyManager>();
                    Log.Debug("Hotkey manager service obtained, initializing...");
                    await hotkeyManager.InitializeAsync();
                    Log.Debug("Hotkey manager initialized");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to initialize services");
                }
            });

            // Initialize system tray instead of main window
            try
            {
                var trayService = Services!.GetRequiredService<ISystemTrayService>();
                trayService.Initialize();

                // Handle tray events
                trayService.OpenSettingsRequested += OnOpenSettingsRequested;
                trayService.ExitRequested += OnExitRequested;

                Log.Debug("System tray initialized");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize system tray");
                // No fallback window needed - application runs in background
            }

            // Hotkeys are now managed by HotkeyManager service (initialized above)
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnOpenSettingsRequested(object? sender, EventArgs e)
    {
        try
        {
            // Check if overlay windows are currently active (screenshot in progress)
            var overlayController = Services!.GetRequiredService<IOverlayController>();
            if (overlayController.IsActive)
            {
                Log.Debug("Settings window request ignored - screenshot overlay is active");
                return;
            }

            var settingsService = Services!.GetRequiredService<ISettingsService>();
            var applicationController = Services!.GetRequiredService<IApplicationController>();
            var updateService = Services!.GetRequiredService<IUpdateService>();
            var settingsWindow = new SettingsWindow(settingsService, applicationController, updateService);
            settingsWindow.Show();
            settingsWindow.Activate();
            Log.Debug("Settings window opened");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open settings window");
        }
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        try
        {
            Log.Debug("Application exit requested from system tray");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to exit application");
        }
    }
}