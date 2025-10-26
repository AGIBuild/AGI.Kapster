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
    private readonly IElementDetector? _elementDetector;
    private readonly IScreenCaptureStrategy? _screenCaptureStrategy;
    private readonly IScreenCoordinateMapper? _coordinateMapper;
    private readonly IToolbarPositionCalculator _toolbarPositionCalculator;
    private readonly ISettingsService _settingsService;
    private readonly IImeController _imeController;
    private ElementHighlightOverlay? _elementHighlight;
    private OverlaySelectionMode _selectionMode = OverlaySelectionMode.FreeSelection;
    private NewAnnotationOverlay? _annotator; // Keep reference to correct annotator instance

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

    // Throttling for element detection to prevent excessive updates
    private PixelPoint _lastDetectionPos;
    private DateTime _lastDetectionTime = DateTime.MinValue;
    private const double MinMovementThreshold = 8.0; // pixels
    private static readonly TimeSpan MinDetectionInterval = TimeSpan.FromMilliseconds(30); // ~33 FPS max

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
        get => _selectionMode == OverlaySelectionMode.ElementPicker;
        set => SetElementPickerMode(value);
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
        
        // Fast initialization: only XAML parsing
        InitializeComponent();

        // Cache control references immediately after InitializeComponent
        CacheControlReferences();

        // Minimal setup for immediate display
        this.Cursor = new Cursor(StandardCursorType.Cross);
        _selectionMode = OverlaySelectionMode.FreeSelection;

        // Set up mouse event handlers for element selection
        this.PointerPressed += OnOverlayPointerPressed;
        this.PointerMoved += OnOverlayPointerMoved;

		// Opened event: initialize UI components
		// Note: Background is set automatically by SetPrecapturedAvaloniaBitmap when ready
		this.Opened += async (_, __) =>
		{
		    UpdateMaskForSelection(default);
		    
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
    /// Initialize heavy UI components (Annotator, Toolbar, ElementHighlight) after background is visible
    /// </summary>
    private void InitializeHeavyComponents()
    {
        // Create annotator with injected settings service
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

        SetupElementHighlight();

        if (_elementDetector != null && _elementHighlight != null)
        {
            _elementHighlight.IsActive = false; // Initially disabled
        }

        // Show selection overlay by default for free selection
        if (_selector != null)
        {
            _selector.IsVisible = true;
            _selector.IsHitTestVisible = true;
        }

        // Setup selection overlay
        SetupSelectionOverlay();
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
            // Backdrop removed to avoid blur/ghosting issues

            _selector.SelectionFinished += r =>
            {
                // Keep selection for annotation; don't capture yet
                _selectionMode = OverlaySelectionMode.Editing;
                Log.Information("Selection finished: {X},{Y} {W}x{H} - editable selection created", r.X, r.Y, r.Width, r.Height);

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
                UpdateMaskForSelection(r);
                if (_annotator != null)
                {
                    _annotator.SelectionRect = r;
                    _annotator.IsHitTestVisible = r.Width > 2 && r.Height > 2;
                }
                UpdateToolbarPosition(r);
            };

            _selector.ConfirmRequested += async r =>
            {
                // Directly capture the window region (includes background + annotations)
                // This avoids coordinate transformation issues
                Bitmap? finalImage = null;
                
                try
                {
                    Log.Debug("Capturing window region with annotations: {Region}", r);
                    finalImage = await CaptureWindowRegionWithAnnotationsAsync(r);
                    Log.Debug("Window region captured successfully");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to capture window region, falling back to frozen background");
                    finalImage = await GetBaseScreenshotForRegionAsync(r);
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
                // Directly capture the window region (includes background + annotations)
                // This avoids coordinate transformation issues
                Bitmap? finalImage = null;
                
                try
                {
                    Log.Debug("Capturing window region with annotations: {Region}", r);
                    finalImage = await CaptureWindowRegionWithAnnotationsAsync(r);
                    Log.Debug("Window region captured successfully");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to capture window region, falling back to frozen background");
                    finalImage = await GetBaseScreenshotForRegionAsync(r);
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
        }

        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        {
            // CTRL key pressed - switch to auto highlight mode (only if not in editing mode)
            if (_selectionMode == OverlaySelectionMode.FreeSelection)
            {
                _selectionMode = OverlaySelectionMode.ElementPicker;
                if (_elementHighlight != null)
                {
                    _elementHighlight.IsActive = true;
                }

                // Hide selection overlay
                if (_selector != null)
                {
                    _selector.IsVisible = false;
                    _selector.IsHitTestVisible = false;
                }

                // Set cursor for element selection
                this.Cursor = new Cursor(StandardCursorType.Hand);

                Log.Debug("Switched to element picker mode");
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            // Tab key for manual toggle (fallback)
            ToggleElementPickerMode();
            e.Handled = true;
        }
        else if (e.Key == Key.Space && _selectionMode == OverlaySelectionMode.ElementPicker)
        {
            // Toggle between window and element detection mode
            _elementDetector?.ToggleDetectionMode();
            e.Handled = true;
        }
        else if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            // Ctrl+S: Export current selection to file
            if (_selectionMode == OverlaySelectionMode.Editing && _annotator != null)
            {
                Log.Information("Ctrl+S pressed - triggering export via annotator");
                _annotator.RequestExport();
            }
            else
            {
                Log.Debug("Ctrl+S pressed but no editable selection or annotator found");
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
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

        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        {
            // CTRL key released - switch back to free selection mode (only if in element picker mode)
            if (_selectionMode == OverlaySelectionMode.ElementPicker)
            {
                _selectionMode = OverlaySelectionMode.FreeSelection;
                if (_elementHighlight != null)
                {
                    _elementHighlight.IsActive = false;
                    // Element highlight state is managed internally
                }

                // Show selection overlay
                if (_selector != null)
                {
                    _selector.IsVisible = true;
                    _selector.IsHitTestVisible = true;
                }

                // Set cursor for free selection
                this.Cursor = new Cursor(StandardCursorType.Cross);

                Log.Debug("Switched to free selection mode");
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

        // Element selection is now handled directly by OverlayWindow mouse events
    }

    private void ToggleElementPickerMode()
    {
        bool isElementPicker = _selectionMode == OverlaySelectionMode.ElementPicker;
        _selectionMode = isElementPicker ? OverlaySelectionMode.FreeSelection : OverlaySelectionMode.ElementPicker;

        if (_elementHighlight != null)
        {
            _elementHighlight.IsActive = _selectionMode == OverlaySelectionMode.ElementPicker;
        }

        // Hide/show selection overlay based on mode
        if (_selector != null)
        {
            _selector.IsHitTestVisible = _selectionMode != OverlaySelectionMode.ElementPicker;
            _selector.IsVisible = _selectionMode != OverlaySelectionMode.ElementPicker;
        }

        // Set appropriate cursor
        this.Cursor = _selectionMode == OverlaySelectionMode.ElementPicker 
            ? new Cursor(StandardCursorType.Hand) 
            : new Cursor(StandardCursorType.Cross);

        Log.Debug("Selection mode: {Mode}", _selectionMode);
    }

    private void SetElementPickerMode(bool enabled)
    {
        var newMode = enabled ? OverlaySelectionMode.ElementPicker : OverlaySelectionMode.FreeSelection;
        if (_selectionMode != newMode)
        {
            _selectionMode = newMode;

            // Update element highlight state
            if (_elementHighlight != null)
            {
                _elementHighlight.IsActive = enabled;
            }

            // Hide/show selection overlay based on mode
            if (_selector != null)
            {
                _selector.IsHitTestVisible = !enabled;
                _selector.IsVisible = !enabled;
            }

            // Set appropriate cursor
            this.Cursor = enabled ? new Cursor(StandardCursorType.Hand) : new Cursor(StandardCursorType.Cross);
        }
    }

    private void OnElementSelected(DetectedElement element)
    {
        Log.Information("Element selected: {Name} - {Bounds}", element.Name, element.Bounds);

        // Convert element bounds to overlay coordinates and set selection
        if (_selector != null)
        {
            // Convert screen coordinates to window coordinates with proper bounds checking
            var screenBounds = element.Bounds;
            var overlayTopLeft = this.PointToClient(new PixelPoint((int)screenBounds.X, (int)screenBounds.Y));
            var overlayBottomRight = this.PointToClient(new PixelPoint(
                (int)(screenBounds.X + screenBounds.Width),
                (int)(screenBounds.Y + screenBounds.Height)));

            var selectionRect = new Rect(
                Math.Min(overlayTopLeft.X, overlayBottomRight.X),
                Math.Min(overlayTopLeft.Y, overlayBottomRight.Y),
                Math.Abs(overlayBottomRight.X - overlayTopLeft.X),
                Math.Abs(overlayBottomRight.Y - overlayTopLeft.Y));

            // Show and enable selection overlay
            _selector.IsVisible = true;
            _selector.IsHitTestVisible = true;

            // Set the selection and switch back to normal mode
            _selector.SetSelection(selectionRect);

            // Switch to editing mode
            _selectionMode = OverlaySelectionMode.Editing;

            if (_elementHighlight != null)
            {
                _elementHighlight.IsActive = false;
                // Element highlight state is managed internally
            }

            // Reset cursor to normal selection mode (let overlay handle cursor)
            // this.Cursor = new Cursor(StandardCursorType.Cross);

            // Raise public event with isEditableSelection = true
            RegionSelected?.Invoke(this, new RegionSelectedEventArgs(selectionRect, false, element, true));

            Log.Debug("Element selected, switched to editing mode");
        }
    }

    private void OnOverlayPointerMoved(object? sender, PointerEventArgs e)
    {
        // Only handle mouse events in element picker mode
        if (_selectionMode != OverlaySelectionMode.ElementPicker || _elementDetector == null || _elementHighlight == null)
            return;

        var position = e.GetPosition(this);
        var screenPos = this.PointToScreen(position);

        // Throttle element detection to prevent excessive updates
        // Only detect if mouse moved significantly or enough time has passed
        if (ShouldUpdateElementDetection(screenPos))
        {
            try
            {
                var overlayHandle = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                var element = _elementDetector.DetectElementAt((int)screenPos.X, (int)screenPos.Y, overlayHandle);
                _elementHighlight.SetCurrentElement(element);

                // Update last detection position and time
                _lastDetectionPos = screenPos;
                _lastDetectionTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error during mouse move element detection");
            }
        }
    }

    private bool ShouldUpdateElementDetection(PixelPoint currentPos)
    {
        var now = DateTime.UtcNow;

        // Check time throttling - don't update too frequently
        if (now - _lastDetectionTime < MinDetectionInterval)
            return false;

        // Check movement threshold - only update if mouse moved significantly
        var distance = Math.Sqrt(
            Math.Pow(currentPos.X - _lastDetectionPos.X, 2) +
            Math.Pow(currentPos.Y - _lastDetectionPos.Y, 2));

        return distance >= MinMovementThreshold;
    }

    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_selectionMode == OverlaySelectionMode.ElementPicker && _elementDetector != null)
        {
            // In element picker mode - only handle single clicks for element selection
            var position = e.GetPosition(this);
            var screenPos = this.PointToScreen(position);

            try
            {
                // Get element at click position with error handling, ignoring this overlay window
                var overlayHandle = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                var element = _elementDetector.DetectElementAt((int)screenPos.X, (int)screenPos.Y, overlayHandle);
                if (element != null)
                {
                    OnElementSelected(element);
                    e.Handled = true;
                }
                else
                {
                    Log.Warning("No element detected at click position {X}, {Y}", screenPos.X, screenPos.Y);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during element selection at {X}, {Y}", screenPos.X, screenPos.Y);
            }
        }
        // Note: When not in element picker mode, let SelectionOverlay handle the event for custom drag selection
    }

    // Clipboard functionality has been moved to platform-specific strategies
    // See IClipboardStrategy and its implementations

    /// <summary>
    /// Cross-platform screenshot capture method using strategy pattern
    /// </summary>
    private async Task<Bitmap?> CaptureRegionAsync(Avalonia.Rect rect)
    {
        if (_screenCaptureStrategy == null)
        {
            Log.Error("No screen capture strategy available");
            return null;
        }

        try
        {
            var skBitmap = await _screenCaptureStrategy.CaptureWindowRegionAsync(rect, this);
            return BitmapConverter.ConvertToAvaloniaBitmap(skBitmap);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to capture region");
            return null;
        }
    }

    /// <summary>
    /// Capture what the user sees: render the actual window region including background and annotation overlay.
    /// This avoids coordinate transformation issues by capturing the already-rendered content.
    /// </summary>
    private async Task<Bitmap?> CaptureWindowRegionWithAnnotationsAsync(Avalonia.Rect region)
    {
        try
        {
            // Calculate DPI scaling
            var scaleX = 1.0;
            var scaleY = 1.0;
            if (_frozenBackground != null)
            {
                scaleX = _frozenBackground.PixelSize.Width / Math.Max(1.0, this.Bounds.Width);
                scaleY = _frozenBackground.PixelSize.Height / Math.Max(1.0, this.Bounds.Height);
            }

            // End text editing before capture to prevent TextBox background artifacts.
            // Null-safety is handled by the null-conditional operator (?.).
            _annotator?.EndTextEditing();

            // Temporarily hide UI elements
            bool selectorWasVisible = _selector?.IsVisible ?? false;
            if (_selector != null)
            {
                _selector.IsVisible = false;
            }

            try
            {
                var baseScreenshot = ExtractRegionFromFrozenBackground(region);
                if (baseScreenshot == null)
                {
                    Log.Warning("Failed to extract region from frozen background");
                    return null;
                }

                var annotations = GetAnnotationsFromAnnotator();
                if (annotations == null || !annotations.Any())
                {
                    return baseScreenshot;
                }

                // Determine target screen for correct DPI handling
                var targetScreen = GetScreenForSelection(region);

                var exportService = new ExportService();
                return await exportService.CreateCompositeImageWithAnnotationsAsync(
                    baseScreenshot, annotations, region, targetScreen);
            }
            finally
            {
                // Restore UI elements
                if (_selector != null && selectorWasVisible)
                {
                    _selector.IsVisible = true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to capture window region with annotations");
            return null;
        }
    }

    /// <summary>
    /// Get annotations from annotator
    /// </summary>
    private IEnumerable<IAnnotationItem>? GetAnnotationsFromAnnotator()
    {
        return _annotator?.GetAnnotations();
    }

    /// <summary>
    /// Determine which screen the selection region is on (using center point)
    /// </summary>
    private Screen? GetScreenForSelection(Rect selectionRect)
    {
        if (_coordinateMapper == null)
        {
            Log.Debug("No coordinate mapper available, cannot determine target screen");
            return null;
        }

        try
        {
            // Calculate center point of selection (in logical DIPs)
            var centerX = selectionRect.X + selectionRect.Width / 2;
            var centerY = selectionRect.Y + selectionRect.Height / 2;
            var centerPoint = new PixelPoint((int)centerX, (int)centerY);

            // Find screen containing this point
            if (_screens == null || _screens.Count == 0)
            {
                Log.Warning("Cannot determine target screen: screens not available");
                return null;
            }

            var targetScreen = _coordinateMapper.GetScreenFromPoint(centerPoint, _screens);
            if (targetScreen != null)
            {
                Log.Debug("Selection at ({X}, {Y}) is on screen with scaling {Scaling}", 
                    centerX, centerY, targetScreen.Scaling);
            }
            else
            {
                Log.Debug("Could not determine screen for selection at ({X}, {Y})", centerX, centerY);
            }

            return targetScreen;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to determine target screen for selection");
            return null;
        }
    }

    /// <summary>
    /// Returns base screenshot for a region: uses frozen background if available, otherwise live capture.
    /// </summary>
    private async Task<Bitmap?> GetBaseScreenshotForRegionAsync(Avalonia.Rect region)
    {
        if (_frozenBackground != null)
        {
            Log.Debug("Using frozen background for region {Region}", region);
            return ExtractRegionFromFrozenBackground(region);
        }
        Log.Debug("Frozen background not available, using live capture for region {Region}", region);
        return await CaptureRegionAsync(region);
    }

    /// <summary>
    /// Extract a region from the frozen background with DPI-aware source rect scaling.
    /// </summary>
    private Bitmap? ExtractRegionFromFrozenBackground(Avalonia.Rect region)
    {
        if (_frozenBackground == null)
            return null;

        var totalDipWidth = Math.Max(1.0, this.Bounds.Width);
        var totalDipHeight = Math.Max(1.0, this.Bounds.Height);
        var scaleX = _frozenBackground.PixelSize.Width / totalDipWidth;
        var scaleY = _frozenBackground.PixelSize.Height / totalDipHeight;

        // Calculate source rectangle in physical pixels
        var sourceRect = new Avalonia.Rect(
            region.X * scaleX,
            region.Y * scaleY,
            Math.Max(1.0, region.Width * scaleX),
            Math.Max(1.0, region.Height * scaleY));

        // Target should be in physical pixels, not DIPs
        var targetWidth = Math.Max(1, (int)Math.Round(region.Width * scaleX));
        var targetHeight = Math.Max(1, (int)Math.Round(region.Height * scaleY));

        // Create bitmap at physical pixel resolution with standard 96 DPI
        var target = new RenderTargetBitmap(new PixelSize(targetWidth, targetHeight), new Vector(96, 96));
        using (var ctx = target.CreateDrawingContext())
        {
            ctx.DrawImage(_frozenBackground, sourceRect, new Avalonia.Rect(0, 0, targetWidth, targetHeight));
        }
        return target;
    }

    /// <summary>
    /// Get full screen screenshot for color sampling
    /// </summary>
    public async Task<Bitmap?> GetFullScreenScreenshotAsync()
    {
        if (_screenCaptureStrategy == null)
        {
            Log.Debug("No screen capture strategy available for full screen screenshot");
            return null;
        }

        try
        {
            // Get the full screen bounds
            var screenBounds = new Avalonia.Rect(0, 0, this.Bounds.Width, this.Bounds.Height);
            var skBitmap = await _screenCaptureStrategy.CaptureWindowRegionAsync(screenBounds, this);
            return BitmapConverter.ConvertToAvaloniaBitmap(skBitmap);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to capture full screen screenshot for color sampling");
            return null;
        }
    }





    #region Export Functionality

    /// <summary>
    /// Handle export request from annotation overlay
    /// </summary>
    private async void HandleExportRequest()
    {
        try
        {
            if (!ValidateExportPreconditions())
                return;

            var settings = await ShowExportSettingsDialogAsync();
            if (settings == null)
                return;

            var file = await ShowSaveFileDialogAsync(settings);
            if (file == null)
                return;

            await PerformExportAsync(file.Path.LocalPath, settings);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to handle export request");
        }
    }

    private bool ValidateExportPreconditions()
    {
        if (_annotator == null)
            return false;

        var selectionRect = _annotator.SelectionRect;
        if (selectionRect.Width <= 0 || selectionRect.Height <= 0)
        {
            Log.Warning("Cannot export: no valid selection area");
            return false;
        }

        return true;
    }

    private async Task<ExportSettings?> ShowExportSettingsDialogAsync()
    {
        var exportService = new ExportService();
        var defaultSettings = exportService.GetDefaultSettings();
        var imageSize = new Avalonia.Size(_annotator!.SelectionRect.Width, _annotator.SelectionRect.Height);
        var settingsDialog = new ExportSettingsDialog(defaultSettings, imageSize);

        var dialogResult = await settingsDialog.ShowDialog<bool?>(this);
        if (dialogResult != true)
        {
            Log.Information("Export cancelled by user");
            return null;
        }

        return settingsDialog.Settings;
    }

    private async Task<IStorageFile?> ShowSaveFileDialogAsync(ExportSettings settings)
    {
        var storageProvider = GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null)
        {
            Log.Error("Cannot access storage provider for file dialog");
            return null;
        }

        var exportService = new ExportService();
        var fileTypes = CreateFileTypesFromFormats(exportService.GetSupportedFormats());
        var suggestedFileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}{settings.GetFileExtension()}";

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Annotated Screenshot",
            FileTypeChoices = fileTypes,
            DefaultExtension = settings.GetFileExtension().TrimStart('.'),
            SuggestedFileName = suggestedFileName
        });

        if (file == null)
        {
            Log.Information("File save cancelled by user");
        }

        return file;
    }

    private async Task PerformExportAsync(string filePath, ExportSettings settings)
    {
        var progressDialog = new ExportProgressDialog();
        progressDialog.SetFileInfo(System.IO.Path.GetFileName(filePath), settings.Format.ToString());

        _ = progressDialog.ShowDialog(this);

        var wasVisible = _selector?.IsVisible ?? false;

        try
        {
            await HideSelectorAndWaitAsync(progressDialog);

            var finalImage = await CaptureScreenshotForExportAsync(progressDialog);
            if (finalImage == null)
                throw new InvalidOperationException("Failed to capture screenshot");

            await ExportImageToFileAsync(finalImage, filePath, settings, progressDialog);

            progressDialog.Close();
            Log.Information("Successfully exported to {FilePath}: {Format}, Q={Quality}",
                filePath, settings.Format, settings.Quality);

            CloseOverlayWithController("export");
        }
        catch (Exception ex)
        {
            RestoreSelectorVisibility(wasVisible);
            var errorMessage = ex.InnerException?.Message ?? ex.Message;
            progressDialog.ShowError($"Export failed: {errorMessage}");
            Log.Error(ex, "Export failed");
            throw;
        }
        finally
        {
            RestoreSelectorVisibility(wasVisible);
        }
    }

    private async Task HideSelectorAndWaitAsync(ExportProgressDialog progressDialog)
    {
        progressDialog.UpdateProgress(5, "Preparing capture...");

        if (_selector != null)
        {
            _selector.IsVisible = false;
            await Task.Delay(OverlayConstants.StandardUiDelay); // UI update delay
        }
    }

    private async Task<Bitmap?> CaptureScreenshotForExportAsync(ExportProgressDialog progressDialog)
    {
        progressDialog.UpdateProgress(10, "Capturing screenshot...");
        return await CaptureWindowRegionWithAnnotationsAsync(_annotator!.SelectionRect);
    }

    private async Task ExportImageToFileAsync(
        Bitmap image,
        string filePath,
        ExportSettings settings,
        ExportProgressDialog progressDialog)
    {
        var exportService = new ExportService();
        await exportService.ExportToFileDirectAsync(image, filePath, settings,
            (percentage, status) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    progressDialog.UpdateProgress(percentage, status),
                    Avalonia.Threading.DispatcherPriority.Background);
            });
    }

    private void RestoreSelectorVisibility(bool wasVisible)
    {
        if (_selector != null && wasVisible)
        {
            _selector.IsVisible = true;
        }
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

    /// <summary>
    /// Create file types for file picker from supported export formats
    /// </summary>
    private FilePickerFileType[] CreateFileTypesFromFormats(ExportFormat[] formats)
    {
        var fileTypes = new List<FilePickerFileType>();

        foreach (var format in formats)
        {
            var extension = GetExtensionForFormat(format);
            var description = GetDescriptionForFormat(format);

            fileTypes.Add(new FilePickerFileType(description)
            {
                Patterns = new[] { $"*{extension}" }
            });
        }

        // Add "All Supported Images" option
        var allPatterns = formats.Select(f => $"*{GetExtensionForFormat(f)}").ToArray();
        fileTypes.Insert(0, new FilePickerFileType("All Supported Images")
        {
            Patterns = allPatterns
        });

        return fileTypes.ToArray();
    }

    private string GetExtensionForFormat(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.PNG => ".png",
            ExportFormat.JPEG => ".jpg",
            ExportFormat.BMP => ".bmp",
            ExportFormat.TIFF => ".tiff",
            ExportFormat.WebP => ".webp",
            ExportFormat.GIF => ".gif",
            _ => ".png"
        };
    }

    private string GetDescriptionForFormat(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.PNG => "PNG Image",
            ExportFormat.JPEG => "JPEG Image",
            ExportFormat.BMP => "Bitmap Image",
            ExportFormat.TIFF => "TIFF Image",
            ExportFormat.WebP => "WebP Image",
            ExportFormat.GIF => "GIF Image",
            _ => "Image File"
        };
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
