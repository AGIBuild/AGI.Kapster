using AGI.Kapster.Desktop.Overlays;

namespace AGI.Kapster.Desktop.Services.Overlay;

/// <summary>
/// Factory for creating OverlayWindow instances with DI-injected dependencies
/// </summary>
public interface IOverlayWindowFactory
{
    /// <summary>
    /// Create a new OverlayWindow instance with injected dependencies
    /// </summary>
    OverlayWindow Create();
}

