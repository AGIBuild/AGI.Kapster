using System.Threading.Tasks;

namespace AGI.Kapster.Desktop.Services;

/// <summary>
/// Platform-agnostic startup manager interface
/// </summary>
public interface IStartupManager
{
    /// <summary>
    /// Check if the platform supports startup configuration
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Enable or disable startup with system
    /// </summary>
    Task<bool> SetStartupAsync(bool enabled);

    /// <summary>
    /// Check if startup with system is currently enabled
    /// </summary>
    Task<bool> IsStartupEnabledAsync();
}

