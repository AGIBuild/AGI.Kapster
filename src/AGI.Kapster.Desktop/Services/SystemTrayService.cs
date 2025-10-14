using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using AGI.Kapster.Desktop.Dialogs;
using Serilog;
using System.Threading.Tasks;
using Avalonia.Layout;
using Avalonia.Media;
using AGI.Kapster.Desktop.Models;
using Microsoft.Extensions.DependencyInjection;
using AGI.Kapster.Desktop.Services.Settings;
using AGI.Kapster.Desktop.Services.Update;
using AGI.Kapster.Desktop.Models.Update;

namespace AGI.Kapster.Desktop.Services;

/// <summary>
/// System tray service implementation
/// </summary>
public class SystemTrayService : ISystemTrayService, IDisposable
{
    private readonly INotificationService _notificationService;
    private readonly IUpdateService _updateService;
    private readonly AppSettings _appSettings;
    private TrayIcon? _trayIcon;
    private NativeMenu? _contextMenu;

    public event EventHandler? OpenSettingsRequested;
    public event EventHandler? ExitRequested;

    public SystemTrayService()
    {
        var services = App.Services ?? throw new InvalidOperationException("Service container not initialized");

        _notificationService = services.GetRequiredService<INotificationService>();
        _updateService = services.GetRequiredService<IUpdateService>();
        var settingsService = services.GetService<ISettingsService>();
        _appSettings = settingsService?.Settings ?? new AppSettings();

        _updateService.UpdateAvailable += OnUpdateAvailable;
    }

    public void Initialize()
    {
        try
        {
            CreateTrayIcon();
            CreateContextMenu();
            Log.Debug("System tray initialized");

            // Start background checking if enabled and not already running
            if (_updateService.IsAutoUpdateEnabled && !_updateService.IsBackgroundCheckingActive)
            {
                _updateService.StartBackgroundChecking();
                Log.Information("Background update checking started from SystemTrayService");
            }
            else if (_updateService.IsBackgroundCheckingActive)
            {
                Log.Debug("Background update checking already active, skipping start");
            }
            else
            {
                Log.Debug("Auto-update disabled, skipping background checking start");
            }

            if (_updateService.PendingInstallerPath is { } pending && _appSettings.General.ShowNotifications)
            {
                _ = NotifyUpdateReadyAsync(pending, null);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize system tray");
            throw;
        }
    }

    private async Task NotifyUpdateReadyAsync(string installerPath, UpdateInfo? updateInfo)
    {
        try
        {
            var versionLabel = updateInfo?.Version ?? Path.GetFileNameWithoutExtension(installerPath);

            var installNow = new NotificationButton(
                "install-now",
                "Install Now",
                async token =>
                {
                    if (_updateService.PendingInstallerPath is not { } path)
                    {
                        return;
                    }

                    var success = await _updateService.InstallUpdateAsync(path);
                    if (success)
                    {
                        _updateService.ClearPendingInstaller();
                    }
                });

            var later = new NotificationButton(
                "install-later",
                "Later",
                token => Task.CompletedTask);

            await _notificationService.ShowAsync(new NotificationRequest(
                Title: "Update Ready",
                Message: "AGI Kapster has a new update ready to install. Proceed now?",
                Buttons: new[] { installNow, later },
                IsAutoDismissEnabled: false
            ));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to notify update ready state");
        }
    }

    private async void OnUpdateAvailable(object? sender, UpdateAvailableEventArgs e)
    {
        try
        {
            if (IsMacOS())
            {
                await ShowMacManualUpdatePromptAsync(e.UpdateInfo);
                return;
            }

            var downloadSuccess = await _updateService.DownloadUpdateAsync(e.UpdateInfo);
            if (!downloadSuccess)
            {
                return;
            }

            if (_updateService.PendingInstallerPath is { } pending)
            {
                if (!_appSettings.General.ShowNotifications)
                {
                    return;
                }

                await NotifyUpdateReadyAsync(pending, e.UpdateInfo);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to handle update availability");
        }
    }


    private async Task ShowMacManualUpdatePromptAsync(UpdateInfo updateInfo)
    {
        try
        {
            if (!_appSettings.General.ShowNotifications)
            {
                return;
            }

            var releaseUrl = !string.IsNullOrWhiteSpace(updateInfo.ReleaseUrl)
                ? updateInfo.ReleaseUrl
                : $"https://github.com/AGIBuild/AGI.Kapster/releases/tag/v{updateInfo.Version}";

            var openDownload = new NotificationButton(
                "open-download",
                "Open Download Page",
                _ => OpenUrlAsync(releaseUrl));

            var instructions = "macOS builds require manual installation. Download the latest package, move it to Applications, then run 'xattr -dr com.apple.quarantine /Applications/AGI Kapster.app'.";

            await _notificationService.ShowAsync(new NotificationRequest(
                Title: "Update Available",
                Message: instructions,
                Buttons: new[] { openDownload },
                IsAutoDismissEnabled: false));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to show macOS manual update instructions");
        }
    }

    private static bool IsMacOS() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    private static Task OpenUrlAsync(string url)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open URL: {Url}", url);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _updateService.UpdateAvailable -= OnUpdateAvailable;
        _updateService.StopBackgroundChecking();
    }

    private void CreateTrayIcon()
    {
        _trayIcon = new TrayIcon
        {
            IsVisible = true,
            ToolTipText = "AGI Kapster - Screenshot Tool"
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
                using var stream = AssetLoader.Open(new Uri("avares://AGI.Kapster.Desktop/logo.ico"));
                return new WindowIcon(stream);
            }
            catch
            {
                Log.Warning("logo.ico not found in assets, trying alternative paths");
            }

            // Try alternative asset paths
            var assetPaths = new[]
            {
                "avares://AGI.Kapster.Desktop/Assets/logo.ico",
                "avares://AGI.Kapster.Desktop/Assets/icon.ico",
                "avares://AGI.Kapster.Desktop/icon.ico"
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
            ToolTip = "About AGI Kapster"
        };
        aboutItem.Click += (s, e) => ShowAbout();
        _contextMenu.Add(aboutItem);

        // Separator
        _contextMenu.Add(new NativeMenuItemSeparator());

        // Exit menu item
        var exitItem = new NativeMenuItem("Exit")
        {
            ToolTip = "Exit AGI Kapster"
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
            var overlayService = App.Services?.GetService(typeof(AGI.Kapster.Desktop.Services.Overlay.IOverlayController))
                as AGI.Kapster.Desktop.Services.Overlay.IOverlayController;
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
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    AboutDialog.ShowDialogWindow(null);
                    Log.Debug("About dialog opened from system tray");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to show About dialog on UI thread");

                    try
                    {
                        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                        ShowNotification("AGI Kapster", $"Version {version}\nScreenshot and annotation tool");
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
        try
        {
            Log.Debug("Showing notification: {Title} - {Message}", title, message);

            if (!_appSettings.General.ShowNotifications)
            {
                Log.Debug("Notifications disabled by user settings");
                return;
            }

            _notificationService.ShowAsync(new NotificationRequest(title, message, IsAutoDismissEnabled: true)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show notification: {Title} - {Message}", title, message);
        }
    }

    /// <summary>
    /// Show an install confirmation prompt to the user. Attempts to show a modal dialog on the UI thread.
    /// Falls back to a notification and returns false if user interaction isn't available.
    /// </summary>
    /// <param name="installerPath">Path to the installer file (used for display only)</param>
    /// <returns>True if user agreed to install</returns>
    public async Task<bool> ShowInstallConfirmationAsync(string installerPath)
    {
        try
        {
            if (Avalonia.Application.Current != null && Avalonia.Threading.Dispatcher.UIThread != null)
            {
                var tcs = new TaskCompletionSource<bool>();

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        var window = new Window
                        {
                            Width = 420,
                            Height = 160,
                            CanResize = false,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            Title = "Install Update",
                        };

                        var panel = new StackPanel
                        {
                            Margin = new Thickness(12),
                            Orientation = Orientation.Vertical
                        };

                        var textBlock = new TextBlock
                        {
                            Text = $"An update installer was downloaded:\n{Path.GetFileName(installerPath)}\n\nDo you want to install it now?",
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(4)
                        };

                        var buttons = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Margin = new Thickness(4)
                        };

                        var yes = new Button { Content = "Install", IsDefault = true, Width = 90, Margin = new Thickness(4) };
                        var no = new Button { Content = "Later", IsCancel = true, Width = 90, Margin = new Thickness(4) };

                        yes.Click += (_, _) =>
                        {
                            tcs.TrySetResult(true);
                            window.Close();
                        };

                        no.Click += (_, _) =>
                        {
                            tcs.TrySetResult(false);
                            window.Close();
                        };

                        buttons.Children.Add(yes);
                        buttons.Children.Add(no);

                        panel.Children.Add(textBlock);
                        panel.Children.Add(buttons);

                        window.Content = panel;


                        // Determine owner window if available
                        Avalonia.Controls.Window? mainWindow = null;
                        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
                        {
                            mainWindow = desktop.MainWindow;
                        }

                        window.Closed += (_, _) => tcs.TrySetResult(false);

                        // Show modal when owner available, otherwise show non-modal window
                        if (mainWindow != null)
                        {
                            window.ShowDialog(mainWindow);
                        }
                        else
                        {
                            window.Show();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to show install confirmation dialog");
                        tcs.TrySetResult(false);
                    }
                });

                var result = await tcs.Task.ConfigureAwait(false);
                return result;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error showing install confirmation dialog");
        }

        // Fallback: notify via tray and decline
        try
        {
            ShowNotification("Update Ready to Install", $"An update was downloaded: {Path.GetFileName(installerPath)}. Open the app to install later.");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to show tray fallback for install confirmation");
        }

        return false;
    }
}

