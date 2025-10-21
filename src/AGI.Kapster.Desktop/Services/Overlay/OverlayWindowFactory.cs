using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.ElementDetection;

namespace AGI.Kapster.Desktop.Services.Overlay;

/// <summary>
/// Factory for creating OverlayWindow instances with DI-injected dependencies
/// Registered as Singleton, but creates Transient OverlayWindow instances
/// </summary>
public class OverlayWindowFactory : IOverlayWindowFactory
{
    private readonly IElementDetector? _elementDetector;
    private readonly IScreenCaptureStrategy? _screenCaptureStrategy;

    public OverlayWindowFactory(
        IElementDetector? elementDetector = null,
        IScreenCaptureStrategy? screenCaptureStrategy = null)
    {
        _elementDetector = elementDetector;
        _screenCaptureStrategy = screenCaptureStrategy;
    }

    public OverlayWindow Create()
    {
        return new OverlayWindow(_elementDetector, _screenCaptureStrategy);
    }
}

