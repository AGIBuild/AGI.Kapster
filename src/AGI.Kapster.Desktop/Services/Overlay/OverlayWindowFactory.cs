using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Services.Input;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using AGI.Kapster.Desktop.Services.Settings;
using AGI.Kapster.Desktop.Services.UI;

namespace AGI.Kapster.Desktop.Services.Overlay;

/// <summary>
/// Factory for creating OverlayWindow instances with DI-injected dependencies
/// Registered as Singleton, but creates Transient OverlayWindow instances
/// </summary>
public class OverlayWindowFactory : IOverlayWindowFactory
{
    private readonly ISettingsService _settingsService;
    private readonly IImeController _imeController;
    private readonly IElementDetector? _elementDetector;
    private readonly IScreenCaptureStrategy? _screenCaptureStrategy;
    private readonly IScreenCoordinateMapper? _coordinateMapper;
    private readonly IToolbarPositionCalculator? _toolbarPositionCalculator;

    public OverlayWindowFactory(
        ISettingsService settingsService,
        IImeController imeController,
        IElementDetector? elementDetector = null,
        IScreenCaptureStrategy? screenCaptureStrategy = null,
        IScreenCoordinateMapper? coordinateMapper = null,
        IToolbarPositionCalculator? toolbarPositionCalculator = null)
    {
        _settingsService = settingsService;
        _imeController = imeController;
        _elementDetector = elementDetector;
        _screenCaptureStrategy = screenCaptureStrategy;
        _coordinateMapper = coordinateMapper;
        _toolbarPositionCalculator = toolbarPositionCalculator;
    }

    public IOverlayWindow Create()
    {
        return new OverlayWindow(
            _settingsService,
            _imeController,
            _elementDetector, 
            _screenCaptureStrategy, 
            _coordinateMapper,
            _toolbarPositionCalculator);
    }
}

