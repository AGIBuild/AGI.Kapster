using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using AGI.Captor.Desktop.Dialogs;
using Serilog;

namespace AGI.Captor.Desktop.Services;

/// <summary>
/// System tray service implementation
/// </summary>
public class SystemTrayService : ISystemTrayService
{
    private TrayIcon? _trayIcon;
    private NativeMenu? _contextMenu;

    public event EventHandler? OpenSettingsRequested;
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        try
        {
            CreateTrayIcon();
            CreateContextMenu();
            Log.Debug("System tray initialized");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize system tray");
            throw;
        }
    }

    private void CreateTrayIcon()
    {
        _trayIcon = new TrayIcon
        {
            IsVisible = true,
            ToolTipText = "AGI Captor - Screenshot Tool"
        };

        // Load tray icon from logo.ico
        try
        {
            _trayIcon.Icon = GetTrayIconFromFile();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load tray icon from logo.ico, using default");
        }
    }

    private WindowIcon? GetTrayIconFromFile()
    {
        try
        {
            // Try to load logo.ico from the application directory
            var iconPath = Path.Combine(AppContext.BaseDirectory, "logo.ico");
            if (File.Exists(iconPath))
            {
                using var stream = File.OpenRead(iconPath);
                return new WindowIcon(stream);
            }

            // Try to load from assets
            try
            {
                using var stream = AssetLoader.Open(new Uri("avares://AGI.Captor.Desktop/logo.ico"));
                return new WindowIcon(stream);
            }
            catch
            {
                Log.Warning("logo.ico not found in assets, trying alternative paths");
            }

            // Try alternative asset paths
            var assetPaths = new[]
            {
                "avares://AGI.Captor.Desktop/Assets/logo.ico",
                "avares://AGI.Captor.Desktop/Assets/icon.ico",
                "avares://AGI.Captor.Desktop/icon.ico"
            };

            foreach (var path in assetPaths)
            {
                try
                {
                    using var stream = AssetLoader.Open(new Uri(path));
                    return new WindowIcon(stream);
                }
                catch
                {
                    // Continue to next path
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load tray icon from file");
        }

        return null;
    }

    private void CreateContextMenu()
    {
        _contextMenu = new NativeMenu();

        // Take Screenshot menu item
        var takeScreenshotItem = new NativeMenuItem("Take Screenshot")
        {
            ToolTip = "Capture a screenshot (Alt+A)"
        };
        takeScreenshotItem.Click += (s, e) => TakeScreenshot();
        _contextMenu.Add(takeScreenshotItem);

        // Separator
        _contextMenu.Add(new NativeMenuItemSeparator());

        // Settings menu item
        var settingsItem = new NativeMenuItem("Settings...")
        {
            ToolTip = "Open application settings"
        };
        settingsItem.Click += (s, e) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
        _contextMenu.Add(settingsItem);

        // About menu item
        var aboutItem = new NativeMenuItem("About")
        {
            ToolTip = "About AGI Captor"
        };
        aboutItem.Click += (s, e) => ShowAbout();
        _contextMenu.Add(aboutItem);

        // Separator
        _contextMenu.Add(new NativeMenuItemSeparator());

        // Exit menu item
        var exitItem = new NativeMenuItem("Exit")
        {
            ToolTip = "Exit AGI Captor"
        };
        exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
        _contextMenu.Add(exitItem);

        if (_trayIcon != null)
        {
            _trayIcon.Menu = _contextMenu;
        }
    }

    private void TakeScreenshot()
    {
        try
        {
            // Get overlay service and show capture UI
            var overlayService = App.Services?.GetService(typeof(AGI.Captor.Desktop.Services.Overlay.IOverlayController))
                as AGI.Captor.Desktop.Services.Overlay.IOverlayController;
            overlayService?.ShowAll();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start screenshot capture");
            ShowNotification("Error", "Failed to start screenshot capture");
        }
    }

    private void ShowAbout()
    {
        try
        {
            // Ensure UI operations run on the UI thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    AboutDialog.ShowWindow();
                    Log.Debug("About dialog opened from system tray");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to show About dialog on UI thread");

                    // Fallback to notification if dialog fails
                    try
                    {
                        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                        ShowNotification("AGI Captor", $"Version {version}\nScreenshot and annotation tool");
                    }
                    catch (Exception fallbackEx)
                    {
                        Log.Error(fallbackEx, "Failed to show About notification fallback");
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to dispatch About dialog to UI thread");
        }
    }

    public void ShowNotification(string title, string message)
    {
        // Start the notification process asynchronously
        _ = ShowNotificationAsync(title, message);
    }

    private async System.Threading.Tasks.Task ShowNotificationAsync(string title, string message)
    {
        try
        {
            // Log the notification with higher priority
            Log.Information("🔔 NOTIFICATION: {Title} - {Message}", title, message);

            // Ensure UI operations run on the UI thread
            if (_trayIcon != null)
            {
                await ShowNotificationOnUIThread(title, message);
            }
            else
            {
                Log.Warning("Cannot show notification: tray icon is null");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show notification: {Title} - {Message}", title, message);
        }
    }

    private async System.Threading.Tasks.Task ShowNotificationOnUIThread(string title, string message)
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (_trayIcon != null)
                {
                    // Store original tooltip
                    var originalTooltip = _trayIcon.ToolTipText;
                    
                    // Show notification with more prominent display
                    var notificationText = $"🔔 {title}\n{message}";
                    _trayIcon.ToolTipText = notificationText;
                    
                    Log.Debug("Tray icon tooltip updated to: {NotificationText}", notificationText);
                    
                    // Start the reset process asynchronously
                    _ = ResetTooltipAfterDelay(originalTooltip ?? "AGI Captor - Screenshot Tool");
                }
                else
                {
                    Log.Warning("Tray icon is null when trying to show notification");
                }
                
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update tray icon tooltip on UI thread");
                tcs.SetException(ex);
            }
        });

        await tcs.Task;
    }

    private async System.Threading.Tasks.Task ResetTooltipAfterDelay(string originalTooltip)
    {
        try
        {
            // Wait for the notification to be visible
            await System.Threading.Tasks.Task.Delay(8000);

            // Reset tooltip on UI thread
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (_trayIcon != null)
                    {
                        _trayIcon.ToolTipText = originalTooltip;
                        Log.Debug("Tray icon tooltip reset to original");
                    }
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to reset tray icon tooltip");
                    tcs.SetException(ex);
                }
            });

            await tcs.Task;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reset tooltip after delay");
        }
    }
}
