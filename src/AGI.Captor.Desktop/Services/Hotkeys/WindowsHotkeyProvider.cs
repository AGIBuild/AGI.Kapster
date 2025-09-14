using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Serilog;

namespace AGI.Captor.Desktop.Services.Hotkeys;

/// <summary>
/// Windows平台热键提供者
/// </summary>
public class WindowsHotkeyProvider : IHotkeyProvider
{
    private readonly Dictionary<string, (int id, Action callback)> _registeredHotkeys = new();
    private int _nextId = 1;
    private bool _disposed = false;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public bool IsSupported => OperatingSystem.IsWindows();
    public bool HasPermissions => true; // Windows不需要特殊权限

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

        try
        {
            // 如果已存在，先注销
            if (_registeredHotkeys.ContainsKey(id))
            {
                UnregisterHotkey(id);
            }

            var hotkeyId = _nextId++;
            var success = RegisterHotKey(IntPtr.Zero, hotkeyId, (uint)modifiers, keyCode);
            
            if (success)
            {
                _registeredHotkeys[id] = (hotkeyId, callback);
                Log.Debug("Windows hotkey registered: {Id} -> {Modifiers}+{KeyCode}", id, modifiers, keyCode);
                return true;
            }
            else
            {
                Log.Warning("Failed to register Windows hotkey: {Id}", id);
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception registering Windows hotkey: {Id}", id);
            return false;
        }
    }

    public bool UnregisterHotkey(string id)
    {
        if (!_registeredHotkeys.TryGetValue(id, out var hotkey))
        {
            return false;
        }

        try
        {
            var success = UnregisterHotKey(IntPtr.Zero, hotkey.id);
            if (success)
            {
                _registeredHotkeys.Remove(id);
                Log.Debug("Windows hotkey unregistered: {Id}", id);
            }
            return success;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception unregistering Windows hotkey: {Id}", id);
            return false;
        }
    }

    public void UnregisterAll()
    {
        var keys = new List<string>(_registeredHotkeys.Keys);
        foreach (var key in keys)
        {
            UnregisterHotkey(key);
        }
        Log.Debug("All Windows hotkeys unregistered");
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        UnregisterAll();
        _disposed = true;
        Log.Debug("WindowsHotkeyProvider disposed");
    }
}