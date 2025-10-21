using AGI.Kapster.Desktop.Overlays;

namespace AGI.Kapster.Desktop.Services.Overlay;

/// <summary>
/// Factory interface for creating IOverlayWindow instances
/// Allows dependency injection of OverlayWindow dependencies
/// </summary>
public interface IOverlayWindowFactory
{
    /// <summary>
    /// Creates a new IOverlayWindow instance with injected dependencies
    /// </summary>
    /// <returns>New IOverlayWindow instance</returns>
    IOverlayWindow Create();
}

