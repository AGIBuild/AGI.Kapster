using System;

namespace AGI.Kapster.Desktop.Services.Hotkeys;

/// <summary>
/// Monitors keyboard layout changes for character-stable hotkey re-registration
/// </summary>
public interface IKeyboardLayoutMonitor : IDisposable
{
    /// <summary>
    /// Event raised when keyboard layout changes
    /// </summary>
    event EventHandler? LayoutChanged;

    /// <summary>
    /// Start monitoring keyboard layout changes
    /// </summary>
    void StartMonitoring();

    /// <summary>
    /// Stop monitoring keyboard layout changes
    /// </summary>
    void StopMonitoring();

    /// <summary>
    /// Whether layout monitoring is currently active
    /// </summary>
    bool IsMonitoring { get; }
}



