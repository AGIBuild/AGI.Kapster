using System;
using System.Threading.Tasks;
using AGI.Captor.App.Services.Hotkeys;

namespace AGI.Captor.App.Services;

/// <summary>
/// Hotkey manager interface for managing application hotkeys
/// </summary>
public interface IHotkeyManager
{
    /// <summary>
    /// Initialize the hotkey manager with current settings
    /// </summary>
    Task InitializeAsync();
    
    /// <summary>
    /// Reload hotkeys from current settings
    /// </summary>
    Task ReloadHotkeysAsync();
    
    /// <summary>
    /// Register a hotkey with callback
    /// </summary>
    bool RegisterHotkey(string id, string hotkeyString, Action callback);
    
    /// <summary>
    /// Unregister a hotkey
    /// </summary>
    void UnregisterHotkey(string id);
    
    /// <summary>
    /// Unregister all hotkeys
    /// </summary>
    void UnregisterAllHotkeys();
    
    /// <summary>
    /// Parse a hotkey string into a chord
    /// </summary>
    HotkeyChord? ParseHotkeyString(string hotkeyString);
}
