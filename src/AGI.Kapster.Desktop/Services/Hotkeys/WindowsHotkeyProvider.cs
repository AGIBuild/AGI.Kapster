using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

using Avalonia.Threading;
using SharpHook;
using SharpHook.Data;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Hotkeys;

/// <summary>
/// Windows hotkey provider using SharpHook global hook.
/// </summary>
public class WindowsHotkeyProvider : IHotkeyProvider, IDisposable
{
    private readonly object _lockObject = new();

    // Hotkey lookup (O(1))
    private readonly Dictionary<(HotkeyModifiers Modifiers, uint KeyCode), Action> _hotkeysByChord = new();
    private readonly Dictionary<string, (HotkeyModifiers Modifiers, uint KeyCode)> _chordById = new();

    private EventLoopGlobalHook? _hook;
    private readonly TaskCompletionSource _runRequestedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool _disposed = false;
    private HotkeyModifiers _currentModifiers = HotkeyModifiers.None;

    private static readonly IReadOnlyDictionary<KeyCode, HotkeyModifiers> ModifierByKeyCode = new Dictionary<KeyCode, HotkeyModifiers>
    {
        { KeyCode.VcLeftControl, HotkeyModifiers.Control },
        { KeyCode.VcRightControl, HotkeyModifiers.Control },
        { KeyCode.VcLeftAlt, HotkeyModifiers.Alt },
        { KeyCode.VcRightAlt, HotkeyModifiers.Alt },
        { KeyCode.VcLeftShift, HotkeyModifiers.Shift },
        { KeyCode.VcRightShift, HotkeyModifiers.Shift },
        { KeyCode.VcLeftMeta, HotkeyModifiers.Win },
        { KeyCode.VcRightMeta, HotkeyModifiers.Win },
    };

    private static readonly IReadOnlyDictionary<KeyCode, uint> VkByKeyCode = BuildVkByKeyCode();

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

    private static IReadOnlyDictionary<KeyCode, uint> BuildVkByKeyCode()
    {
        var map = new Dictionary<KeyCode, uint>();
        foreach (var keyCode in Enum.GetValues<KeyCode>())
        {
            // Build once; avoid ToString allocations on the hot path.
            var vk = MapToWindowsVkFromName(keyCode.ToString());
            if (vk != 0)
            {
                map[keyCode] = vk;
            }
        }

        return map;
    }

    private void InitializeHook()
    {
        try
        {
            _hook = new EventLoopGlobalHook();
            _hook.HookEnabled += OnHookEnabled;
            _hook.HookDisabled += OnHookDisabled;
            _hook.KeyPressed += OnKeyPressed;
            _hook.KeyReleased += OnKeyReleased;

            Task runTask;
            try
            {
                // Start the global hook (non-blocking). The returned task completes when the hook stops/disposes.
                runTask = _hook.RunAsync();
                _runRequestedTcs.TrySetResult();
            }
            catch (Exception ex)
            {
                _runRequestedTcs.TrySetResult();
                Log.Error(ex, "Error starting SharpHook global hook");
                return;
            }

            _ = runTask.ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    Log.Error(t.Exception, "Error running SharpHook global hook");
                }
            }, TaskScheduler.Default);

            Log.Debug("SharpHook global hook initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize SharpHook global hook");
        }
    }

    private void OnHookEnabled(object? sender, HookEventArgs e)
    {
        lock (_lockObject)
        {
            // Ensure a clean modifier state after startup/restart.
            _currentModifiers = HotkeyModifiers.None;
        }

        Log.Debug("SharpHook global hook enabled");
    }

    private void OnHookDisabled(object? sender, HookEventArgs e)
    {
        lock (_lockObject)
        {
            _currentModifiers = HotkeyModifiers.None;
        }

        Log.Debug("SharpHook global hook disabled");
    }

    /// <summary>
    /// Wait until the global hook is actually running. This prevents a startup window where hotkeys are registered
    /// but the hook hasn't begun processing events yet.
    /// </summary>
    public async Task WaitUntilReadyAsync(TimeSpan timeout)
    {
        if (!IsSupported)
        {
            return;
        }

        // Ensure we at least attempted to start the hook.
        await _runRequestedTcs.Task.ConfigureAwait(false);

        var hook = _hook;
        if (hook == null)
        {
            return;
        }

        var sw = Stopwatch.StartNew();
        while (!hook.IsRunning && sw.Elapsed < timeout)
        {
            await Task.Delay(10).ConfigureAwait(false);
        }

        if (!hook.IsRunning)
        {
            Log.Warning("SharpHook global hook not running after waiting {TimeoutMs}ms", (int)timeout.TotalMilliseconds);
        }
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        try
        {
            var keyCode = e.Data.KeyCode;

            // Do not trigger on pure modifier keys.
            if (ModifierByKeyCode.TryGetValue(keyCode, out var modifierFlag))
            {
                lock (_lockObject)
                {
                    _currentModifiers |= modifierFlag;
                }

                return;
            }

            if (!VkByKeyCode.TryGetValue(keyCode, out var vk) || vk == 0)
            {
                return;
            }

            Action? callback;
            lock (_lockObject)
            {
                var modifiers = _currentModifiers;
                _hotkeysByChord.TryGetValue((modifiers, vk), out callback);
            }

            if (callback == null)
            {
                return;
            }

            // Execute callback on UI thread.
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    callback();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error executing hotkey callback");
                }
            });
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
            var keyCode = e.Data.KeyCode;
            if (ModifierByKeyCode.TryGetValue(keyCode, out var modifierFlag))
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

    private static uint MapToWindowsVkFromName(string keyName)
    {
        if (string.IsNullOrEmpty(keyName)) return 0;

        // Letters/digits
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
                // Remove existing if present.
                if (_chordById.TryGetValue(id, out var existingChord))
                {
                    _chordById.Remove(id);
                    _hotkeysByChord.Remove(existingChord);
                    Log.Debug("Removed existing hotkey: {Id}", id);
                }

                var chord = (modifiers, keyCode);
                _chordById[id] = chord;
                _hotkeysByChord[chord] = callback;

                Log.Debug("SharpHook hotkey registered: {Id} -> {Modifiers}+{KeyCode}", id, modifiers, keyCode);
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
                if (_chordById.TryGetValue(id, out var chord))
                {
                    _chordById.Remove(id);
                    _hotkeysByChord.Remove(chord);
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
                var count = _chordById.Count;
                _chordById.Clear();
                _hotkeysByChord.Clear();
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
            UnregisterAll();

            if (_hook != null)
            {
                _hook.HookEnabled -= OnHookEnabled;
                _hook.HookDisabled -= OnHookDisabled;
                _hook.KeyPressed -= OnKeyPressed;
                _hook.KeyReleased -= OnKeyReleased;
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

