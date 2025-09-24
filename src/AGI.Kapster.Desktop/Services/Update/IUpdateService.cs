using System;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Models.Update;

namespace AGI.Kapster.Desktop.Services.Update;

/// <summary>
/// Interface for automatic update service
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Check for available updates
    /// </summary>
    /// <returns>Update information if available, null if no updates</returns>
    Task<UpdateInfo?> CheckForUpdatesAsync();

    /// <summary>
    /// Download an update package
    /// </summary>
    /// <param name="updateInfo">Update information</param>
    /// <param name="progress">Progress reporter</param>
    /// <returns>True if download succeeded</returns>
    Task<bool> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<DownloadProgress>? progress = null);

    /// <summary>
    /// Install a downloaded update
    /// </summary>
    /// <param name="installerPath">Path to the downloaded installer</param>
    /// <returns>True if installation started successfully</returns>
    Task<bool> InstallUpdateAsync(string installerPath);

    /// <summary>
    /// Start background update checking
    /// </summary>
    void StartBackgroundChecking();

    /// <summary>
    /// Stop background update checking
    /// </summary>
    void StopBackgroundChecking();

    /// <summary>
    /// Get current update settings
    /// </summary>
    UpdateSettings GetSettings();

    /// <summary>
    /// Update settings
    /// </summary>
    /// <param name="settings">New settings</param>
    Task UpdateSettingsAsync(UpdateSettings settings);

    /// <summary>
    /// Check if auto-update is enabled in current environment
    /// </summary>
    bool IsAutoUpdateEnabled { get; }

    /// <summary>
    /// Event fired when an update becomes available
    /// </summary>
    event EventHandler<UpdateAvailableEventArgs>? UpdateAvailable;

    /// <summary>
    /// Event fired when update process completes (success or failure)
    /// </summary>
    event EventHandler<UpdateCompletedEventArgs>? UpdateCompleted;

    /// <summary>
    /// Event fired when download progress changes
    /// </summary>
    event EventHandler<DownloadProgress>? DownloadProgressChanged;
}