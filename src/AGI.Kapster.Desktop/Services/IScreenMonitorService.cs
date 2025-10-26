using System;
using System.Collections.Generic;
using Avalonia.Platform;

namespace AGI.Kapster.Desktop.Services;

/// <summary>
/// Service for monitoring screen configuration changes
/// Provides real-time screen information through event-driven caching
/// </summary>
public interface IScreenMonitorService
{
    /// <summary>
    /// Get current screens (cached, always up-to-date)
    /// Returns immediately without delay - screen info is maintained in real-time
    /// </summary>
    /// <returns>Current list of screens with latest configuration</returns>
    IReadOnlyList<Screen> GetCurrentScreens();
    
    /// <summary>
    /// Event fired when screen configuration changes
    /// Triggered by: screen rotation, hot-plug, resolution change, DPI change
    /// </summary>
    event EventHandler<ScreensChangedEventArgs>? ScreensChanged;
    
    /// <summary>
    /// Request application exit (closes MainWindow and triggers shutdown)
    /// Use this instead of Application.Current.Shutdown() for proper cleanup
    /// </summary>
    void RequestAppExit();
}

/// <summary>
/// Event args for screen configuration changes
/// </summary>
public class ScreensChangedEventArgs : EventArgs
{
    public IReadOnlyList<Screen> Screens { get; }
    
    public ScreensChangedEventArgs(IReadOnlyList<Screen> screens)
    {
        Screens = screens;
    }
}

