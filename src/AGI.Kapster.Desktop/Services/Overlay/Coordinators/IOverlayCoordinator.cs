using System.Threading.Tasks;
using AGI.Kapster.Desktop.Services.Overlay.State;

namespace AGI.Kapster.Desktop.Services.Overlay.Coordinators;

/// <summary>
/// Orchestrates the creation and lifecycle of overlay sessions
/// Platform-specific implementations handle window creation and positioning
/// </summary>
public interface IOverlayCoordinator
{
    /// <summary>
    /// Create and show a new screenshot session
    /// </summary>
    /// <returns>The created session</returns>
    Task<IOverlaySession> CreateAndShowSessionAsync();

    /// <summary>
    /// Close the current active session
    /// </summary>
    void CloseCurrentSession();

    /// <summary>
    /// Check if there's an active session
    /// </summary>
    bool HasActiveSession { get; }
}

