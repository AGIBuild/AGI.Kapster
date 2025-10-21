using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;

namespace AGI.Kapster.Desktop.Services.Overlay;

/// <summary>
/// Factory for creating OverlayWindow instances with DI-injected dependencies
/// Registered as Singleton, but creates Transient OverlayWindow instances
/// </summary>
public class OverlayWindowFactory : IOverlayWindowFactory
{
    private readonly IElementDetector? _elementDetector;
    private readonly IScreenCaptureStrategy? _screenCaptureStrategy;
    private readonly IScreenCoordinateMapper? _coordinateMapper;

    public OverlayWindowFactory(
        IElementDetector? elementDetector = null,
        IScreenCaptureStrategy? screenCaptureStrategy = null,
        IScreenCoordinateMapper? coordinateMapper = null)
    {
        _elementDetector = elementDetector;
        _screenCaptureStrategy = screenCaptureStrategy;
        _coordinateMapper = coordinateMapper;
    }

    public OverlayWindow Create()
    {
        return new OverlayWindow(_elementDetector, _screenCaptureStrategy, _coordinateMapper);
    }
}

