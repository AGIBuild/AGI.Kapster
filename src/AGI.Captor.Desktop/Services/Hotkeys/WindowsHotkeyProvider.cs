using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using SharpHook;
using SharpHook.Native;
using Serilog;

namespace AGI.Captor.Desktop.Services.Hotkeys;

/// <summary>
/// Windows hotkey provider using SharpHook global hook
/// </summary>
public class WindowsHotkeyProvider : IHotkeyProvider, IDisposable
{
    private readonly Dictionary<string, (HotkeyModifiers modifiers, uint keyCode, Action callback)> _registeredHotkeys = new();
    private TaskPoolGlobalHook? _hook;
    private bool _disposed = false;
    private readonly object _lockObject = new();
    private HotkeyModifiers _currentModifiers = HotkeyModifiers.None;

    public bool IsSupported => OperatingSystem.IsWindows();
    public bool HasPermissions => true; // SharpHook handles permissions

    public WindowsHotkeyProvider()
    {
        if (!IsSupported)
        {
            Log.Warning("WindowsHotkeyProvider created on non-Windows platform");
            return;
        }

        InitializeHook();
    }

    private void InitializeHook()
    {
        try
        {
            _hook = new TaskPoolGlobalHook();
            _hook.KeyPressed += OnKeyPressed;
            _hook.KeyReleased += OnKeyReleased;

            // Start the global hook
            Task.Run(async () =>
            {
                try
                {
                    await _hook.RunAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error running SharpHook global hook");
                }
            });

            Log.Debug("SharpHook global hook initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize SharpHook global hook");
        }
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        try
        {
            var keyName = e.Data.KeyCode.ToString();

            // Do not trigger on pure modifier keys
            if (IsModifierKeyName(keyName, out var modifierFlag))
            {
                lock (_lockObject)
                {
                    _currentModifiers |= modifierFlag;
                }
                return;
            }

            // Non-modifier: combine with current modifiers and match
            uint vk = MapToWindowsVkFromName(keyName);
            if (vk == 0)
            {
                return;
            }

            HotkeyModifiers modifiers;
            lock (_lockObject)
            {
                modifiers = _currentModifiers;
            }

            lock (_lockObject)
            {
                foreach (var (id, (registeredModifiers, registeredKeyCode, callback)) in _registeredHotkeys)
                {
                    if (vk == registeredKeyCode && modifiers == registeredModifiers)
                    {
                        Log.Debug("Hotkey matched: {Id} -> {Modifiers}+{KeyCode}", id, modifiers, vk);

                        // Execute callback on UI thread
                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            try
                            {
                                callback();
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Error executing hotkey callback: {Id}", id);
                            }
                        });

                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in SharpHook key pressed handler");
        }
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        try
        {
            var keyName = e.Data.KeyCode.ToString();
            if (IsModifierKeyName(keyName, out var modifierFlag))
            {
                lock (_lockObject)
                {
                    _currentModifiers &= ~modifierFlag;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in SharpHook key released handler");
        }
    }

    private static bool IsModifierKeyName(string keyName, out HotkeyModifiers flag)
    {
        flag = HotkeyModifiers.None;

        if (string.IsNullOrEmpty(keyName)) return false;
        if (keyName is "VcLeftControl" or "VcRightControl") { flag = HotkeyModifiers.Control; return true; }
        if (keyName is "VcLeftAlt" or "VcRightAlt") { flag = HotkeyModifiers.Alt; return true; }
        if (keyName is "VcLeftShift" or "VcRightShift") { flag = HotkeyModifiers.Shift; return true; }
        if (keyName is "VcLeftMeta" or "VcRightMeta") { flag = HotkeyModifiers.Win; return true; }
        return false;
    }

    private static uint MapToWindowsVkFromName(string keyName)
    {
        if (string.IsNullOrEmpty(keyName)) return 0;

        // Letters A-Z
        if (keyName.Length == 3 && keyName.StartsWith("Vc", StringComparison.Ordinal))
        {
            char c = keyName[2];
            if (c >= 'A' && c <= 'Z') return (uint)c;
            if (c >= '0' && c <= '9') return (uint)c;
        }

        // Numpad 0-9
        if (keyName.StartsWith("VcNumPad", StringComparison.Ordinal) && keyName.Length == 9)
        {
            char d = keyName[8];
            if (d >= '0' && d <= '9') return (uint)(0x60 + (d - '0'));
        }

        // Function keys F1-F24
        if (keyName.StartsWith("VcF", StringComparison.Ordinal))
        {
            if (int.TryParse(keyName.AsSpan(3), out var fn) && fn >= 1 && fn <= 24)
                return (uint)(0x70 + (fn - 1));
        }

        return keyName switch
        {
            // Whitespace and control
            "VcSpace" => 0x20,
            "VcEnter" => 0x0D,
            "VcTab" => 0x09,
            "VcBackspace" => 0x08,
            "VcEscape" => 0x1B,

            // Arrows
            "VcUp" => 0x26,
            "VcDown" => 0x28,
            "VcLeft" => 0x25,
            "VcRight" => 0x27,

            // Home/End/Page
            "VcHome" => 0x24,
            "VcEnd" => 0x23,
            "VcPageUp" => 0x21,
            "VcPageDown" => 0x22,
            "VcInsert" => 0x2D,
            "VcDelete" => 0x2E,

            // Symbols row
            "VcMinus" => 0xBD,
            "VcEquals" => 0xBB,
            "VcBracketLeft" => 0xDB,
            "VcBracketRight" => 0xDD,
            "VcBackslash" => 0xDC,
            "VcSemicolon" => 0xBA,
            "VcQuote" => 0xDE,
            "VcComma" => 0xBC,
            "VcPeriod" => 0xBE,
            "VcSlash" => 0xBF,
            "VcGrave" => 0xC0,

            // Numpad operations
            "VcNumPadAdd" => 0x6B,
            "VcNumPadSubtract" => 0x6D,
            "VcNumPadMultiply" => 0x6A,
            "VcNumPadDivide" => 0x6F,
            "VcNumPadDecimal" => 0x6E,

            // Print/Scroll/Pause
            "VcPrintScreen" => 0x2C,
            "VcScrollLock" => 0x91,
            "VcPause" => 0x13,

            _ => 0
        };
    }

    public bool RegisterHotkey(string id, HotkeyModifiers modifiers, uint keyCode, Action callback)
    {
        if (!IsSupported)
        {
            Log.Warning("Windows hotkeys not supported on this platform");
            return false;
        }

        if (string.IsNullOrEmpty(id) || callback == null)
        {
            Log.Warning("Invalid hotkey registration parameters");
            return false;
        }

        if (_hook == null)
        {
            Log.Error("SharpHook not initialized, cannot register hotkey");
            return false;
        }

        try
        {
            lock (_lockObject)
            {
                // Remove existing if present
                if (_registeredHotkeys.ContainsKey(id))
                {
                    _registeredHotkeys.Remove(id);
                    Log.Debug("Removed existing hotkey: {Id}", id);
                }

                // Register new hotkey
                _registeredHotkeys[id] = (modifiers, keyCode, callback);
                Log.Debug("✅ SharpHook hotkey registered: {Id} -> {Modifiers}+{KeyCode}", id, modifiers, keyCode);
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception registering SharpHook hotkey: {Id}", id);
            return false;
        }
    }

    public bool UnregisterHotkey(string id)
    {
        try
        {
            lock (_lockObject)
            {
                if (_registeredHotkeys.Remove(id))
                {
                    Log.Debug("SharpHook hotkey unregistered: {Id}", id);
                    return true;
                }
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception unregistering SharpHook hotkey: {Id}", id);
            return false;
        }
    }

    public void UnregisterAll()
    {
        try
        {
            lock (_lockObject)
            {
                var count = _registeredHotkeys.Count;
                _registeredHotkeys.Clear();
                Log.Debug("All SharpHook hotkeys unregistered: {Count}", count);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error unregistering all SharpHook hotkeys");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        try
        {
            // 注销所有热键
            UnregisterAll();

            // 停止并释放SharpHook
            if (_hook != null)
            {
                _hook.KeyPressed -= OnKeyPressed;
                _hook.Dispose();
                _hook = null;
                Log.Debug("SharpHook disposed");
            }

            Log.Debug("WindowsHotkeyProvider disposed");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error disposing WindowsHotkeyProvider");
        }
    }
}