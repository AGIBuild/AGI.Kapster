using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Serilog;

namespace AGI.Captor.App.Services;

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
                using var stream = AssetLoader.Open(new Uri("avares://AGI.Captor.App/logo.ico"));
                return new WindowIcon(stream);
            }
            catch
            {
                Log.Warning("logo.ico not found in assets, trying alternative paths");
            }
            
            // Try alternative asset paths
            var assetPaths = new[]
            {
                "avares://AGI.Captor.App/Assets/logo.ico",
                "avares://AGI.Captor.App/Assets/icon.ico",
                "avares://AGI.Captor.App/icon.ico"
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
            var overlayService = App.Services?.GetService(typeof(AGI.Captor.App.Services.Overlay.IOverlayController)) 
                as AGI.Captor.App.Services.Overlay.IOverlayController;
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
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            ShowNotification("AGI Captor", $"Version {version}\nScreenshot and annotation tool");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show about information");
        }
    }

    public void ShowNotification(string title, string message)
    {
        try
        {
            if (_trayIcon != null)
            {
                // For now, log the notification
                // In a full implementation, you might use Windows Toast notifications
                Log.Debug("Notification: {Title} - {Message}", title, message);
                
                // Update tooltip temporarily to show notification
                var originalTooltip = _trayIcon.ToolTipText;
                _trayIcon.ToolTipText = $"{title}: {message}";
                
                // Reset tooltip after a delay
                System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ =>
                {
                    if (_trayIcon != null)
                    {
                        _trayIcon.ToolTipText = originalTooltip;
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show notification: {Title} - {Message}", title, message);
        }
    }
}
