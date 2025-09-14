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
                    statusText.Text = "权限状态：已授予";
                    statusText.Foreground = Avalonia.Media.Brushes.Green;
                    
                    // 权限已授予，可以关闭对话框
                    Dispatcher.UIThread.Post(async () =>
                    {
                        await Task.Delay(1000); // 显示成功状态1秒
                        Close(true); // 返回true表示权限已授予
                    });
                }
                else
                {
                    statusIcon.Text = "❌";
                    statusText.Text = "权限状态：未授予";
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
        var dialog = new AccessibilityPermissionDialog();
        
        if (parent != null)
        {
            return await dialog.ShowDialog<bool>(parent);
        }
        else
        {
            dialog.Show();
            
            // 创建TaskCompletionSource来等待对话框关闭
            var tcs = new TaskCompletionSource<bool>();
            
            dialog.Closed += (_, _) =>
            {
                // 检查最终权限状态
                var hasPermission = MacHotkeyProvider.HasAccessibilityPermissions;
                tcs.SetResult(hasPermission);
            };
            
            return await tcs.Task;
        }
    }
}

