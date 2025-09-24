using System;
using System.ComponentModel;

namespace AGI.Kapster.Desktop.Models.Update;

/// <summary>
/// Update information model
/// </summary>
public class UpdateInfo
{
    /// <summary>
    /// Version number (e.g., "1.2.1")
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Release name/title
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Release description/changelog
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Release publish date
    /// </summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// Whether this is a pre-release version
    /// </summary>
    public bool IsPreRelease { get; set; }

    /// <summary>
    /// Direct download URL for the MSI installer
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// SHA256 hash for file verification
    /// </summary>
    public string Sha256Hash { get; set; } = string.Empty;

    /// <summary>
    /// GitHub release URL
    /// </summary>
    public string ReleaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Check if this version is newer than the current version
    /// </summary>
    public bool IsNewerThan(System.Version currentVersion)
    {
        if (!System.Version.TryParse(Version, out var updateVersion))
            return false;

        return updateVersion > currentVersion;
    }
}

/// <summary>
/// Update settings configuration
/// </summary>
public class UpdateSettings
{
    /// <summary>
    /// Whether automatic updates are enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Frequency of update checks in hours
    /// </summary>
    public int CheckFrequencyHours { get; set; } = 24;

    /// <summary>
    /// Whether to install updates automatically
    /// </summary>
    public bool InstallAutomatically { get; set; } = true;

    /// <summary>
    /// Whether to notify user before installing
    /// </summary>
    public bool NotifyBeforeInstall { get; set; } = false;

    /// <summary>
    /// Whether to include pre-release versions
    /// </summary>
    public bool UsePreReleases { get; set; } = false;

    /// <summary>
    /// Last successful update check time
    /// </summary>
    public DateTime LastCheckTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Override to enable updates in debug mode (for testing)
    /// When null, uses default build configuration behavior
    /// When true, enables updates even in debug builds
    /// When false, disables updates (same as debug mode)
    /// </summary>
    public bool? EnableInDebugMode { get; set; } = null;

    /// <summary>
    /// GitHub repository URL for update checks
    /// When null, uses default AGIBuild/AGI.Kapster repository
    /// Format: "owner/repository" or full API URL
    /// </summary>
    public string? GitHubRepository { get; set; } = null;
}

/// <summary>
/// Download progress information
/// </summary>
public class DownloadProgress
{
    /// <summary>
    /// Total bytes to download
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// Bytes downloaded so far
    /// </summary>
    public long DownloadedBytes { get; set; }

    /// <summary>
    /// Download percentage (0-100)
    /// </summary>
    public int ProgressPercentage => TotalBytes > 0 ? (int)((DownloadedBytes * 100) / TotalBytes) : 0;

    /// <summary>
    /// Download percentage (0-100) - alias for ProgressPercentage
    /// </summary>
    public int PercentComplete => ProgressPercentage;

    /// <summary>
    /// Download speed in bytes per second
    /// </summary>
    public long BytesPerSecond { get; set; }

    /// <summary>
    /// Estimated time remaining
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }
}

/// <summary>
/// Update availability event arguments
/// </summary>
public class UpdateAvailableEventArgs : EventArgs
{
    public UpdateInfo UpdateInfo { get; }
    public bool IsAutomatic { get; }

    public UpdateAvailableEventArgs(UpdateInfo updateInfo, bool isAutomatic = true)
    {
        UpdateInfo = updateInfo;
        IsAutomatic = isAutomatic;
    }
}

/// <summary>
/// Update completion event arguments
/// </summary>
public class UpdateCompletedEventArgs : EventArgs
{
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public UpdateInfo? UpdateInfo { get; }

    public UpdateCompletedEventArgs(bool success, UpdateInfo? updateInfo = null, string? errorMessage = null)
    {
        Success = success;
        UpdateInfo = updateInfo;
        ErrorMessage = errorMessage;
    }
}