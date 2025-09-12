using System;

namespace AGI.Captor.App.Services;

/// <summary>
/// System tray service interface
/// </summary>
public interface ISystemTrayService
{
    /// <summary>
    /// Initialize system tray
    /// </summary>
    void Initialize();
    
    /// <summary>
    /// Show notification in system tray
    /// </summary>
    void ShowNotification(string title, string message);
    
    /// <summary>
    /// Event fired when user wants to open settings
    /// </summary>
    event EventHandler? OpenSettingsRequested;
    
    /// <summary>
    /// Event fired when user wants to exit application
    /// </summary>
    event EventHandler? ExitRequested;
}
