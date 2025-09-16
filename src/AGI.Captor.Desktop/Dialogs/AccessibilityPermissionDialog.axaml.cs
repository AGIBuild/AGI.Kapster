using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AGI.Captor.Desktop.Services.Hotkeys;
using Serilog;

namespace AGI.Captor.Desktop.Dialogs;

public partial class AccessibilityPermissionDialog : Window
{
    private readonly DispatcherTimer _statusTimer;
    
    public AccessibilityPermissionDialog()
    {
        InitializeComponent();
        
        // Display current application path
        try
        {
            var appPath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(appPath))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var pathText = this.FindControl<TextBlock>("CurrentAppPathText");
                    if (pathText != null)
                    {
                        pathText.Text = $"• {appPath}";
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get application path");
        }
        
        // 创建定时器定期检查权限状态
        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _statusTimer.Tick += OnStatusTimerTick;
        
        // 初始状态检查
        UpdatePermissionStatus();
        
        // 开始定时检查
        _statusTimer.Start();
    }
    
    private void OnStatusTimerTick(object? sender, EventArgs e)
    {
        UpdatePermissionStatus();
    }
    
    private void UpdatePermissionStatus()
    {
        try
        {
            var hasPermission = MacHotkeyProvider.HasAccessibilityPermissions;
            
            var statusIcon = this.FindControl<TextBlock>("PermissionStatusIcon");
            var statusText = this.FindControl<TextBlock>("PermissionStatusText");
            
            if (statusIcon != null && statusText != null)
            {
                if (hasPermission)
                {
                    statusIcon.Text = "✅";
                    statusText.Text = "Permission Status: Granted";
                    statusText.Foreground = Avalonia.Media.Brushes.Green;
                    
                }
                else
                {
                    statusIcon.Text = "❌";
                    statusText.Text = "Permission Status: Not Granted";
                    statusText.Foreground = Avalonia.Media.Brushes.Red;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to check accessibility permissions");
        }
    }
    
    private void RefreshPermissionStatus(object? sender, RoutedEventArgs e)
    {
        UpdatePermissionStatus();
    }
    
    private void OpenSystemPreferences(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                // 尝试直接打开辅助功能设置页面
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "open",
                        Arguments = "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                Log.Debug("Opened system preferences for accessibility");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open system preferences");
            
            // 备用方案：打开通用系统设置
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "open",
                        Arguments = "/System/Applications/System Preferences.app",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
            }
            catch (Exception ex2)
            {
                Log.Error(ex2, "Failed to open system preferences as fallback");
            }
        }
    }
    
    private void CloseDialog(object? sender, RoutedEventArgs e)
    {
        Close(false); // 返回false表示用户选择稍后设置
    }
    
    protected override void OnClosed(EventArgs e)
    {
        _statusTimer?.Stop();
        base.OnClosed(e);
    }
    
    /// <summary>
    /// 显示权限对话框并等待用户操作
    /// </summary>
    /// <param name="parent">父窗口</param>
    /// <returns>true表示权限已授予，false表示用户选择稍后设置</returns>
    public static async Task<bool> ShowAsync(Window? parent = null)
    {
        if (parent == null)
        {
            throw new ArgumentNullException(nameof(parent), "Parent window must be provided for a modal dialog.");
        }

        var dialog = new AccessibilityPermissionDialog();
        
        return await dialog.ShowDialog<bool>(parent);
    }
}

