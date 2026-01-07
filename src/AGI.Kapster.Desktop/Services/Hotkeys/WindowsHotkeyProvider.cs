using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Threading;
using Serilog;
using AGI.Kapster.Desktop.Models;

namespace AGI.Kapster.Desktop.Services.Hotkeys;

/// <summary>
/// Windows global hotkey provider using the system API RegisterHotKey (no global hooks).
/// Also provides keyboard layout change notifications (WM_INPUTLANGCHANGE) via the same hidden window.
/// </summary>
public sealed class WindowsHotkeyProvider : IHotkeyProvider, IKeyboardLayoutMonitor
{
    private const int WM_HOTKEY = 0x0312;
    private const int WM_INPUTLANGCHANGE = 0x0051;
    private const int WM_CLOSE = 0x0010;
    private const int WM_DESTROY = 0x0002;

    private const int GWLP_WNDPROC = -4;

    // RegisterHotKey modifiers
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private const uint WS_OVERLAPPED = 0x00000000;

    private readonly object _lock = new();
    private readonly Dictionary<string, int> _nativeIdByStringId = new();
    private readonly Dictionary<int, Action> _callbackByNativeId = new();
    private int _nextNativeId = 1;

    private bool _monitoringLayout;
    private bool _disposed;

    private Thread? _thread;
    private uint _threadId;
    private IntPtr _windowHandle;
    private WndProcDelegate? _wndProc;
    private GCHandle _wndProcHandle;
    private IntPtr _oldWndProc;
    private readonly ManualResetEventSlim _ready = new(false);

    public bool IsSupported => OperatingSystem.IsWindows();
    public bool HasPermissions => OperatingSystem.IsWindows();

    public event EventHandler? LayoutChanged;
    public bool IsMonitoring => _monitoringLayout;

    private readonly IHotkeyResolver? _resolver;

    public WindowsHotkeyProvider(IHotkeyResolver? resolver = null)
    {
        _resolver = resolver;
        if (!IsSupported)
        {
            Log.Warning("WindowsHotkeyProvider created on non-Windows platform");
            return;
        }

        StartMessageThread();
    }

    public bool RegisterHotkey(string id, HotkeyGesture gesture, Action callback)
    {
        if (gesture == null)
        {
            Log.Warning("Hotkey gesture is null for {Id}", id);
            return false;
        }

        // Try to resolve gesture using resolver
        ResolvedHotkey? resolved = null;
        if (_resolver != null)
        {
            resolved = _resolver.Resolve(gesture);
        }

        // Fallback for named keys if resolver unavailable
        if (resolved == null && gesture.KeySpec is NamedKeySpec namedSpec)
        {
            var keyCode = NamedKeyToVK(namedSpec.NamedKey);
            if (keyCode != 0)
            {
                resolved = new ResolvedHotkey(keyCode, gesture.Modifiers, gesture.ToDisplayString());
            }
        }

        if (resolved == null)
        {
            Log.Warning("Failed to resolve hotkey gesture: {Gesture} for {Id}", gesture.ToDisplayString(), id);
            return false;
        }

        return RegisterResolvedHotkey(id, resolved.Modifiers, resolved.KeyCode, callback);
    }

    private bool RegisterResolvedHotkey(string id, HotkeyModifiers modifiers, uint keyCode, Action callback)
    {
        if (!IsSupported)
            return false;
        if (string.IsNullOrWhiteSpace(id) || callback == null)
            return false;

        EnsureReady();

        lock (_lock)
        {
            if (_disposed)
                return false;

            // Replace existing
            if (_nativeIdByStringId.TryGetValue(id, out var existingNativeId))
            {
                UnregisterInternal(existingNativeId);
                _nativeIdByStringId.Remove(id);
                _callbackByNativeId.Remove(existingNativeId);
            }

            var nativeId = _nextNativeId++;
            var fsModifiers = ToFsModifiers(modifiers);

            if (!RegisterHotKey(_windowHandle, nativeId, fsModifiers, keyCode))
            {
                var err = Marshal.GetLastWin32Error();
                Log.Warning("RegisterHotKey failed: id={Id}, vk=0x{Vk:X}, modifiers=0x{Mods:X}, err={Err}", id, keyCode, fsModifiers, err);
                return false;
            }

            _nativeIdByStringId[id] = nativeId;
            _callbackByNativeId[nativeId] = callback;
            return true;
        }
    }

    public bool UnregisterHotkey(string id)
    {
        if (!IsSupported)
            return false;

        EnsureReady();

        lock (_lock)
        {
            if (_disposed)
                return false;

            if (!_nativeIdByStringId.TryGetValue(id, out var nativeId))
                return false;

            _nativeIdByStringId.Remove(id);
            _callbackByNativeId.Remove(nativeId);
            return UnregisterInternal(nativeId);
        }
    }

    public void UnregisterAll()
    {
        if (!IsSupported)
            return;

        EnsureReady();

        lock (_lock)
        {
            if (_disposed)
                return;

            foreach (var nativeId in _callbackByNativeId.Keys)
            {
                UnregisterInternal(nativeId);
            }

            _nativeIdByStringId.Clear();
            _callbackByNativeId.Clear();
            _nextNativeId = 1;
        }
    }

    public void StartMonitoring()
    {
        _monitoringLayout = true;
    }

    public void StopMonitoring()
    {
        _monitoringLayout = false;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        try
        {
            UnregisterAll();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error unregistering hotkeys during dispose");
        }

        try
        {
            if (_windowHandle != IntPtr.Zero)
            {
                PostMessage(_windowHandle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error stopping hotkey message thread");
        }

        try
        {
            _thread?.Join(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // ignore
        }

        if (_wndProcHandle.IsAllocated)
        {
            _wndProcHandle.Free();
        }
    }

    private void StartMessageThread()
    {
        _thread = new Thread(MessageThreadMain)
        {
            IsBackground = true,
            Name = "WindowsHotkeyMessageLoop"
        };
        _thread.Start();
    }

    private void EnsureReady()
    {
        if (!_ready.IsSet)
        {
            _ready.Wait(TimeSpan.FromSeconds(2));
        }
    }

    private void MessageThreadMain()
    {
        _threadId = GetCurrentThreadId();

        // Create a hidden window (built-in STATIC class is sufficient for message dispatch).
        _windowHandle = CreateWindowEx(0, "STATIC", "KapsterHotkeyWindow", WS_OVERLAPPED, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (_windowHandle == IntPtr.Zero)
        {
            Log.Error("Failed to create Windows hotkey message window. err={Err}", Marshal.GetLastWin32Error());
            _ready.Set();
            return;
        }

        _wndProc = WndProc;
        _wndProcHandle = GCHandle.Alloc(_wndProc);
        _oldWndProc = SetWindowLongPtr(_windowHandle, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProc));

        _ready.Set();

        // Standard message loop.
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0))
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
    {
        if (uMsg == WM_HOTKEY)
        {
            var nativeId = wParam.ToInt32();
            Action? cb = null;
            lock (_lock)
            {
                _callbackByNativeId.TryGetValue(nativeId, out cb);
            }

            if (cb != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try { cb(); }
                    catch (Exception ex) { Log.Error(ex, "Error executing hotkey callback"); }
                });
            }
        }
        else if (uMsg == WM_INPUTLANGCHANGE)
        {
            if (_monitoringLayout)
            {
                LayoutChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        else if (uMsg == WM_CLOSE)
        {
            DestroyWindow(hWnd);
            return IntPtr.Zero;
        }
        else if (uMsg == WM_DESTROY)
        {
            PostQuitMessage(0);
            return IntPtr.Zero;
        }

        if (_oldWndProc != IntPtr.Zero)
        {
            return CallWindowProc(_oldWndProc, hWnd, uMsg, wParam, lParam);
        }

        return DefWindowProc(hWnd, uMsg, wParam, lParam);
    }

    private bool UnregisterInternal(int nativeId)
    {
        if (_windowHandle == IntPtr.Zero)
            return false;

        if (!UnregisterHotKey(_windowHandle, nativeId))
        {
            var err = Marshal.GetLastWin32Error();
            Log.Debug("UnregisterHotKey returned false for id={Id}, err={Err}", nativeId, err);
            return false;
        }

        return true;
    }

    private static uint ToFsModifiers(HotkeyModifiers modifiers)
    {
        uint m = MOD_NOREPEAT;
        if ((modifiers & HotkeyModifiers.Alt) != 0) m |= MOD_ALT;
        if ((modifiers & HotkeyModifiers.Control) != 0) m |= MOD_CONTROL;
        if ((modifiers & HotkeyModifiers.Shift) != 0) m |= MOD_SHIFT;
        if ((modifiers & HotkeyModifiers.Win) != 0) m |= MOD_WIN;
        return m;
    }

    private static uint NamedKeyToVK(NamedKey namedKey)
    {
        return namedKey switch
        {
            NamedKey.F1 => 0x70,
            NamedKey.F2 => 0x71,
            NamedKey.F3 => 0x72,
            NamedKey.F4 => 0x73,
            NamedKey.F5 => 0x74,
            NamedKey.F6 => 0x75,
            NamedKey.F7 => 0x76,
            NamedKey.F8 => 0x77,
            NamedKey.F9 => 0x78,
            NamedKey.F10 => 0x79,
            NamedKey.F11 => 0x7A,
            NamedKey.F12 => 0x7B,
            NamedKey.Enter => 0x0D,
            NamedKey.Tab => 0x09,
            NamedKey.Escape => 0x1B,
            NamedKey.Space => 0x20,
            NamedKey.Backspace => 0x08,
            NamedKey.Delete => 0x2E,
            NamedKey.Insert => 0x2D,
            NamedKey.Home => 0x24,
            NamedKey.End => 0x23,
            NamedKey.PageUp => 0x21,
            NamedKey.PageDown => 0x22,
            NamedKey.Up => 0x26,
            NamedKey.Down => 0x28,
            NamedKey.Left => 0x25,
            NamedKey.Right => 0x27,
            _ => 0
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public System.Drawing.Point pt;
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}

