using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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

    private const int WM_APP = 0x8000;
    private const int WM_KAPSTER_INVOKE = WM_APP + 1;

    // RegisterHotKey modifiers
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private const uint WS_OVERLAPPED = 0x00000000;

    private static readonly IntPtr HWND_MESSAGE = new(-3);

    private readonly object _lock = new();
    private readonly Dictionary<string, int> _nativeIdByStringId = new();
    private readonly Dictionary<int, Action> _callbackByNativeId = new();
    private int _nextNativeId = 1;

    private long _opVersion;
    private readonly Dictionary<string, long> _idVersion = new();

    private readonly ConcurrentQueue<IInvokeRequest> _invokeQueue = new();
    private int _invokePosted;

    private bool _monitoringLayout;
    private bool _disposed;

    private Thread? _thread;
    private uint _threadId;
    private IntPtr _windowHandle;
    private WndProcDelegate? _wndProc;
    private GCHandle _wndProcHandle;
    private string? _windowClassName;
    private IntPtr _hInstance;
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

        int? existingNativeId = null;
        int nativeId;
        uint fsModifiers;
        long version;

        lock (_lock)
        {
            if (_disposed)
                return false;

            version = ++_opVersion;
            _idVersion[id] = version;

            // Replace existing (unregister happens outside lock)
            if (_nativeIdByStringId.TryGetValue(id, out var existing))
            {
                existingNativeId = existing;
                _nativeIdByStringId.Remove(id);
                _callbackByNativeId.Remove(existing);
            }

            nativeId = _nextNativeId++;
            fsModifiers = ToFsModifiers(modifiers);
        }

        if (existingNativeId.HasValue)
        {
            _ = UnregisterInternal(existingNativeId.Value);
        }

        var result = InvokeOnMessageThread(() =>
        {
            var ok = RegisterHotKey(_windowHandle, nativeId, fsModifiers, keyCode);
            var err = ok ? 0 : Marshal.GetLastWin32Error();
            return new Win32BoolResult(ok, err);
        }, fallback: new Win32BoolResult(false, unchecked((int)0xFFFF_FFFE)));

        if (!result.Ok)
        {
            Log.Warning(
                "RegisterHotKey failed: id={Id}, vk=0x{Vk:X}, modifiers=0x{Mods:X}, err={Err}",
                id, keyCode, fsModifiers, result.Error);
            return false;
        }

        // Commit only if no newer operation replaced this id.
        bool shouldRollback = false;
        lock (_lock)
        {
            if (_disposed)
            {
                shouldRollback = true;
            }
            else if (_idVersion.TryGetValue(id, out var current) && current == version)
            {
                _nativeIdByStringId[id] = nativeId;
                _callbackByNativeId[nativeId] = callback;
                return true;
            }
            else
            {
                shouldRollback = true;
            }
        }

        if (shouldRollback)
        {
            _ = UnregisterInternal(nativeId);
        }

        return false;
    }

    public bool UnregisterHotkey(string id)
    {
        if (!IsSupported)
            return false;

        EnsureReady();

        int nativeId;
        lock (_lock)
        {
            if (_disposed)
                return false;

            // New version to invalidate any in-flight register for this id.
            _idVersion[id] = ++_opVersion;

            if (!_nativeIdByStringId.TryGetValue(id, out nativeId))
                return false;

            _nativeIdByStringId.Remove(id);
            _callbackByNativeId.Remove(nativeId);
        }

        return UnregisterInternal(nativeId);
    }

    public void UnregisterAll()
    {
        if (!IsSupported)
            return;

        EnsureReady();

        List<int> nativeIds;
        lock (_lock)
        {
            if (_disposed)
                return;

            // Invalidate any in-flight per-id operations.
            _opVersion++;
            _idVersion.Clear();

            nativeIds = new List<int>(_callbackByNativeId.Keys);
            _nativeIdByStringId.Clear();
            _callbackByNativeId.Clear();
            _nextNativeId = 1;
        }

        foreach (var nativeId in nativeIds)
        {
            _ = UnregisterInternal(nativeId);
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
        List<int>? idsToUnregister = null;
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;

            // Make a best-effort attempt to unregister everything on the message thread.
            idsToUnregister = new List<int>(_callbackByNativeId.Keys);
            _nativeIdByStringId.Clear();
            _callbackByNativeId.Clear();
            _nextNativeId = 1;
        }

        // Fail any pending invoke requests so callers don't hang.
        FailPendingInvokes(new ObjectDisposedException(nameof(WindowsHotkeyProvider)));

        try
        {
            EnsureReady();
            if (idsToUnregister != null)
            {
                foreach (var nativeId in idsToUnregister)
                {
                    _ = UnregisterInternal(nativeId);
                }
            }
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
            if (!_ready.Wait(TimeSpan.FromSeconds(2)))
            {
                Log.Warning("Windows hotkey message thread not ready after timeout");
            }
        }
    }

    private void MessageThreadMain()
    {
        _threadId = GetCurrentThreadId();

        _hInstance = GetModuleHandle(null);
        // Must be unique per instance; otherwise multiple providers in one process could share a class
        // registered with the first instance's WndProc, causing messages to be delivered to the wrong delegate.
        _windowClassName = $"KapsterHotkeyWindowClass_{Environment.ProcessId}_{Guid.NewGuid():N}";

        _wndProc = WndProc;
        _wndProcHandle = GCHandle.Alloc(_wndProc);

        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = _hInstance,
            hIcon = IntPtr.Zero,
            hCursor = IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null,
            lpszClassName = _windowClassName,
            hIconSm = IntPtr.Zero
        };

        var atom = RegisterClassEx(ref wc);
        if (atom == 0)
        {
            var err = Marshal.GetLastWin32Error();
            Log.Error("Failed to register Windows hotkey window class. err={Err}", err);
            _ready.Set();
            return;
        }

        // Create a hidden message-only window with our own WndProc.
        _windowHandle = CreateWindowEx(
            0,
            _windowClassName,
            "KapsterHotkeyWindow",
            WS_OVERLAPPED,
            0, 0, 0, 0,
            HWND_MESSAGE,
            IntPtr.Zero,
            _hInstance,
            IntPtr.Zero);
        if (_windowHandle == IntPtr.Zero)
        {
            Log.Error("Failed to create Windows hotkey message window. err={Err}", Marshal.GetLastWin32Error());
            _ready.Set();
            try { UnregisterClass(_windowClassName, _hInstance); } catch { /* ignore */ }
            return;
        }

        _ready.Set();

        // Standard message loop.
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0))
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        try
        {
            if (_windowClassName != null && _hInstance != IntPtr.Zero)
            {
                _ = UnregisterClass(_windowClassName, _hInstance);
            }
        }
        catch
        {
            // ignore
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
    {
        if (uMsg == WM_KAPSTER_INVOKE)
        {
            DrainInvokeQueue();

            // Allow future posts. If new items were enqueued while draining,
            // re-post so we don't miss them.
            Interlocked.Exchange(ref _invokePosted, 0);
            if (!_invokeQueue.IsEmpty)
            {
                TryPostInvokeMessage();
            }

            return IntPtr.Zero;
        }

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
            // Fail any pending marshaled work to avoid callers hanging during shutdown.
            FailPendingInvokes(new InvalidOperationException("Hotkey message window destroyed"));
            PostQuitMessage(0);
            return IntPtr.Zero;
        }

        return DefWindowProc(hWnd, uMsg, wParam, lParam);
    }

    private bool UnregisterInternal(int nativeId)
    {
        if (_windowHandle == IntPtr.Zero)
            return false;

        var result = InvokeOnMessageThread(() =>
        {
            var ok = UnregisterHotKey(_windowHandle, nativeId);
            var err = ok ? 0 : Marshal.GetLastWin32Error();
            return new Win32BoolResult(ok, err);
        }, fallback: new Win32BoolResult(false, unchecked((int)0xFFFF_FFFE)));

        if (!result.Ok)
        {
            Log.Debug("UnregisterHotKey returned false for id={Id}, err={Err}", nativeId, result.Error);
            return false;
        }

        return true;
    }

    private T InvokeOnMessageThread<T>(Func<T> func, T fallback)
    {
        if (_windowHandle == IntPtr.Zero)
            return fallback;

        if (GetCurrentThreadId() == _threadId)
        {
            try { return func(); }
            catch (Exception ex)
            {
                Log.Error(ex, "Error executing hotkey-thread operation inline");
                return fallback;
            }
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var req = new InvokeRequest<T>(func, tcs);
        _invokeQueue.Enqueue(req);

        if (!TryPostInvokeMessage())
        {
            req.Fail(new InvalidOperationException("Failed to post invoke message to hotkey thread"));
            return fallback;
        }

        if (!tcs.Task.Wait(TimeSpan.FromSeconds(2)))
        {
            return fallback;
        }

        try { return tcs.Task.Result; }
        catch (Exception ex)
        {
            Log.Error(ex, "Error awaiting hotkey-thread operation");
            return fallback;
        }
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

    private readonly record struct Win32BoolResult(bool Ok, int Error);

    private interface IInvokeRequest
    {
        void Execute();

        void Fail(Exception ex);
    }

    private sealed class InvokeRequest<T> : IInvokeRequest
    {
        private readonly Func<T> _func;
        private readonly TaskCompletionSource<T> _tcs;

        public InvokeRequest(Func<T> func, TaskCompletionSource<T> tcs)
        {
            _func = func;
            _tcs = tcs;
        }

        public void Execute()
        {
            try
            {
                _tcs.TrySetResult(_func());
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex);
            }
        }

        public void Fail(Exception ex)
        {
            _tcs.TrySetException(ex);
        }
    }

    private bool TryPostInvokeMessage()
    {
        if (_windowHandle == IntPtr.Zero)
            return false;

        // Only post when transitioning 0 -> 1 to avoid message storms.
        if (Interlocked.Exchange(ref _invokePosted, 1) != 0)
            return true;

        var posted = PostMessage(_windowHandle, WM_KAPSTER_INVOKE, IntPtr.Zero, IntPtr.Zero);
        if (!posted)
        {
            Interlocked.Exchange(ref _invokePosted, 0);
            return false;
        }

        return true;
    }

    private void DrainInvokeQueue()
    {
        while (_invokeQueue.TryDequeue(out var req))
        {
            try
            {
                req.Execute();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error executing hotkey-thread invoke request");
            }
        }
    }

    private void FailPendingInvokes(Exception ex)
    {
        while (_invokeQueue.TryDequeue(out var req))
        {
            try
            {
                req.Fail(ex);
            }
            catch
            {
                // ignore
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

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
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}

