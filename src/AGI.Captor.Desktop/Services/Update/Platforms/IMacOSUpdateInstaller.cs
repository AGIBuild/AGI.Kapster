using System.Threading.Tasks;

namespace AGI.Captor.Desktop.Services.Update.Platforms;

/// <summary>
/// Interface for macOS-specific update installation
/// </summary>
public interface IMacOSUpdateInstaller
{
    /// <summary>
    /// Install update package on macOS
    /// </summary>
    /// <param name="packagePath">Path to the .pkg or .dmg file</param>
    /// <returns>True if installation started successfully</returns>
    Task<bool> InstallUpdateAsync(string packagePath);

    /// <summary>
    /// Check if the current user has permissions to install updates
    /// </summary>
    /// <returns>True if user can install updates</returns>
    bool CanInstallUpdates();
}