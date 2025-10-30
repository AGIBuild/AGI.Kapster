using System;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Serilog;

using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services;
using AGI.Kapster.Desktop.Services.Hotkeys;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using AGI.Kapster.Desktop.Services.Update;
using AGI.Kapster.Desktop.Services.Settings;
using AGI.Kapster.Desktop.Views;

namespace AGI.Kapster.Desktop;

public partial class App : Application
{
    public static IServiceProvider? Services { get; internal set; }
    private static SettingsWindow? _settingsWindow;
    private static readonly object _settingsWindowLock = new object();

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
            // Create and set ScreenMonitorWindow as MainWindow
            // This hidden window monitors screen changes and provides IScreenMonitorService
            var monitorWindow = new Services.ScreenMonitorWindow();
            desktop.MainWindow = monitorWindow;
            
            // Change shutdown mode to exit when MainWindow closes
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;

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

                // Show startup notification
                ShowStartupNotification(trayService);
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
        ShowSettingsWindow();
    }

    /// <summary>
    /// Shows the settings window. If a settings window is already open, brings it to focus instead of creating a new one.
    /// </summary>
    public static void ShowSettingsWindow()
    {
        // Ensure all window operations run on UI thread to avoid duplicates and crashes
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ShowSettingsWindow);
            return;
        }

        try
        {
            // Ensure we're on the UI thread
            if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => ShowSettingsWindow());
                return;
            }

            // Check if screenshot is currently in progress
            var overlayCoordinator = Services!.GetRequiredService<IOverlayCoordinator>();
            if (overlayCoordinator.HasActiveSession)
            {
                Log.Debug("Settings window request ignored - screenshot is active");
                return;
            }

            // Use lock to prevent race conditions when checking and creating window
            SettingsWindow? windowToShow;
            bool isNewWindow;
            
            lock (_settingsWindowLock)
            {
                // Check if settings window already exists
                // Since we're on UI thread now, if _settingsWindow exists, it's either:
                // 1. Already shown and visible
                // 2. Just created but not yet shown (in which case we should reuse it)
                if (_settingsWindow != null)
                {
                    Log.Debug("Settings window already exists, bringing to focus");
                    windowToShow = _settingsWindow;
                    isNewWindow = false;
                }
                else
                {
                    var settingsService = Services!.GetRequiredService<ISettingsService>();
                    var applicationController = Services!.GetRequiredService<IApplicationController>();
                    var updateService = Services!.GetRequiredService<IUpdateService>();
                    _settingsWindow = new SettingsWindow(settingsService, applicationController, updateService);
                    
                    // Clear reference when window is closed
                    _settingsWindow.Closed += (s, args) =>
                    {
                        lock (_settingsWindowLock)
                        {
                            _settingsWindow = null;
                            Log.Debug("Settings window closed, reference cleared");
                        }
                    };
                    
                    windowToShow = _settingsWindow;
                    isNewWindow = true;
                }
            }
            
            // Perform UI operations - safe because we're on UI thread and lock protects the check
            if (isNewWindow)
            {
                windowToShow.Show();
                windowToShow.Activate();
                Log.Debug("Settings window opened");
            }
            else
            {
                windowToShow.Activate();
            }
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

            // Use screen monitor service to exit (closes MainWindow properly)
            var screenMonitor = Services!.GetRequiredService<IScreenMonitorService>();
            screenMonitor.RequestAppExit();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to exit application");
            
            // Fallback to direct shutdown
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }
    }

    private void ShowStartupNotification(ISystemTrayService trayService)
    {
        try
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var versionString = version?.ToString() ?? "Unknown";

            // Show startup notification with a slight delay
            System.Threading.Tasks.Task.Delay(1000).ContinueWith(_ =>
            {
                try
                {
                    trayService.ShowNotification(
                        "AGI Kapster Started", 
                        $"Version {versionString} is running in the background. Use Alt+A to take screenshots.");
                    
                    Log.Debug("Startup notification displayed");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to show startup notification");
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to schedule startup notification");
        }
    }
}