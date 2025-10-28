using AGI.Kapster.Desktop.Overlays.Handlers;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Services.Input;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using AGI.Kapster.Desktop.Services.Overlay.State;
using AGI.Kapster.Desktop.Services.Settings;
using AGI.Kapster.Desktop.Services.UI;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AGI.Kapster.Desktop.Overlays;

public partial class OverlayWindow : Window, IOverlayWindow
{
    private readonly IElementDetector? _elementDetector;
    private readonly IScreenCaptureStrategy? _screenCaptureStrategy;
    private readonly IScreenCoordinateMapper? _coordinateMapper;
    private readonly IToolbarPositionCalculator _toolbarPositionCalculator;
    private readonly ISettingsService _settingsService;
    private readonly IImeController _imeController;
    
    // Handlers for separate concerns
    private ImeHandler? _imeHandler;
    private ToolbarHandler? _toolbarHandler;
    private ElementDetectionHandler? _elementDetectionHandler;
    private AnnotationHandler? _annotationHandler;
    private SelectionHandler? _selectionHandler;
    private CaptureHandler? _captureHandler;
    
    private ElementHighlightOverlay? _elementHighlight;
    private NewAnnotationOverlay? _annotator; // Keep reference to correct annotator instance

    // Initialization state tracking
    private bool _componentsInitialized = false;
    private bool _backgroundReady = false;
    private bool _focusSet = false;

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
    public bool ElementDetectionEnabled
    {
        get => _selectionHandler?.SelectionMode == OverlaySelectionMode.ElementPicker;
        set => _selectionHandler?.SetElementPickerMode(value);
    }

    public OverlayWindow(
        ISettingsService settingsService,
        IImeController imeController,
        IElementDetector? elementDetector = null, 
        IScreenCaptureStrategy? screenCaptureStrategy = null, 
        IScreenCoordinateMapper? coordinateMapper = null,
        IToolbarPositionCalculator? toolbarPositionCalculator = null)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _imeController = imeController ?? throw new ArgumentNullException(nameof(imeController));
        _elementDetector = elementDetector;
        _screenCaptureStrategy = screenCaptureStrategy;
        _coordinateMapper = coordinateMapper;
        _toolbarPositionCalculator = toolbarPositionCalculator ?? new ToolbarPositionCalculator(coordinateMapper);
        
        Log.Debug("OverlayWindow constructor: elementDetector = {Detector}", 
            elementDetector?.GetType().Name ?? "null");
        
        // Fast initialization: only XAML parsing
        InitializeComponent();

        // Cache control references immediately after InitializeComponent
        CacheControlReferences();
        
        // Initialize handlers (IME and Annotation handlers can be created immediately)
        _imeHandler = new ImeHandler(this, _imeController);
        _annotationHandler = new AnnotationHandler(_settingsService);

        // Minimal setup for immediate display
        this.Cursor = new Cursor(StandardCursorType.Cross);

        // Set up mouse event handlers
        this.PointerPressed += OnOverlayPointerPressed;
        this.PointerMoved += OnOverlayPointerMoved;
        
        // Use tunneling events (PreviewKeyDown/Up) to intercept Ctrl key before children
        // This ensures OverlayWindow receives Ctrl key events even when focus is on child controls
        this.AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        this.AddHandler(KeyUpEvent, OnPreviewKeyUp, RoutingStrategies.Tunnel);

		// Opened event: initialize UI components synchronously
		// Note: Background is set automatically by SetPrecapturedAvaloniaBitmap when ready
		this.Opened += (_, __) =>
		{
		    UpdateMaskForSelection(default);
		    
		    // Initialize components in dependency order
		    InitializeComponents();
		};
		
		// Setup window cleanup
		SetupWindowCleanup();
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
    /// Initialize components in proper dependency order
    /// Phase 1: Core UI components
    /// Phase 2: Handlers that depend on UI components  
    /// Phase 3: Event subscriptions and final setup
    /// </summary>
    private void InitializeComponents()
    {
        Log.Debug("Starting component initialization");
        
        // Phase 1: Initialize core UI components
        InitializeCoreUIComponents();
        
        // Phase 2: Initialize handlers in dependency order
        InitializeHandlers();
        
        // Phase 3: Setup event subscriptions and final configuration
        SetupEventSubscriptions();
        
        // Mark components as initialized
        _componentsInitialized = true;
        Log.Debug("Component initialization completed");
        
        // Try to set focus if background is already ready
        TrySetFocusWhenReady();
    }

    /// <summary>
    /// Phase 1: Initialize core UI components (Annotator, ElementHighlight)
    /// </summary>
    private void InitializeCoreUIComponents()
    {
        Log.Debug("Phase 1: Initializing core UI components");
        
        // Initialize annotator using handler
        if (this.Content is Grid grid && _selector != null && _toolbar != null && _annotationHandler != null)
        {
            _annotator = _annotationHandler.InitializeAnnotator(grid, _selector, _toolbar);
            Log.Debug("Annotator initialized");
        }

        SetupElementHighlight();

        if (_elementDetector != null && _elementHighlight != null)
        {
            _elementHighlight.IsActive = false; // Initially disabled
            Log.Debug("Element highlight setup completed");
        }
    }

    /// <summary>
    /// Phase 2: Initialize handlers in dependency order
    /// </summary>
    private void InitializeHandlers()
    {
        Log.Debug("Phase 2: Initializing handlers");
        
        // SelectionHandler - depends on _selector
        if (_selector != null)
        {
            _selectionHandler = new SelectionHandler(this, _selector);
            Log.Debug("SelectionHandler initialized");
        }

        // ToolbarHandler - depends on _toolbar and _uiCanvas
        if (_toolbar != null && _uiCanvas != null)
        {
            _toolbarHandler = new ToolbarHandler(this, _uiCanvas, _toolbar, _toolbarPositionCalculator);
            _toolbarHandler.HideToolbar(); // Hide initially
            Log.Debug("ToolbarHandler initialized");
        }

        // CaptureHandler - depends on _selector and _annotationHandler
        if (_selector != null && _annotationHandler != null)
        {
            _captureHandler = new CaptureHandler(
                this, _selector, _annotationHandler,
                _screenCaptureStrategy, _coordinateMapper,
                () => _frozenBackground,  // Use lambda to get latest value
                () => _screens);           // Use lambda to get latest value
            Log.Debug("CaptureHandler initialized");
        }
    }

    /// <summary>
    /// Phase 3: Setup event subscriptions and final configuration
    /// </summary>
    private void SetupEventSubscriptions()
    {
        Log.Debug("Phase 3: Setting up event subscriptions");
        
        // Setup handler events
        SetupSelectionHandlerEvents();
        SetupCaptureHandlerEvents();
        SetupAnnotationHandlerEvents();
        
        Log.Debug("Event subscriptions completed");
    }

    /// <summary>
    /// Verify that all critical components are properly initialized
    /// </summary>
    private bool VerifyInitialization()
    {
        var isReady = _annotator != null && 
                     _selectionHandler != null && 
                     _elementDetectionHandler != null &&
                     _captureHandler != null;
        
        Log.Debug("Initialization verification: Ready={Ready}, Annotator={A}, SelectionHandler={SH}, ElementDetectionHandler={EDH}, CaptureHandler={CH}",
            isReady, _annotator != null, _selectionHandler != null, _elementDetectionHandler != null, _captureHandler != null);
        
        return isReady;
    }

    /// <summary>
    /// Try to set focus when both components and background are ready
    /// This ensures focus is set at the right time regardless of initialization order
    /// </summary>
    private void TrySetFocusWhenReady()
    {
        if (_componentsInitialized && _backgroundReady && !_focusSet)
        {
            Log.Debug("Both components and background ready - setting focus");
            SetFocusAfterInitialization();
            _focusSet = true;
        }
        else
        {
            Log.Debug("Not ready for focus yet - Components: {C}, Background: {B}, FocusSet: {F}", 
                _componentsInitialized, _backgroundReady, _focusSet);
        }
    }

    /// <summary>
    /// Ensure focus is set after background is ready (called from async background loading)
    /// </summary>
    private void EnsureFocusAfterBackgroundReady()
    {
        Log.Debug("Background ready - checking if focus should be set");
        _backgroundReady = true;
        TrySetFocusWhenReady();
    }

    /// <summary>
    /// Set focus after all initialization is complete
    /// This ensures keyboard events work properly on first screenshot
    /// </summary>
    private void SetFocusAfterInitialization()
    {
        Log.Debug("Setting focus after initialization");
        
        // Verify all components are ready before setting focus
        if (!VerifyInitialization())
        {
            Log.Warning("Not all components initialized, focus may not work properly");
        }
        
        // Ensure annotator has focus for keyboard shortcuts
        if (_annotator != null)
        {
            _annotator.Focus();
            this.Focus();
            Log.Debug("Focus set to annotator and window");
        }

        // Disable IME to prevent input method interference with keyboard shortcuts
        _imeHandler?.DisableIme();
        
        Log.Debug("Focus initialization completed - keyboard events ready");
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
                
                // Background is now ready - set focus if not already done
                EnsureFocusAfterBackgroundReady();
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
        _toolbarHandler?.SetScreens(screens);
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

    private void SetupSelectionHandlerEvents()
    {
        if (_selectionHandler == null)
            return;

        // Subscribe to selection handler events
        _selectionHandler.SelectionChanged += r =>
        {
            UpdateMaskForSelection(r);
            _annotationHandler?.UpdateSelection(r);
            _toolbarHandler?.UpdatePosition(r);
        };

        _selectionHandler.SelectionFinished += r =>
        {
            _annotationHandler?.FocusAnnotator();
            RegionSelected?.Invoke(this, new RegionSelectedEventArgs(r, false, null, true));
        };

        _selectionHandler.ConfirmRequested += async r =>
        {
            var finalImage = await CaptureWithFallbackAsync(r);
            RegionSelected?.Invoke(this, new RegionSelectedEventArgs(r, false, null, false, finalImage));
            
            // Close overlay after capture
            var _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await System.Threading.Tasks.Task.Delay(OverlayConstants.StandardUiDelay);
                CloseOverlayWithController("selection confirmed");
            });
        };

        _selectionHandler.Cancelled += (s, e) => Cancelled?.Invoke(this, e);
    }

    private void SetupAnnotationHandlerEvents()
    {
        if (_annotationHandler == null)
            return;

        _annotationHandler.ExportRequested += async () => await _captureHandler?.HandleExportRequestAsync()!;
        _annotationHandler.ColorPickerRequested += () => _annotationHandler.ShowColorPicker();
        _annotationHandler.ConfirmRequested += async r =>
        {
            var finalImage = await CaptureWithFallbackAsync(r);
            RegionSelected?.Invoke(this, new RegionSelectedEventArgs(r, false, null, false, finalImage));

            var _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await System.Threading.Tasks.Task.Delay(OverlayConstants.StandardUiDelay);
                CloseOverlayWithController("double-click save");
            });
        };
    }

    private void SetupCaptureHandlerEvents()
    {
        if (_captureHandler == null)
            return;

        _captureHandler.CloseRequested += context => CloseOverlayWithController(context);
    }

    private async Task<Bitmap?> CaptureWithFallbackAsync(Rect region)
    {
        try
        {
            Log.Debug("Capturing window region with annotations: {Region}", region);
            var image = await _captureHandler?.CaptureWindowRegionWithAnnotationsAsync(region)!;
            Log.Debug("Window region captured successfully");
            return image;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to capture window region, falling back to frozen background");
            return await _captureHandler?.GetBaseScreenshotForRegionAsync(region)!;
        }
    }

    private void SetupWindowCleanup()
    {
        this.Closing += (sender, e) =>
        {
            Log.Information("OverlayWindow: Window closing, cleaning up resources");
            
            // NOTE: Do not restore IME here - window handle is being destroyed
            // System will automatically restore IME state when window is closed
            
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

    private void UpdateMaskForSelection(Rect selection)
    {
        if (_maskPath == null)
            return;

        // Use mask size set by controller (platform-specific)
        // Fallback to ClientSize if not set (for backwards compatibility)
        var maskWidth = _maskSize.Width > 0 ? _maskSize.Width : this.ClientSize.Width;
        var maskHeight = _maskSize.Height > 0 ? _maskSize.Height : this.ClientSize.Height;

        // Create geometry with even-odd fill rule to create a "hole" for the selection
        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(new RectangleGeometry(new Rect(0, 0, maskWidth, maskHeight)));
        if (selection.Width > 0 && selection.Height > 0)
            group.Children.Add(new RectangleGeometry(selection));
        
        _maskPath.Data = group;
    }

    /// <summary>
    /// Preview (tunneling) keyboard event - captures keys before children
    /// This ensures OverlayWindow handles Ctrl key even when NewAnnotationOverlay has focus
    /// </summary>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        Log.Debug("OnPreviewKeyDown: Key={Key}, Modifiers={Modifiers}", e.Key, e.KeyModifiers);
        
        // Handle Ctrl, Tab, Space keys in preview - let other keys go to children first
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        {
            Log.Debug("Ctrl key detected - calling handlers");
            _selectionHandler?.HandleCtrlKeyDown();
            _elementDetectionHandler?.EnableElementPicker();
            // Don't set e.Handled = true to allow children to also receive the event
            return;
        }

        if (e.Key == Key.Tab)
        {
            _selectionHandler?.HandleTabKey();
            e.Handled = true; // Prevent tab navigation
            return;
        }

        if (e.Key == Key.Space && _selectionHandler?.SelectionMode == OverlaySelectionMode.ElementPicker)
        {
            _elementDetectionHandler?.ToggleDetectionMode();
            e.Handled = true;
            return;
        }

        // ESC key - delegate to selection handler
        if (e.Key == Key.Escape)
        {
            _selectionHandler?.HandleEscapeKey();
            e.Handled = true;
            return;
        }

        // Ctrl+S - Export
        if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (_selectionHandler?.SelectionMode == OverlaySelectionMode.Editing)
            {
                Log.Information("Ctrl+S pressed - triggering export");
                _annotationHandler?.RequestExport();
            }
            e.Handled = true;
            return;
        }

        // Enter key - confirm selection with capture and close
        if (e.Key == Key.Enter)
        {
            _selectionHandler?.HandleEnterKey();
            e.Handled = true;
            return;
        }
    }

    /// <summary>
    /// Preview (tunneling) keyboard event for key release
    /// </summary>
    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        {
            _selectionHandler?.HandleCtrlKeyUp();
            _elementDetectionHandler?.DisableElementPicker();
            // Don't set e.Handled = true to allow children to also receive the event
        }
    }


    private void SetupElementHighlight()
    {
        if (_elementDetector == null)
        {
            Log.Warning("ElementDetector is null, skipping element highlight setup");
            return;
        }

        Log.Debug("Setting up element highlight with detector: {Type}, IsSupported: {IsSupported}, HasPermissions: {HasPermissions}",
            _elementDetector.GetType().Name, _elementDetector.IsSupported, _elementDetector.HasPermissions);

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

        // Create element detection handler
        _elementDetectionHandler = new ElementDetectionHandler(this, _elementDetector, _elementHighlight);
        _elementDetectionHandler.ElementSelected += OnElementSelected;
        
        Log.Debug("Element detection handler created successfully");
    }



    private void OnElementSelected(DetectedElement element)
    {
        Log.Information("Element selected: {Name} - {Bounds}", element.Name, element.Bounds);

        if (_selectionHandler == null || _elementDetectionHandler == null)
            return;

        var selectionRect = _elementDetectionHandler.ConvertElementBoundsToOverlay(element);

        _selectionHandler.ShowSelector();
        _selectionHandler.SetSelection(selectionRect);
        _elementDetectionHandler.DisableElementPicker();

        RegionSelected?.Invoke(this, new RegionSelectedEventArgs(selectionRect, false, element, true));

        Log.Debug("Element selected, switched to editing mode");
    }

    private void OnOverlayPointerMoved(object? sender, PointerEventArgs e)
    {
        var isElementPickerMode = _selectionHandler?.SelectionMode == OverlaySelectionMode.ElementPicker;
        _elementDetectionHandler?.HandlePointerMoved(e, isElementPickerMode);
    }

    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var isElementPickerMode = _selectionHandler?.SelectionMode == OverlaySelectionMode.ElementPicker;
        var handled = _elementDetectionHandler?.HandlePointerPressed(e, isElementPickerMode) ?? false;
        if (handled)
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Get full screen screenshot for color sampling
    /// </summary>
    public async Task<Bitmap?> GetFullScreenScreenshotAsync()
    {
        return await _captureHandler?.GetFullScreenScreenshotAsync()!;
    }





    #region Export Functionality

    /// <summary>
    /// Handle export request from annotation overlay (delegated to CaptureHandler)
    /// </summary>
    private async void HandleExportRequest()
    {
        await _captureHandler?.HandleExportRequestAsync()!;
    }

    /// <summary>
    /// Handle color picker request from annotation overlay (delegated to AnnotationHandler)
    /// </summary>
    private void HandleColorPickerRequest()
    {
        _annotationHandler?.ShowColorPicker();
    }

    #endregion

    #region IME Control

    /// <summary>
    /// Public method for annotation overlay to enable IME during text editing
    /// </summary>
    public void EnableImeForTextEditing()
    {
        _imeHandler?.EnableIme();
    }

    /// <summary>
    /// Public method for annotation overlay to disable IME after text editing
    /// </summary>
    public void DisableImeAfterTextEditing()
    {
        _imeHandler?.DisableIme();
    }

    #endregion
}
