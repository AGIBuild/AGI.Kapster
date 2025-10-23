using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Overlays.Layers;
using AGI.Kapster.Desktop.Overlays.Events;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Services.Export;
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
    private readonly IOverlayLayerManager _layerManager;
    private readonly IOverlayEventBus _eventBus;
    private readonly IElementDetector? _elementDetector;
    private readonly IOverlayImageCaptureService _imageCaptureService;
    private readonly IAnnotationExportService _exportService;
    private readonly IScreenCoordinateMapper? _coordinateMapper;
    private readonly IToolbarPositionCalculator? _toolbarPositionCalculator;

    public OverlayWindowFactory(
        ISettingsService settingsService,
        IImeController imeController,
        IOverlayLayerManager layerManager,
        IOverlayEventBus eventBus,
        IOverlayImageCaptureService imageCaptureService,
        IAnnotationExportService exportService,
        IElementDetector? elementDetector = null,
        IScreenCoordinateMapper? coordinateMapper = null,
        IToolbarPositionCalculator? toolbarPositionCalculator = null)
    {
        _settingsService = settingsService;
        _imeController = imeController;
        _layerManager = layerManager;
        _eventBus = eventBus;
        _imageCaptureService = imageCaptureService;
        _exportService = exportService;
        _elementDetector = elementDetector;
        _coordinateMapper = coordinateMapper;
        _toolbarPositionCalculator = toolbarPositionCalculator;
    }

    public IOverlayWindow Create()
    {
        return new OverlayWindow(
            _settingsService,
            _imeController,
            _layerManager,
            _eventBus,
            _imageCaptureService,
            _exportService,
            _elementDetector,
            _coordinateMapper,
            _toolbarPositionCalculator);
    }
}

