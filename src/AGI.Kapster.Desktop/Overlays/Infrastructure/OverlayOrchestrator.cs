using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Overlays.Coordinators;
using AGI.Kapster.Desktop.Overlays.Events;
using AGI.Kapster.Desktop.Overlays.Layers;
using AGI.Kapster.Desktop.Overlays.Layers.Selection;
using AGI.Kapster.Desktop.Services.Annotation;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Services.Export;
using AGI.Kapster.Desktop.Services.Input;
using AGI.Kapster.Desktop.Services.Overlay.State;
using AGI.Kapster.Desktop.Services.Settings;
using AGI.Kapster.Desktop.Services.UI;
using AGI.Kapster.Desktop.Services.Clipboard;
using Serilog;

namespace AGI.Kapster.Desktop.Overlays.Infrastructure;

/// <summary>
/// Orchestrator implementation - centralizes overlay subsystems and reduces OverlayWindow dependencies
/// </summary>
public class OverlayOrchestrator : IOverlayOrchestrator
{
    private readonly IOverlayLayerManager _layerManager;
    private readonly IOverlayEventBus _eventBus;
    private readonly OverlayContextProvider _contextProvider;
    private readonly InputRouter _inputRouter;
    private readonly IOverlayImageCaptureService _imageCaptureService;
    private readonly IAnnotationExportService _exportService;
    private readonly ISettingsService _settingsService;
    private readonly IToolbarPositionCalculator _toolbarPositionCalculator;
    private readonly IElementDetector? _elementDetector;
    private readonly IImeController _imeController;
    private readonly IClipboardStrategy _clipboardStrategy;
    
    private OverlayEventCoordinator? _coordinator;
    private IOverlayActionHandler? _actionHandler;
    private ILayerHost? _host;
    private TopLevel? _window;
    private IOverlayContext? _currentContext;
    private Size _maskSize;
    private IOverlaySession? _session; // Passed in Initialize, used by SelectionLayer

    // Layers
    private IMaskLayer? _maskLayer;
    private ISelectionLayer? _selectionLayer;
    private IAnnotationLayer? _annotationLayer;
    private IToolbarLayer? _toolbarLayer;
    
    // Callbacks for notifying Session (no reverse dependency)
    public Action<object?, RegionSelectedEventArgs>? OnRegionSelected { get; set; }
    public Action<string>? OnCancelled { get; set; }

    public OverlayOrchestrator(
        IOverlayLayerManager layerManager,
        IOverlayEventBus eventBus,
        OverlayContextProvider contextProvider,
        InputRouter inputRouter,
        IOverlayImageCaptureService imageCaptureService,
        IAnnotationExportService exportService,
        ISettingsService settingsService,
        IToolbarPositionCalculator toolbarPositionCalculator,
        IImeController imeController,
        IClipboardStrategy clipboardStrategy,
        IElementDetector? elementDetector = null)
    {
        _layerManager = layerManager ?? throw new ArgumentNullException(nameof(layerManager));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
        _inputRouter = inputRouter ?? throw new ArgumentNullException(nameof(inputRouter));
        _imageCaptureService = imageCaptureService ?? throw new ArgumentNullException(nameof(imageCaptureService));
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _toolbarPositionCalculator = toolbarPositionCalculator ?? throw new ArgumentNullException(nameof(toolbarPositionCalculator));
        _imeController = imeController ?? throw new ArgumentNullException(nameof(imeController));
        _clipboardStrategy = clipboardStrategy ?? throw new ArgumentNullException(nameof(clipboardStrategy));
        _elementDetector = elementDetector;

        Log.Debug("OverlayOrchestrator created");
    }

    public void Initialize(TopLevel window, ILayerHost host, Size maskSize, IOverlaySession session, IReadOnlyList<Screen>? screens = null)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _maskSize = maskSize;
        _session = session ?? throw new ArgumentNullException(nameof(session));

        // Build initial context with screens information
        _currentContext = _contextProvider.BuildContext(window, null, screens);
        Log.Debug("Orchestrator: Context initialized with {ScreenCount} screens", screens?.Count ?? 0);

        // Create action handler with direct ContextProvider reference and ClipboardStrategy
        _actionHandler = new OverlayActionHandler(
            window as Window ?? throw new ArgumentException("Window must be of type Window"),
            _exportService,
            _layerManager,
            _imageCaptureService,
            _contextProvider,
            () => window.Bounds.Size,
            _clipboardStrategy);

        // Create coordinator
        _coordinator = new OverlayEventCoordinator(_eventBus, _layerManager, _actionHandler);
        _coordinator.SetOrchestrator(this); // Set orchestrator reference for IME control
        
        // Use callback to notify Session when region is selected (no reverse dependency)
        _coordinator.RegionSelected += (s, e) =>
        {
            OnRegionSelected?.Invoke(this, e);
        };

        // Disable IME at startup
        DisableIme();

        Log.Debug("OverlayOrchestrator initialized");
    }


    public void BuildLayers()
    {
        if (_host == null || _currentContext == null || _window == null)
        {
            Log.Error("Cannot build layers: Orchestrator not initialized");
            return;
        }

        // 1. Create and register MaskLayer
        var maskLayer = new MaskLayer(_eventBus);
        maskLayer.SetMaskSize(_maskSize);
        maskLayer.SetMaskColor(Colors.White);
        maskLayer.SetMaskOpacity(0.25);
        
        // Phase 3: Inject LayerManager for state management (requires concrete type)
        maskLayer.SetLayerManager(_layerManager);
        
        _maskLayer = maskLayer;
        _layerManager.RegisterAndAttachLayer(_maskLayer.LayerId, _maskLayer, _host, _currentContext);
        Log.Debug("Orchestrator: MaskLayer registered");

        // 2. Create and register SelectionLayer
        var selectionLayer = _elementDetector != null
            ? new SelectionLayer(_elementDetector, _maskLayer, _window as Window ?? throw new InvalidOperationException(), _eventBus)
            : new SelectionLayer(_eventBus);
        
        // Phase 2: Inject LayerManager for state management (requires concrete type)
        selectionLayer.SetLayerManager(_layerManager);
        
        // Inject Session for element highlight coordination
        if (_session != null)
        {
            selectionLayer.SetSession(_session);
        }
        else
        {
            Log.Warning("Orchestrator.BuildLayers: Session not set, element highlight will use fallback singleton");
        }
        
        _selectionLayer = selectionLayer;
        _layerManager.RegisterAndAttachLayer(_selectionLayer.LayerId, _selectionLayer, _host, _currentContext);
        Log.Debug("Orchestrator: SelectionLayer registered");

        // 3. Create and register AnnotationLayer
        var annotationLayer = new AnnotationLayer(_settingsService, _eventBus);
        annotationLayer.SetTool(AnnotationToolType.Arrow);
        
        // Phase 3: Inject LayerManager for state management
        annotationLayer.SetLayerManager(_layerManager);
        
        _annotationLayer = annotationLayer;
        _layerManager.RegisterAndAttachLayer(_annotationLayer.LayerId, _annotationLayer, _host, _currentContext);
        Log.Debug("Orchestrator: AnnotationLayer registered with default tool: Arrow");

        // 4. Create and register ToolbarLayer
        var toolbarLayer = new ToolbarLayer(_eventBus, _toolbarPositionCalculator);
        toolbarLayer.SetAnnotationLayer(_annotationLayer);
        
        // Phase 3: Inject LayerManager for state management
        toolbarLayer.SetLayerManager(_layerManager);
        
        _toolbarLayer = toolbarLayer;
        _layerManager.RegisterAndAttachLayer(_toolbarLayer.LayerId, _toolbarLayer, _host, _currentContext);
        Log.Debug("Orchestrator: ToolbarLayer registered");

        // 🔧 CRITICAL FIX: Activate initial mode through LayerManager
        // This ensures all layers that CanHandle(FreeSelection) are properly activated
        // DO NOT manually call OnActivate() - let SwitchMode manage lifecycle
        Log.Debug("🔧 Orchestrator.BuildLayers: About to switch to FreeSelection mode");
        
        _layerManager.SwitchMode(OverlayMode.FreeSelection);
        
        Log.Debug("🔧 Orchestrator.BuildLayers COMPLETED: Initial mode activated: FreeSelection");
        
        // Diagnostic: Check SelectionLayer state
        if (_selectionLayer != null)
        {
            Log.Debug("🔧 Orchestrator.BuildLayers: SelectionLayer state - IsVisible={IsVisible}, IsInteractive={IsInteractive}, CanHandle(FreeSelection)={CanHandle}",
                _selectionLayer.IsVisible, _selectionLayer.IsInteractive, _selectionLayer.CanHandle(OverlayMode.FreeSelection));
        }
    }

    public void PublishContextChanged(Size overlaySize, PixelPoint overlayPosition, IReadOnlyList<Screen>? screens)
    {
        _currentContext = _contextProvider.UpdateContext(overlaySize, overlayPosition, screens);
        _eventBus.Publish(new OverlayContextChangedEvent(overlaySize, overlayPosition, screens ?? Array.Empty<Screen>()));
        Log.Debug("Orchestrator: Context changed published");
    }

    public bool RouteKeyEvent(KeyEventArgs e)
    {
        return _inputRouter.RouteKeyEvent(e);
    }

    public bool RoutePointerEvent(PointerEventArgs e)
    {
        return _inputRouter.RoutePointerEvent(e);
    }

    public void SetFrozenBackground(Bitmap? background)
    {
        _contextProvider.SetFrozenBackground(background);
    }

    public void SetScreens(IReadOnlyList<Screen>? screens)
    {
        _contextProvider.SetScreens(screens);
    }

    public async Task<Bitmap?> GetFullScreenScreenshotAsync(Size bounds)
    {
        if (_window == null || _window is not Window window) return null;
        return await _imageCaptureService.GetFullScreenScreenshotAsync(window, bounds);
    }

    public void EnableImeForTextEditing()
    {
        if (!_imeController.IsSupported || _window == null)
            return;

        try
        {
            var handle = (_window as Window)?.TryGetPlatformHandle()?.Handle ?? nint.Zero;
            if (handle != nint.Zero)
            {
                _imeController.EnableIme(handle);
                Log.Debug("OverlayOrchestrator: IME enabled for text editing");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "OverlayOrchestrator: Failed to enable IME");
        }
    }

    public void DisableImeAfterTextEditing()
    {
        DisableIme();
        Log.Debug("OverlayOrchestrator: IME disabled after text editing");
    }

    private void DisableIme()
    {
        if (!_imeController.IsSupported || _window == null)
            return;

        try
        {
            var handle = (_window as Window)?.TryGetPlatformHandle()?.Handle ?? nint.Zero;
            if (handle != nint.Zero)
            {
                _imeController.DisableIme(handle);
                Log.Debug("OverlayOrchestrator: IME disabled");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "OverlayOrchestrator: Failed to disable IME");
        }
    }

    private void EnableIme()
    {
        if (!_imeController.IsSupported || _window == null)
            return;

        try
        {
            var handle = (_window as Window)?.TryGetPlatformHandle()?.Handle ?? nint.Zero;
            if (handle != nint.Zero)
            {
                _imeController.EnableIme(handle);
                Log.Debug("OverlayOrchestrator: IME enabled (cleanup)");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "OverlayOrchestrator: Failed to enable IME");
        }
    }

    public void Dispose()
    {
        // Restore IME before disposal
        EnableIme();
        
        _coordinator?.Dispose();
        _contextProvider?.Dispose();
        Log.Debug("OverlayOrchestrator disposed");
    }
}

