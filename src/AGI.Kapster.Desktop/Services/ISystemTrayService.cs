using System;
using System.Threading.Tasks;

namespace AGI.Kapster.Desktop.Services;

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
    /// Show an install confirmation prompt to the user. Should be interactive when UI is available.
    /// Returns true if the user agreed to install, false otherwise.
    /// </summary>
    /// <param name="installerPath">Path to the installer file (used for display only)</param>
    /// <returns>Task resolving to user's choice</returns>
    Task<bool> ShowInstallConfirmationAsync(string installerPath);

    /// <summary>
    /// Event fired when user wants to open settings
    /// </summary>
    event EventHandler? OpenSettingsRequested;

    /// <summary>
    /// Event fired when user wants to exit application
    /// </summary>
    event EventHandler? ExitRequested;
}
