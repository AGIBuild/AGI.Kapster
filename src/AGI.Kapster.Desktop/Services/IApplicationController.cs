using System;
using System.Threading.Tasks;

namespace AGI.Kapster.Desktop.Services;

/// <summary>
/// Application controller interface for managing app lifecycle and startup
/// </summary>
public interface IApplicationController
{
    /// <summary>
    /// Initialize the application controller
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Enable or disable startup with Windows
    /// </summary>
    /// <param name="enabled">Whether to start with Windows</param>
    /// <returns>True if successful</returns>
    Task<bool> SetStartupWithWindowsAsync(bool enabled);

    /// <summary>
    /// Check if application is set to start with Windows
    /// </summary>
    /// <returns>True if enabled</returns>
    Task<bool> IsStartupWithWindowsEnabledAsync();


    /// <summary>
    /// Restart the application
    /// </summary>
    void RestartApplication();

    /// <summary>
    /// Exit the application gracefully
    /// </summary>
    void ExitApplication();
}
