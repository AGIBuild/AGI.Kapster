using AGI.Kapster.Desktop.Dialogs;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Services.Annotation;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Services.Export;
using AGI.Kapster.Desktop.Services.Export.Imaging;
using AGI.Kapster.Desktop.Services.Input;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using AGI.Kapster.Desktop.Services.Overlay.State;
using AGI.Kapster.Desktop.Services.Screenshot;
using AGI.Kapster.Desktop.Services.Settings;
using AGI.Kapster.Desktop.Services.UI;
using AGI.Kapster.Desktop.Overlays.Layers;
using AGI.Kapster.Desktop.Overlays.Layers.Selection;
using AGI.Kapster.Desktop.Overlays.Events;
using LayerSelectionMode = AGI.Kapster.Desktop.Overlays.Layers.Selection.SelectionMode;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AGI.Kapster.Desktop.Overlays;

public partial class OverlayWindow : Window, IOverlayWindow
{
    // Core services
    private readonly IElementDetector? _elementDetector;
    private readonly IScreenCoordinateMapper? _coordinateMapper;
    private readonly IToolbarPositionCalculator _toolbarPositionCalculator;
    private readonly ISettingsService _settingsService;
    private readonly IImeController _imeController;
    
    // New layered architecture
    private readonly IOverlayLayerManager _layerManager;
    private readonly IOverlayEventBus _eventBus;
    private IMaskLayer? _maskLayer;
    private ISelectionLayer? _selectionLayer;
    
    // Extracted services
    private readonly IOverlayImageCaptureService _imageCaptureService;
    private readonly IAnnotationExportService _exportService;
    
    // Legacy components (to be phased out gradually)
    private ElementHighlightOverlay? _elementHighlight;
    private NewAnnotationOverlay? _annotator;

    // Cached control references to avoid FindControl<>() abuse
    private Image? _backgroundImage;
    private Avalonia.Controls.Shapes.Path? _maskPath;
    private SelectionOverlay? _selector;
    private NewAnnotationToolbar? _toolbar;
    private Canvas? _uiCanvas;

    // Mask size is set by overlay controller based on platform strategy
    private Size _maskSize;
    
    // Session for this overlay (scoped state management)
    private IOverlaySession? _session;
    private IReadOnlyList<Screen>? _screens;

	// Frozen background (snapshot at overlay activation)
	private Bitmap? _frozenBackground;
	// Pre-captured Avalonia bitmap set before Show() for instant display
	private Bitmap? _precapturedBackground;

    // Public events for external consumers
    public event EventHandler<RegionSelectedEventArgs>? RegionSelected;
    public event EventHandler<OverlayCancelledEventArgs>? Cancelled;

    // Property to check element detection support
    public bool ElementDetectionEnabled { get; set; }

    public OverlayWindow(
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
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _imeController = imeController ?? throw new ArgumentNullException(nameof(imeController));
        _layerManager = layerManager ?? throw new ArgumentNullException(nameof(layerManager));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _imageCaptureService = imageCaptureService ?? throw new ArgumentNullException(nameof(imageCaptureService));
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        _elementDetector = elementDetector;
        _coordinateMapper = coordinateMapper;
        _toolbarPositionCalculator = toolbarPositionCalculator ?? new ToolbarPositionCalculator(coordinateMapper);
        
        // Fast initialization: only XAML parsing
        InitializeComponent();

        // Cache control references immediately after InitializeComponent
        CacheControlReferences();

        // Minimal setup for immediate display
        this.Cursor = new Cursor(StandardCursorType.Cross);

        // Set up event handlers
        this.PointerPressed += OnOverlayPointerPressed;
        this.PointerMoved += OnOverlayPointerMoved;

		// Opened event: initialize UI components
		// Note: Background is set automatically by SetPrecapturedAvaloniaBitmap when ready
		this.Opened += async (_, __) =>
		{
		    // Mask will be initialized in InitializeHeavyComponents -> InitializeLayers
		    
		    // Initialize heavy components immediately
		    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
		    {
		        InitializeHeavyComponents();
		    });
		};

		// Set focus to annotator when window is loaded
		this.Loaded += OnOverlayWindowLoaded;
    }

    /// <summary>
    /// Cache control references to avoid repeated FindControl<>() calls
    /// </summary>
    private void CacheControlReferences()
    {
        _backgroundImage = this.FindControl<Image>("BackgroundImage");
        _maskPath = this.FindControl<Avalonia.Controls.Shapes.Path>("MaskPath");
        _selector = this.FindControl<SelectionOverlay>("Selector");
        _toolbar = this.FindControl<NewAnnotationToolbar>("Toolbar");
        _uiCanvas = this.FindControl<Canvas>("UiCanvas");
        
        // Note: _annotator will be cached after creation in InitializeHeavyComponents
    }

    /// <summary>
    /// Initialize heavy UI components and layers after background is visible
    /// </summary>
    private void InitializeHeavyComponents()
    {
        // === Phase 1: Initialize legacy annotator (to be refactored later) ===
        _annotator = new NewAnnotationOverlay(_settingsService)
        {
            Name = "Annotator"
        };

        // Add annotator to grid (after Selector, before UiCanvas)
        if (this.Content is Grid grid && _selector != null && _uiCanvas != null)
        {
            var selectorIndex = grid.Children.IndexOf(_selector);
            grid.Children.Insert(selectorIndex + 1, _annotator);
            
            // Set focus to enable keyboard shortcuts
            _annotator.Focus();
        }

        // === Phase 2: Setup element highlight (legacy, still needed by ElementSelectionStrategy) ===
        SetupElementHighlight();

        if (_elementDetector != null && _elementHighlight != null)
        {
            _elementHighlight.IsActive = false; // Initially disabled
        }

        // === Phase 3: Initialize new layered architecture ===
        InitializeLayers();

        // === Phase 4: Setup legacy selection overlay (still needed by FreeSelectionStrategy) ===
        SetupSelectionOverlay();
        
        if (_selector != null)
        {
            _selector.IsVisible = true;
            _selector.IsHitTestVisible = true;
        }
    }

    /// <summary>
    /// Initialize the new layered architecture
    /// </summary>
    private void InitializeLayers()
    {
        if (_maskPath == null)
        {
            Log.Warning("MaskPath not found, cannot initialize layers");
            return;
        }

        // 1. Create and register MaskLayer
        _maskLayer = new MaskLayer(_maskPath, _eventBus);
        _maskLayer.SetMaskSize(_maskSize);
        _maskLayer.SetMaskColor(Colors.White);
        _maskLayer.SetMaskOpacity(0.25);
        _layerManager.RegisterLayer(_maskLayer.LayerId, _maskLayer);
        Log.Debug("MaskLayer registered");

        // 2. Create and register SelectionLayer (if element detection is supported)
        if (_elementDetector != null && _elementHighlight != null && _selector != null)
        {
            // Create strategies
            var freeStrategy = new FreeSelectionStrategyAdapter(_selector);
            var elementStrategy = new ElementSelectionStrategyAdapter(
                _elementDetector,
                _elementHighlight,
                _maskLayer,
                this,
                _eventBus);

            // Create selection layer
            _selectionLayer = new SelectionLayer(freeStrategy, elementStrategy, _eventBus);
            
            _layerManager.RegisterLayer(_selectionLayer.LayerId, _selectionLayer);
            Log.Debug("SelectionLayer registered with both strategies");
        }
        else if (_selector != null)
        {
            // Only free selection is available
            Log.Debug("Element detection not available, selection layer not created");
        }

        // 3. Activate mask layer
        _layerManager.SetActiveLayer(_maskLayer.LayerId);
        _maskLayer.OnActivate();
        
        Log.Debug("Layers initialized successfully");
    }

    private void OnOverlayWindowLoaded(object? sender, EventArgs e)
    {
        // Ensure annotator has focus for keyboard shortcuts
        if (_annotator != null)
        {
            _annotator.Focus();
            Log.Debug("Focus set to annotator after window loaded");

            // Also set focus to the window itself to ensure keyboard events are captured
            this.Focus();
            Log.Debug("Focus also set to overlay window");
        }

        // Disable IME to prevent input method interference with keyboard shortcuts
        DisableImeForOverlay();
    }

    /// <summary>
    /// Set pre-captured Avalonia bitmap and apply to UI if window is already initialized
    /// Can be called before or after Show() - will update UI automatically
    /// </summary>
    public void SetPrecapturedAvaloniaBitmap(Bitmap? bitmap)
    {
        _precapturedBackground = bitmap;
        
        // If window is already shown and controls are initialized, apply background immediately
        // This handles the case where pre-capture completes after Opened event
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (bitmap != null && _backgroundImage != null)
            {
                _backgroundImage.Source = bitmap;
                _frozenBackground = bitmap;
            }
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// Set mask size for platform-specific overlay strategies (called by controller before Show())
    /// </summary>
    /// <param name="width">Mask width (logical pixels)</param>
    /// <param name="height">Mask height (logical pixels)</param>
    public void SetMaskSize(double width, double height)
    {
        _maskSize = new Size(width, height);
        Log.Debug("Mask size set to: {Width}x{Height}", width, height);
    }
    
    /// <summary>
    /// Set the overlay session for this window (called by controller before Show())
    /// </summary>
    public void SetSession(IOverlaySession? session)
    {
        _session = session;
        Log.Debug("Overlay session set");
    }

    public void SetScreens(IReadOnlyList<Screen>? screens)
    {
        _screens = screens;
        Log.Debug("Overlay screens set: {Count} screen(s)", screens?.Count ?? 0);
    }
    
    /// <summary>
    /// Get the underlying Window instance (implements IOverlayWindow)
    /// Required for IOverlaySession.AddWindow(Window) compatibility
    /// </summary>
    public Window AsWindow() => this;
    
    /// <summary>
    /// Get the current overlay session
    /// </summary>
    internal IOverlaySession? GetSession() => _session;

    private void SetupSelectionOverlay()
    {
        if (_selector != null)
        {
            _selector.SelectionFinished += r =>
            {
                // Keep selection for annotation; don't capture yet
                Log.Information("Selection finished: {X},{Y} {W}x{H} - editable selection created", r.X, r.Y, r.Width, r.Height);

                // Hide selector to allow annotator interaction, but keep the selection rectangle visible
                if (_selector != null)
                {
                    // Disable hit testing so mouse events go to annotator
                    _selector.IsHitTestVisible = false;
                    Log.Debug("Selector interaction disabled after selection finished");
                }

                // Ensure focus is on annotator for keyboard shortcuts
                if (_annotator != null)
                {
                    _annotator.Focus();
                    Log.Debug("Focus set to annotator after selection finished");
                }

                // Raise public event with isEditableSelection = true
                RegionSelected?.Invoke(this, new RegionSelectedEventArgs(r, false, null, true));
            };

            // Create a hole in mask over selection using Path (even-odd)
            _selector.SelectionChanged += r =>
            {
                _maskLayer?.SetCutout(r); // Use layer system for mask updates
                if (_annotator != null)
                {
                    _annotator.SelectionRect = r;
                    _annotator.IsHitTestVisible = r.Width > 2 && r.Height > 2;
                }
                UpdateToolbarPosition(r);
            };

            _selector.ConfirmRequested += async r =>
            {
                Bitmap? finalImage = null;
                
                try
                {
                    Log.Debug("Capturing window region with annotations: {Region}", r);
                    finalImage = await _imageCaptureService.CaptureWindowRegionWithAnnotationsAsync(
                        r, _frozenBackground, GetAnnotationsFromAnnotator(), this.Bounds.Size);
                    Log.Debug("Window region captured successfully");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to capture window region, falling back to frozen background");
                    finalImage = await _imageCaptureService.GetBaseScreenshotForRegionAsync(
                        r, _frozenBackground, this.Bounds.Size, this);
                }

                // Raise region selected event with final image
                RegionSelected?.Invoke(this, new RegionSelectedEventArgs(r, false, null, false, finalImage));
            };
        }

        if (_toolbar != null && _annotator != null)
        {
            // Set default tool to Arrow first
            Log.Information("Setting default tool to Arrow");
            _annotator.CurrentTool = AnnotationToolType.Arrow;
            Log.Information("Default tool set to: {CurrentTool}", _annotator.CurrentTool);

            // Then set up toolbar (this will call UpdateUIFromTarget and sync the UI)
            _toolbar.SetTarget(_annotator);

            // Subscribe to export events
            _annotator.ExportRequested += HandleExportRequest;
            
            // Subscribe to color picker events
            _annotator.ColorPickerRequested += HandleColorPickerRequest;

            // Handle double-click confirm (unified cross-platform logic)
            _annotator.ConfirmRequested += async r =>
            {
                Bitmap? finalImage = null;
                
                try
                {
                    Log.Debug("Capturing window region with annotations: {Region}", r);
                    finalImage = await _imageCaptureService.CaptureWindowRegionWithAnnotationsAsync(
                        r, _frozenBackground, GetAnnotationsFromAnnotator(), this.Bounds.Size);
                    Log.Debug("Window region captured successfully");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to capture window region, falling back to frozen background");
                    finalImage = await _imageCaptureService.GetBaseScreenshotForRegionAsync(
                        r, _frozenBackground, this.Bounds.Size, this);
                }

                // Raise region selected event with final image
                RegionSelected?.Invoke(this, new RegionSelectedEventArgs(r, false, null, false, finalImage));

                var _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(OverlayConstants.StandardUiDelay);
                    CloseOverlayWithController("double-click save");
                });
            };
        }


        // Hide toolbar initially
        if (_toolbar != null)
        {
            Canvas.SetLeft(_toolbar, -10000);
            Canvas.SetTop(_toolbar, -10000);
        }

        // Clean up resources when this window closes
        this.Closing += (sender, e) =>
        {
            Log.Information("OverlayWindow: Window closing, cleaning up resources");
            
            // Restore IME state before closing
            EnableImeForOverlay();
            
            try
            {
                if (_frozenBackground != null)
                {
                    _frozenBackground.Dispose();
                    _frozenBackground = null;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error disposing frozen background");
            }
        };
    }

    /// <summary>
    /// Close all overlay windows using the screenshot service
    /// </summary>
    private void CloseOverlayWithController(string context)
    {
        // Trigger Cancelled event - let the coordinator handle cancellation
        Cancelled?.Invoke(this, new OverlayCancelledEventArgs(context));
        Log.Information("Overlay cancelled after {Context}", context);
    }

    private void UpdateToolbarPosition(Rect selection)
    {
        if (_toolbar == null || _uiCanvas == null)
            return;

        if (selection.Width <= 0 || selection.Height <= 0)
        {
            Canvas.SetLeft(_toolbar, -10000);
            Canvas.SetTop(_toolbar, -10000);
            return;
        }

        // Measure toolbar size
        _toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var toolbarSize = _toolbar.DesiredSize;

        // Calculate position using the calculator service
        var context = new ToolbarPositionContext(
            Selection: selection,
            ToolbarSize: toolbarSize,
            OverlayPosition: this.Position,
            Screens: _screens);

        var position = _toolbarPositionCalculator.CalculatePosition(context);

        Canvas.SetLeft(_toolbar, position.X);
        Canvas.SetTop(_toolbar, position.Y);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // ESC key always exits screenshot mode
        if (e.Key == Key.Escape)
        {
            Serilog.Log.Information("ESC key pressed - exiting screenshot mode");
            Cancelled?.Invoke(this, new OverlayCancelledEventArgs("User pressed ESC"));
            e.Handled = true;
            return;
        }

        // CTRL key: Switch to element selection mode
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        {
            if (_selectionLayer != null && _selectionLayer.CurrentMode == LayerSelectionMode.Free)
            {
                _selectionLayer.SwitchMode(LayerSelectionMode.Element);
                this.Cursor = new Cursor(StandardCursorType.Hand);
                Log.Debug("Switched to element selection mode via Ctrl key");
            }
            e.Handled = true;
            return;
        }

        // SPACE key: Toggle detection mode (window/element) when in element mode
        if (e.Key == Key.Space && _selectionLayer?.CurrentMode == LayerSelectionMode.Element)
        {
            _elementDetector?.ToggleDetectionMode();
            e.Handled = true;
            return;
        }

        // Ctrl+S: Export current selection
        if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (_annotator != null)
            {
                Log.Information("Ctrl+S pressed - triggering export via annotator");
                _annotator.RequestExport();
            }
            else
            {
                Log.Debug("Ctrl+S pressed but no annotator found");
            }
            e.Handled = true;
            return;
        }

        // Enter key: Confirm free selection
        if (e.Key == Key.Enter)
        {
            if (_selector != null)
            {
                var r = _selector.SelectionRect;
                if (r.Width > 0)
                {
                    // Raise region selected event
                    RegionSelected?.Invoke(this, new RegionSelectedEventArgs(r, false));

                    var _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await System.Threading.Tasks.Task.Delay(OverlayConstants.StandardUiDelay);
                        CloseOverlayWithController("Enter key save");
                    });
                }
            }
            e.Handled = true;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        // CTRL key released: Return to free selection mode
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        {
            if (_selectionLayer != null && _selectionLayer.CurrentMode == LayerSelectionMode.Element)
            {
                _selectionLayer.SwitchMode(LayerSelectionMode.Free);
                this.Cursor = new Cursor(StandardCursorType.Cross);
                
                // Clear mask cutout
                _maskLayer?.ClearCutout();
                
                Log.Debug("Returned to free selection mode via Ctrl key release");
            }
            e.Handled = true;
        }
    }

    private void SetupElementHighlight()
    {
        if (_elementDetector == null) return;

        _elementHighlight = new ElementHighlightOverlay(_elementDetector);

        // Add to the grid after SelectionOverlay but before AnnotationOverlay
        var grid = (Grid)this.Content!;
        if (_selector != null)
        {
            var index = grid.Children.IndexOf(_selector) + 1;
            grid.Children.Insert(index, _elementHighlight);
        }
        else
        {
            grid.Children.Add(_elementHighlight);
        }
    }

    private void OnOverlayPointerMoved(object? sender, PointerEventArgs e)
    {
        // In element selection mode, route to selection layer for element detection
        if (_selectionLayer?.CurrentMode == LayerSelectionMode.Element)
        {
            _selectionLayer.HandlePointerEvent(e);
        }
        // In free selection mode, _selector handles events through Avalonia's event system
    }

    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // In element selection mode, route to selection layer for element selection
        if (_selectionLayer?.CurrentMode == LayerSelectionMode.Element)
        {
            _selectionLayer.HandlePointerEvent(e);
        }
        // In free selection mode, _selector handles events through Avalonia's event system
    }

    /// <summary>
    /// Get annotations from annotator
    /// </summary>
    private IEnumerable<IAnnotationItem>? GetAnnotationsFromAnnotator()
    {
        return _annotator?.GetAnnotations();
    }

    /// <summary>
    /// Get full screen screenshot for color sampling
    /// </summary>
    public async Task<Bitmap?> GetFullScreenScreenshotAsync()
    {
        return await _imageCaptureService.GetFullScreenScreenshotAsync(this, this.Bounds.Size);
    }





    #region Export Functionality

    /// <summary>
    /// Handle export request from annotation overlay
    /// </summary>
    private async void HandleExportRequest()
    {
        if (_annotator == null)
            return;

        await _exportService.HandleExportRequestAsync(
            this,
            _annotator.SelectionRect,
            async () =>
            {
                // Hide selector temporarily
                bool selectorWasVisible = _selector?.IsVisible ?? false;
                if (_selector != null)
                    _selector.IsVisible = false;

                try
                {
                    // End text editing before capture
                    _annotator?.EndTextEditing();
                    await Task.Delay(OverlayConstants.StandardUiDelay);

                    return await _imageCaptureService.CaptureWindowRegionWithAnnotationsAsync(
                        _annotator.SelectionRect, _frozenBackground, GetAnnotationsFromAnnotator(), this.Bounds.Size);
                }
                finally
                {
                    // Restore selector
                    if (_selector != null && selectorWasVisible)
                        _selector.IsVisible = true;
                }
            },
            () => CloseOverlayWithController("export"));
    }

    /// <summary>
    /// Handle color picker request from annotation overlay
    /// </summary>
    private void HandleColorPickerRequest()
    {
        try
        {
            if (_toolbar != null)
            {
                _toolbar.ShowColorPicker();
                Log.Debug("Color picker opened via keyboard shortcut");
            }
            else
            {
                Log.Warning("Could not find toolbar to open color picker");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to handle color picker request");
        }
    }

    #endregion

    #region IME Control

    /// <summary>
    /// Disable IME for overlay window to prevent input method interference with shortcuts
    /// </summary>
    private void DisableImeForOverlay()
    {
        if (!_imeController.IsSupported)
            return;

        try
        {
            var handle = TryGetPlatformHandle()?.Handle ?? nint.Zero;
            if (handle != nint.Zero)
            {
                _imeController.DisableIme(handle);
                Log.Debug("IME disabled for overlay window");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to disable IME for overlay window");
        }
    }

    /// <summary>
    /// Enable IME for overlay window (called when text editing starts or window closes)
    /// </summary>
    private void EnableImeForOverlay()
    {
        if (!_imeController.IsSupported)
            return;

        try
        {
            var handle = TryGetPlatformHandle()?.Handle ?? nint.Zero;
            if (handle != nint.Zero)
            {
                _imeController.EnableIme(handle);
                Log.Debug("IME enabled for overlay window");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to enable IME for overlay window");
        }
    }

    /// <summary>
    /// Public method for annotation overlay to enable IME during text editing
    /// </summary>
    public void EnableImeForTextEditing()
    {
        EnableImeForOverlay();
    }

    /// <summary>
    /// Public method for annotation overlay to disable IME after text editing
    /// </summary>
    public void DisableImeAfterTextEditing()
    {
        DisableImeForOverlay();
    }

    #endregion
}

