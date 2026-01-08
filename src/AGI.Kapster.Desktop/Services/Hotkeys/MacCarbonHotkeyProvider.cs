using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Serilog;
using AGI.Kapster.Desktop.Models;

namespace AGI.Kapster.Desktop.Services.Hotkeys;

/// <summary>
/// macOS Carbon-based hotkey provider using managed P/Invoke:
/// - InstallApplicationEventHandler (hotkey pressed)
/// - RegisterEventHotKey / UnregisterEventHotKey
/// This avoids CGEventTap / Input Monitoring and avoids fragile managed event-loop polling.
/// </summary>
public sealed class MacCarbonHotkeyProvider : IHotkeyProvider
{
    // 'Kaps'
    private const uint HotkeySignature = 0x4B617073;

    // Carbon modifier flags (Events.h)
    private const uint cmdKey = 0x0100;
    private const uint shiftKey = 0x0200;
    private const uint optionKey = 0x0800;
    private const uint controlKey = 0x1000;

    private readonly object _lock = new();
    private readonly Dictionary<string, RegisteredHotkey> _byStringId = new();
    private readonly Dictionary<uint, RegisteredHotkey> _byNativeId = new();
    private uint _nextNativeId = 1;

    private bool _initialized;
    private bool _disposed;

    private IntPtr _eventHandlerRef;
    private CarbonEventHandlerProc? _eventHandlerProc;
    private GCHandle _eventHandlerProcHandle;
    private readonly IHotkeyResolver? _resolver;

    public bool IsSupported => OperatingSystem.IsMacOS();
    public bool HasPermissions => OperatingSystem.IsMacOS(); // Carbon hotkeys don't require Input Monitoring

    public MacCarbonHotkeyProvider(IHotkeyResolver? resolver = null)
    {
        _resolver = resolver;
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
            var keyCode = NamedKeyToMacVK(namedSpec.NamedKey);
            if (keyCode != uint.MaxValue)
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
        {
            Log.Warning("macOS Carbon hotkeys not supported on this platform");
            return false;
        }

        if (string.IsNullOrWhiteSpace(id) || callback == null)
        {
            Log.Warning("Invalid hotkey registration parameters");
            return false;
        }

        lock (_lock)
        {
            if (_disposed)
                return false;

            if (!EnsureInitializedOnUiThread())
                return false;

            // Replace existing
            if (_byStringId.TryGetValue(id, out var existing))
            {
                UnregisterHotkeyInternal(existing);
            }

            var carbonModifiers = ToCarbonModifiers(modifiers);
            var nativeId = _nextNativeId++;

            var hkId = new EventHotKeyID
            {
                signature = HotkeySignature,
                id = nativeId
            };

            var target = GetApplicationEventTarget();
            var rc = RegisterEventHotKey(keyCode, carbonModifiers, hkId, target, 0, out var hotKeyRef);
            if (rc != 0 || hotKeyRef == IntPtr.Zero)
            {
                Log.Warning(
                    "Failed to register Carbon hotkey: {Id}, keyCode={KeyCode}, carbonModifiers=0x{Mods:X}, rc={Rc}, hotKeyRef={HotKeyRef}",
                    id, keyCode, carbonModifiers, rc, hotKeyRef);
                return false;
            }

            var reg = new RegisteredHotkey(id, nativeId, hotKeyRef, callback);
            _byStringId[id] = reg;
            _byNativeId[nativeId] = reg;

            Log.Debug("Carbon hotkey registered: {Id} -> modifiers={Modifiers}, keyCode={KeyCode}",
                id, modifiers, keyCode);

            return true;
        }
    }

    public bool UnregisterHotkey(string id)
    {
        if (!IsSupported)
            return false;

        lock (_lock)
        {
            if (_disposed)
                return false;

            if (!_byStringId.TryGetValue(id, out var existing))
                return false;

            return UnregisterHotkeyInternal(existing);
        }
    }

    public void UnregisterAll()
    {
        if (!IsSupported)
            return;

        List<IntPtr>? refsToUnregister = null;
        lock (_lock)
        {
            if (_disposed)
                return;

            if (_byStringId.Count > 0)
            {
                refsToUnregister = new List<IntPtr>(_byStringId.Count);
                foreach (var (_, reg) in _byStringId)
                {
                    if (reg.HotKeyRef != IntPtr.Zero)
                        refsToUnregister.Add(reg.HotKeyRef);
                }
            }

            _byStringId.Clear();
            _byNativeId.Clear();
            _nextNativeId = 1;
        }

        if (refsToUnregister != null)
        {
            foreach (var hkRef in refsToUnregister)
            {
                try
                {
                    _ = UnregisterEventHotKey(hkRef);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error unregistering Carbon hotkey ref");
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            try
            {
                UnregisterAll();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error unregistering macOS Carbon hotkeys during dispose");
            }

            try
            {
                RemoveEventHandlerOnUiThread();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error removing Carbon hotkey event handler");
            }

            if (_eventHandlerProcHandle.IsAllocated)
            {
                _eventHandlerProcHandle.Free();
            }

            _disposed = true;
        }

        Log.Debug("MacCarbonHotkeyProvider disposed");
    }

    private bool EnsureInitializedOnUiThread()
    {
        if (_initialized)
            return true;

        if (Dispatcher.UIThread.CheckAccess())
        {
            return InitializeOnUiThread();
        }

        try
        {
            var op = Dispatcher.UIThread.InvokeAsync(InitializeOnUiThread);
            // Avalonia DispatcherOperation<T> is not Task-based; wait then read Result.
            // Wait(TimeSpan) may throw on timeout depending on Avalonia version.
            op.Wait(TimeSpan.FromSeconds(2));
            return op.Result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize native Carbon hotkey helper on UI thread");
            return false;
        }
    }

    private bool InitializeOnUiThread()
    {
        if (_initialized)
            return true;

        try
        {
            var eventType = new EventTypeSpec
            {
                eventClass = kEventClassKeyboard,
                eventKind = kEventHotKeyPressed
            };

            _eventHandlerProc = OnCarbonEvent;
            _eventHandlerProcHandle = GCHandle.Alloc(_eventHandlerProc);

            // NOTE: InstallApplicationEventHandler is not exported on some macOS versions (it is a macro/inline wrapper).
            // Use InstallEventHandler against the application event target instead.
            var target = GetApplicationEventTarget();
            var rc = InstallEventHandler(target, _eventHandlerProc, 1, ref eventType, IntPtr.Zero, out _eventHandlerRef);
            if (rc != 0)
            {
                Log.Error("InstallEventHandler failed: rc={Rc}", rc);
                return false;
            }

            _initialized = true;
            Log.Debug("Carbon hotkey handler initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception initializing Carbon hotkey handler");
            return false;
        }
    }

    private int OnCarbonEvent(IntPtr nextHandler, IntPtr eventRef, IntPtr userData)
    {
        _ = nextHandler;
        _ = userData;

        try
        {
            var rc = GetEventParameter(
                eventRef,
                kEventParamDirectObject,
                typeEventHotKeyID,
                IntPtr.Zero,
                (uint)Marshal.SizeOf<EventHotKeyID>(),
                IntPtr.Zero,
                out var hkId);

            if (rc != 0)
                return 0;

            if (hkId.signature != HotkeySignature)
                return 0;

            OnHotkeyTriggered(hkId.id);
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling Carbon hotkey event");
            return 0;
        }
    }

    private void OnHotkeyTriggered(uint nativeHotkeyId)
    {
        RegisteredHotkey? reg = null;
        lock (_lock)
        {
            if (_disposed)
                return;

            _byNativeId.TryGetValue(nativeHotkeyId, out reg);
        }

        if (reg == null)
        {
            Log.Warning("Received Carbon hotkey event for unknown id: {NativeId}", nativeHotkeyId);
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                reg.Callback();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error executing hotkey callback for {Id}", reg.StringId);
            }
        });
    }

    private bool UnregisterHotkeyInternal(RegisteredHotkey existing)
    {
        _byStringId.Remove(existing.StringId);
        _byNativeId.Remove(existing.NativeId);

        if (_initialized && existing.HotKeyRef != IntPtr.Zero)
        {
            var rc = UnregisterEventHotKey(existing.HotKeyRef);
            if (rc != 0)
            {
                Log.Warning("Failed to unregister Carbon hotkey: {Id}, nativeId={NativeId}, rc={Rc}",
                    existing.StringId, existing.NativeId, rc);
                return false;
            }
        }

        Log.Debug("Carbon hotkey unregistered: {Id}", existing.StringId);
        return true;
    }

    private static uint NamedKeyToMacVK(NamedKey namedKey)
    {
        // macOS virtual key codes (kVK_ANSI_*)
        return namedKey switch
        {
            NamedKey.F1 => 0x7A,
            NamedKey.F2 => 0x78,
            NamedKey.F3 => 0x63,
            NamedKey.F4 => 0x76,
            NamedKey.F5 => 0x60,
            NamedKey.F6 => 0x61,
            NamedKey.F7 => 0x62,
            NamedKey.F8 => 0x64,
            NamedKey.F9 => 0x65,
            NamedKey.F10 => 0x6D,
            NamedKey.F11 => 0x67,
            NamedKey.F12 => 0x6F,
            NamedKey.Enter => 0x24,
            NamedKey.Tab => 0x30,
            NamedKey.Escape => 0x35,
            NamedKey.Space => 0x31,
            NamedKey.Backspace => 0x33,
            NamedKey.Delete => 0x75,
            NamedKey.Insert => 0x72,
            NamedKey.Home => 0x73,
            NamedKey.End => 0x77,
            NamedKey.PageUp => 0x74,
            NamedKey.PageDown => 0x79,
            NamedKey.Up => 0x7E,
            NamedKey.Down => 0x7D,
            NamedKey.Left => 0x7B,
            NamedKey.Right => 0x7C,
            _ => uint.MaxValue
        };
    }

    private static uint ToCarbonModifiers(HotkeyModifiers modifiers)
    {
        uint carbon = 0;
        if ((modifiers & HotkeyModifiers.Win) != 0) carbon |= cmdKey;
        if ((modifiers & HotkeyModifiers.Shift) != 0) carbon |= shiftKey;
        if ((modifiers & HotkeyModifiers.Alt) != 0) carbon |= optionKey;
        if ((modifiers & HotkeyModifiers.Control) != 0) carbon |= controlKey;
        return carbon;
    }

    private void RemoveEventHandlerOnUiThread()
    {
        if (!_initialized)
            return;

        if (_eventHandlerRef == IntPtr.Zero)
            return;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            var op = Dispatcher.UIThread.InvokeAsync(RemoveEventHandlerOnUiThread);
            op.Wait(TimeSpan.FromSeconds(2));
            return;
        }

        try
        {
            var rc = RemoveEventHandler(_eventHandlerRef);
            if (rc != 0)
                Log.Warning("RemoveEventHandler returned rc={Rc}", rc);
        }
        finally
        {
            _eventHandlerRef = IntPtr.Zero;
            _initialized = false;
        }
    }

    private sealed record RegisteredHotkey(string StringId, uint NativeId, IntPtr HotKeyRef, Action Callback);

    private const string CarbonLib = "/System/Library/Frameworks/Carbon.framework/Carbon";

    // Carbon Event Manager constants
    private const uint kEventClassKeyboard = 0x6B657962; // 'keyb'
    private const uint kEventHotKeyPressed = 6;
    private const uint kEventParamDirectObject = 0x2D2D2D2D; // '----'
    private const uint typeEventHotKeyID = 0x686B6964; // 'hkid'

    [StructLayout(LayoutKind.Sequential)]
    private struct EventTypeSpec
    {
        public uint eventClass;
        public uint eventKind;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventHotKeyID
    {
        public uint signature;
        public uint id;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CarbonEventHandlerProc(IntPtr nextHandler, IntPtr theEvent, IntPtr userData);

    [DllImport(CarbonLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int InstallEventHandler(
        IntPtr inTarget,
        CarbonEventHandlerProc handler,
        uint numTypes,
        ref EventTypeSpec list,
        IntPtr userData,
        out IntPtr outHandlerRef);

    [DllImport(CarbonLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int RemoveEventHandler(IntPtr handlerRef);

    [DllImport(CarbonLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr GetApplicationEventTarget();

    [DllImport(CarbonLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int RegisterEventHotKey(
        uint inHotKeyCode,
        uint inHotKeyModifiers,
        EventHotKeyID inHotKeyID,
        IntPtr inTarget,
        uint inOptions,
        out IntPtr outRef);

    [DllImport(CarbonLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int UnregisterEventHotKey(IntPtr inHotKeyRef);

    [DllImport(CarbonLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int GetEventParameter(
        IntPtr inEvent,
        uint inName,
        uint inDesiredType,
        IntPtr outActualType,
        uint inBufferSize,
        IntPtr outActualSize,
        out EventHotKeyID outData);
}
