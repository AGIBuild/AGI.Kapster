using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Serilog;

namespace AGI.Captor.Desktop.Services.Hotkeys;

/// <summary>
/// macOS平台热键提供者
/// </summary>
public class MacHotkeyProvider : IHotkeyProvider
{
    private readonly Dictionary<string, (uint keyCode, HotkeyModifiers modifiers, Action callback)> _registeredHotkeys = new();
    private IntPtr _eventTap = IntPtr.Zero;
    private IntPtr _runLoopSource = IntPtr.Zero;
    private CGEventTapCallBack _eventTapCallback = null!;
    private bool _disposed = false;

    // CGEventTap constants - based on working example configuration
    private const int kCGEventKeyDown = 10;
    private const int kCGHIDEventTap = 0;           // Prefer HID layer
    private const int kCGSessionEventTap = 1;       // Fallback to Session layer
    private const int kCGHeadInsertEventTap = 0;
    private const int kCGEventTapOptionListenOnly = 1; // Listen only, don't intercept
    private const int kCGKeyboardEventKeycode = 9;
    private const uint kCFStringEncodingUTF8 = 0x08000100;
    
    // Modifier key masks - consistent with example code
    private const ulong FLAG_SHIFT = 0x20000;
    private const ulong FLAG_CONTROL = 0x40000;
    private const ulong FLAG_OPTION = 0x80000;
    private const ulong FLAG_COMMAND = 0x100000;
    private const ulong PRIMARY_MASK = FLAG_SHIFT | FLAG_CONTROL | FLAG_OPTION | FLAG_COMMAND;

    // P/Invoke declarations - complete declarations based on working example
    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern bool AXIsProcessTrusted();

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGEventTapCreate(int tap, int place, int options, ulong eventsOfInterest, CGEventTapCallBack callback, IntPtr userInfo);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGEventTapEnable(IntPtr tap, bool enable);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr machPort, IntPtr order);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CFRunLoopGetCurrent();

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CFRunLoopAddSource(IntPtr rl, IntPtr source, IntPtr mode);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern long CGEventGetIntegerValueField(IntPtr cgEvent, int field);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern ulong CGEventGetFlags(IntPtr cgEvent);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string cStr, uint encoding);

    private delegate IntPtr CGEventTapCallBack(IntPtr proxy, int type, IntPtr eventRef, IntPtr userInfo);

    public bool IsSupported => OperatingSystem.IsMacOS();
    public bool HasPermissions => OperatingSystem.IsMacOS() && AXIsProcessTrusted();

    /// <summary>
    /// 静态方法检查macOS辅助功能权限
    /// </summary>
    public static bool HasAccessibilityPermissions => OperatingSystem.IsMacOS() && AXIsProcessTrusted();

    /// <summary>
    /// 获取当前应用程序的路径信息，用于调试权限问题
    /// </summary>
    public static string GetCurrentApplicationPath()
    {
        try
        {
            var appPath = System.AppContext.BaseDirectory;
            var bundlePath = Environment.GetEnvironmentVariable("CFBundleExecutablePath") ?? "Not set";
            
            // 尝试获取进程路径，但不依赖可能缺失的程序集
            string processPath = "Unknown";
            try
            {
                var process = System.Diagnostics.Process.GetCurrentProcess();
                processPath = process.ProcessName ?? "Unknown";
            }
            catch
            {
                processPath = "Process info unavailable";
            }
            
            return $"Process: {processPath}\nAppContext: {appPath}\nBundle: {bundlePath}";
        }
        catch (Exception ex)
        {
            return $"Error getting path: {ex.Message}";
        }
    }

    public MacHotkeyProvider()
    {
        if (!IsSupported)
        {
            Log.Warning("MacHotkeyProvider created on non-macOS platform");
            return;
        }

        _eventTapCallback = EventTapCallbackMethod;
        
        // 详细的权限调试信息
        var hasPermissions = AXIsProcessTrusted();
        var appPath = System.AppContext.BaseDirectory;
        
        Log.Debug("MacHotkeyProvider created - HasPermissions: {HasPermissions}, AppPath: {AppPath}", 
            hasPermissions, appPath);
        
        if (!hasPermissions)
        {
            Log.Warning("⚠️ Accessibility permissions not granted for app at: {AppPath}", appPath);
            Log.Warning("Please add this application to System Preferences > Security & Privacy > Accessibility");
        }
        
        // 延迟初始化CGEventTap，避免在构造函数中阻塞
    }

    private void InitializeEventTap()
    {
        if (_eventTap != IntPtr.Zero) return; // 避免重复初始化

        if (!HasPermissions)
        {
            Log.Warning("No accessibility permissions for CGEventTap");
            return;
        }

        Log.Debug("Initializing CGEventTap based on working example...");

        // 完全按照成功示例的实现
        ulong eventMask = 1UL << kCGEventKeyDown; // 仅监听KeyDown

        // 优先尝试HID层，失败时回退到Session层（与示例一致）
        _eventTap = CGEventTapCreate(
            kCGHIDEventTap,
            kCGHeadInsertEventTap,
            kCGEventTapOptionListenOnly,
            eventMask,
            _eventTapCallback,
            IntPtr.Zero);

        if (_eventTap == IntPtr.Zero)
        {
            Log.Debug("Primary HID tap failed; retrying session tap...");
            _eventTap = CGEventTapCreate(
                kCGSessionEventTap,
                kCGHeadInsertEventTap,
                kCGEventTapOptionListenOnly,
                eventMask,
                _eventTapCallback,
                IntPtr.Zero);
        }

        if (_eventTap == IntPtr.Zero)
        {
            Log.Error("Failed to create event tap. Grant Accessibility + Input Monitoring permissions.");
            return;
        }

        Log.Debug("CGEventTap created successfully");

        // 按照成功示例创建RunLoop源
        _runLoopSource = CFMachPortCreateRunLoopSource(IntPtr.Zero, _eventTap, IntPtr.Zero);
        if (_runLoopSource == IntPtr.Zero)
        {
            Log.Error("Failed to create run loop source");
            return;
        }

        // 关键修复：必须在主线程的RunLoop中添加源（成功示例在主线程运行）
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var currentRunLoop = CFRunLoopGetCurrent(); // 在UI线程中获取当前RunLoop
                var defaultMode = CFStringCreateWithCString(IntPtr.Zero, "kCFRunLoopDefaultMode", kCFStringEncodingUTF8);
                
                CFRunLoopAddSource(currentRunLoop, _runLoopSource, defaultMode);
                CGEventTapEnable(_eventTap, true);
                
                Log.Debug("CGEventTap enabled and added to UI thread RunLoop");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to add CGEventTap to UI thread RunLoop");
            }
        });

        Log.Debug("CGEventTap enabled and added to current RunLoop");
    }

    public bool RegisterHotkey(string id, HotkeyModifiers modifiers, uint keyCode, Action callback)
    {
        if (!IsSupported)
        {
            Log.Warning("macOS hotkeys not supported on this platform");
            return false;
        }

        if (!HasPermissions)
        {
            Log.Warning("No accessibility permissions for hotkey registration");
            return false;
        }

        if (string.IsNullOrEmpty(id) || callback == null)
        {
            Log.Warning("Invalid hotkey registration parameters");
            return false;
        }

        try
        {
            // 如果是第一次注册热键，初始化CGEventTap
            if (_eventTap == IntPtr.Zero)
            {
                InitializeEventTap();
            }

            // 转换为macOS虚拟键码
            var macKeyCode = WindowsToMacKeyCode(keyCode);
            if (macKeyCode == uint.MaxValue)
            {
                Log.Warning("Unsupported key code: 0x{KeyCode:X}", keyCode);
                return false;
            }

            _registeredHotkeys[id] = (macKeyCode, modifiers, callback);
            Log.Debug("macOS hotkey registered: {Id} -> {Modifiers}+{KeyCode} (mac: {MacKeyCode})", 
                id, modifiers, keyCode, macKeyCode);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception registering macOS hotkey: {Id}", id);
            return false;
        }
    }

    public bool UnregisterHotkey(string id)
    {
        if (_registeredHotkeys.Remove(id))
        {
            Log.Debug("macOS hotkey unregistered: {Id}", id);
            return true;
        }
        return false;
    }

    public void UnregisterAll()
    {
        var count = _registeredHotkeys.Count;
        _registeredHotkeys.Clear();
        Log.Debug("Unregistered all macOS hotkeys: {Count}", count);
    }

    private IntPtr EventTapCallbackMethod(IntPtr proxy, int type, IntPtr cgEvent, IntPtr userInfo)
    {
        try
        {
            if (type == kCGEventKeyDown)
            {
                var keyCode = (uint)CGEventGetIntegerValueField(cgEvent, kCGKeyboardEventKeycode);
                var flags = CGEventGetFlags(cgEvent);
                var normalizedFlags = flags & PRIMARY_MASK; // Normalize modifier keys, remove irrelevant bits

                // Check if matches registered hotkeys
                foreach (var (id, (registeredKeyCode, registeredModifiers, callback)) in _registeredHotkeys)
                {
                    var requiredFlags = ConvertHotkeyModifiersToFlags(registeredModifiers);
                    
                    if (keyCode == registeredKeyCode && normalizedFlags == requiredFlags)
                    {
                        Log.Information("Hotkey triggered: {Id}", id);
                        
                        // Execute callback on UI thread
                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            try
                            {
                                callback();
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Hotkey '{Id}' error: {Message}", id, ex.Message);
                            }
                        });
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in CGEventTap callback");
        }

        return cgEvent; // Don't intercept event, let it continue
    }

    /// <summary>
    /// Convert HotkeyModifiers to CGEvent flags
    /// </summary>
    private static ulong ConvertHotkeyModifiersToFlags(HotkeyModifiers modifiers)
    {
        ulong flags = 0;
        
        if ((modifiers & HotkeyModifiers.Control) != 0) flags |= FLAG_CONTROL;
        if ((modifiers & HotkeyModifiers.Alt) != 0) flags |= FLAG_OPTION;
        if ((modifiers & HotkeyModifiers.Shift) != 0) flags |= FLAG_SHIFT;
        if ((modifiers & HotkeyModifiers.Win) != 0) flags |= FLAG_COMMAND;
        
        return flags;
    }

    /// <summary>
    /// Convert CGEvent flags to HotkeyModifiers (kept for debugging)
    /// </summary>
    private static HotkeyModifiers ConvertCGEventFlagsToHotkeyModifiers(ulong flags)
    {
        var modifiers = HotkeyModifiers.None;
        
        if ((flags & FLAG_CONTROL) != 0) modifiers |= HotkeyModifiers.Control;
        if ((flags & FLAG_OPTION) != 0) modifiers |= HotkeyModifiers.Alt;
        if ((flags & FLAG_SHIFT) != 0) modifiers |= HotkeyModifiers.Shift;
        if ((flags & FLAG_COMMAND) != 0) modifiers |= HotkeyModifiers.Win;
        
        return modifiers;
    }

    private static uint WindowsToMacKeyCode(uint windowsKeyCode)
    {
        // Windows虚拟键码到macOS虚拟键码的映射
        return windowsKeyCode switch
        {
            0x41 => 0x00, // A
            0x53 => 0x01, // S
            0x44 => 0x02, // D
            0x46 => 0x03, // F
            0x48 => 0x04, // H
            0x47 => 0x05, // G
            0x5A => 0x06, // Z
            0x58 => 0x07, // X
            0x43 => 0x08, // C
            0x56 => 0x09, // V
            0x42 => 0x0B, // B
            0x51 => 0x0C, // Q
            0x57 => 0x0D, // W
            0x45 => 0x0E, // E
            0x52 => 0x0F, // R
            0x59 => 0x10, // Y
            0x54 => 0x11, // T
            0x31 => 0x12, // 1
            0x32 => 0x13, // 2
            0x33 => 0x14, // 3
            0x34 => 0x15, // 4
            0x36 => 0x16, // 6
            0x35 => 0x17, // 5
            0x37 => 0x1A, // 7
            0x38 => 0x1C, // 8
            0x39 => 0x19, // 9
            0x30 => 0x1D, // 0
            0x20 => 0x31, // Space
            0x1B => 0x35, // Escape
            _ => uint.MaxValue // 不支持的键码
        };
    }

    public void Dispose()
    {
        if (_disposed) return;

        UnregisterAll();

        if (_eventTap != IntPtr.Zero)
        {
            CGEventTapEnable(_eventTap, false);
            _eventTap = IntPtr.Zero;
        }

        _disposed = true;
        Log.Debug("MacHotkeyProvider disposed");
    }
}