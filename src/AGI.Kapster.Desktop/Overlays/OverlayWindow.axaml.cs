using AGI.Kapster.Desktop.Dialogs;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Services.Annotation;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Services.Export;
using AGI.Kapster.Desktop.Services.Export.Imaging;
using AGI.Kapster.Desktop.Services.Overlay;
using AGI.Kapster.Desktop.Services.Settings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AGI.Kapster.Desktop.Overlays;

public partial class OverlayWindow : Window
{
    private readonly IElementDetector? _elementDetector;
    private readonly IScreenCaptureStrategy? _screenCaptureStrategy;
    private ElementHighlightOverlay? _elementHighlight;
    private bool _isElementPickerMode = false; // Default to free selection mode
    private bool _hasEditableSelection = false; // Track if there's an editable selection
    private NewAnnotationOverlay? _annotator; // Keep reference to correct annotator instance

    // Throttling for element detection to prevent excessive updates
    private PixelPoint _lastDetectionPos;
    private DateTime _lastDetectionTime = DateTime.MinValue;
    private const double MinMovementThreshold = 8.0; // pixels
    private static readonly TimeSpan MinDetectionInterval = TimeSpan.FromMilliseconds(30); // ~33 FPS max

	// Frozen background (snapshot at overlay activation)
	private Bitmap? _frozenBackground;

    // Public events for external consumers
    public event EventHandler<RegionSelectedEventArgs>? RegionSelected;
    public event EventHandler<OverlayCancelledEventArgs>? Cancelled;

    // Property to check element detection support
    public bool ElementDetectionEnabled
    {
        get => _isElementPickerMode;
        set => SetElementPickerMode(value);
    }

    public OverlayWindow(IElementDetector? elementDetector = null, IScreenCaptureStrategy? screenCaptureStrategy = null)
    {
        _elementDetector = elementDetector;
        _screenCaptureStrategy = screenCaptureStrategy;
        InitializeComponent();

        // Create settings service instance for this overlay session
        var settingsService = new SettingsService();

        // Replace XAML annotator with one that has settings service
        var existingAnnotator = this.FindControl<NewAnnotationOverlay>("Annotator");
        if (existingAnnotator != null)
        {
            // Create a new NewAnnotationOverlay with settings service and replace the XAML one
            var newAnnotator = new NewAnnotationOverlay(settingsService);
            newAnnotator.Name = "Annotator";

            // Find the parent grid and replace the old annotator
            if (this.Content is Grid grid)
            {
                var index = grid.Children.IndexOf(existingAnnotator);
                if (index >= 0)
                {
                    grid.Children.RemoveAt(index);
                    grid.Children.Insert(index, newAnnotator);

                    // Update the reference for later setup
                    existingAnnotator = newAnnotator;
                    _annotator = newAnnotator; // Store reference to avoid FindControl issues

                    // Set focus to enable keyboard shortcuts
                    newAnnotator.Focus();
                }
            }
        }

        SetupElementHighlight();

        // Start in free selection mode by default
        _isElementPickerMode = false;
        _hasEditableSelection = false;

        if (_elementDetector != null && _elementHighlight != null)
        {
            _elementHighlight.IsActive = false; // Initially disabled
        }

        // Show selection overlay by default for free selection
        var selectorOverlay = this.FindControl<SelectionOverlay>("Selector");
        if (selectorOverlay != null)
        {
            selectorOverlay.IsVisible = true;
            selectorOverlay.IsHitTestVisible = true;
        }

        // Set initial cursor for free selection mode
        this.Cursor = new Cursor(StandardCursorType.Cross);

        Log.Information("OverlayWindow created with default free selection mode");

        Log.Information("OverlayWindow created {W}x{H}", Width, Height);

        // Set up mouse event handlers for element selection
        this.PointerPressed += OnOverlayPointerPressed;
        this.PointerMoved += OnOverlayPointerMoved;

		// Set focus to annotator when window is loaded
		this.Loaded += OnOverlayWindowLoaded;
		// Initialize frozen background on open
		this.Opened += async (_, __) =>
		{
		    await InitializeFrozenBackgroundAsync();
		    // Initialize mask after window is fully opened
		    UpdateMaskForSelection(default);
		    Log.Information("OverlayWindow opened: Position={Pos}, Size={W}x{H}, Bounds={Bounds}", 
		        Position, Width, Height, Bounds);
		};

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
    }

    private async Task InitializeFrozenBackgroundAsync()
    {
        if (_screenCaptureStrategy == null)
            return;

        try
        {
            // Temporarily make window transparent to avoid capturing the overlay itself
            var originalOpacity = this.Opacity;
            this.Opacity = 0;
            await Task.Delay(16);

            var rect = new Avalonia.Rect(0, 0, this.Bounds.Width, this.Bounds.Height);
            var skBitmap = await _screenCaptureStrategy.CaptureWindowRegionAsync(rect, this);

            this.Opacity = originalOpacity;

            var bitmap = BitmapConverter.ConvertToAvaloniaBitmap(skBitmap);
            if (bitmap != null)
            {
                _frozenBackground = bitmap;
                if (this.FindControl<Image>("BackgroundImage") is { } img)
                {
                    img.Source = _frozenBackground;
                }
                Log.Information("Frozen background initialized: {W}x{H} pixels for window bounds {Bounds}", 
                    bitmap.PixelSize.Width, bitmap.PixelSize.Height, this.Bounds);
            }
            else
            {
                Log.Warning("Failed to create frozen background bitmap");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize frozen background - falling back to live capture");
            this.Opacity = 1;
        }
    }

    private void SetupSelectionOverlay()
    {
        if (this.FindControl<SelectionOverlay>("Selector") is { } selector)
        {
            // Backdrop removed to avoid blur/ghosting issues

            selector.SelectionFinished += r =>
            {
                // Keep selection for annotation; don't capture yet
                _hasEditableSelection = true; // Mark that we have an editable selection
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
            selector.SelectionChanged += r =>
            {
                UpdateMaskForSelection(r);
                if (_annotator != null)
                {
                    _annotator.SelectionRect = r;
                    _annotator.IsHitTestVisible = r.Width > 2 && r.Height > 2;
                }
                UpdateToolbarPosition(r);
            };

            selector.ConfirmRequested += async r =>
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

        if (this.FindControl<NewAnnotationToolbar>("Toolbar") is { } toolbar && _annotator != null)
        {
            // Set default tool to Arrow first
            Log.Information("Setting default tool to Arrow");
            _annotator.CurrentTool = AnnotationToolType.Arrow;
            Log.Information("Default tool set to: {CurrentTool}", _annotator.CurrentTool);

            // Then set up toolbar (this will call UpdateUIFromTarget and sync the UI)
            toolbar.SetTarget(_annotator);

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
                    await System.Threading.Tasks.Task.Delay(50);

                    // Close all overlay windows, not just this one
                    var overlayController = App.Services?.GetService(typeof(IOverlayController)) as IOverlayController;
                    if (overlayController != null)
                    {
                        overlayController.CloseAll();
                        Log.Information("All overlay windows closed after double-click save");
                    }
                    else
                    {
                        // Fallback: close just this window
                        Close();
                        Log.Warning("Could not get overlay controller, closing only current window");
                    }
                });
            };
        }


        // Hide toolbar initially
        if (this.FindControl<NewAnnotationToolbar>("Toolbar") is { } tb)
        {
            Canvas.SetLeft(tb, -10000);
            Canvas.SetTop(tb, -10000);
        }

        // Clear global selection state when this window closes
        this.Closing += (sender, e) =>
        {
            GlobalSelectionState.ClearSelection(this);
            Log.Information("OverlayWindow: Cleared global selection state on window close");
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

    private void UpdateMaskForSelection(Rect selection)
    {
        if (this.FindControl<Avalonia.Controls.Shapes.Path>("MaskPath") is not { } mask)
            return;

        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(new RectangleGeometry(new Rect(0, 0, Bounds.Width, Bounds.Height)));
        if (selection.Width > 0 && selection.Height > 0)
            group.Children.Add(new RectangleGeometry(selection));
        mask.Data = group;
    }

    private void UpdateToolbarPosition(Rect selection)
    {
        if (this.FindControl<NewAnnotationToolbar>("Toolbar") is not { } tb || this.FindControl<Canvas>("UiCanvas") is not { } canvas)
            return;

        if (selection.Width <= 0 || selection.Height <= 0)
        {
            Canvas.SetLeft(tb, -10000);
            Canvas.SetTop(tb, -10000);
            return;
        }

        // Desired position: slightly outside bottom-right
        double offset = 8;
        double desiredX = selection.Right + offset;
        double desiredY = selection.Bottom + offset;

        // Toolbar size estimation: measure
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = tb.DesiredSize;

        // Window bounds
        double maxX = Bounds.Width;
        double maxY = Bounds.Height;

        // Auto-flip horizontally if overflowing right; vertically if overflowing bottom
        if (desiredX + size.Width > maxX)
        {
            desiredX = selection.X - size.Width - offset; // place to the left outside
            if (desiredX < 0)
            {
                // clamp inside selection right-bottom
                desiredX = Math.Max(selection.Right - size.Width - offset, selection.X + offset);
            }
        }
        if (desiredY + size.Height > maxY)
        {
            desiredY = selection.Y - size.Height - offset; // place above outside
            if (desiredY < 0)
            {
                // clamp inside selection right-bottom
                desiredY = Math.Max(selection.Bottom - size.Height - offset, selection.Y + offset);
            }
        }

        Canvas.SetLeft(tb, desiredX);
        Canvas.SetTop(tb, desiredY);
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
            // CTRL key pressed - switch to auto highlight mode (only if no editable selection)
            if (!_hasEditableSelection && !_isElementPickerMode)
            {
                _isElementPickerMode = true;
                if (_elementHighlight != null)
                {
                    _elementHighlight.IsActive = true;
                }

                // Hide selection overlay
                var selector = this.FindControl<SelectionOverlay>("Selector");
                if (selector != null)
                {
                    selector.IsVisible = false;
                    selector.IsHitTestVisible = false;
                }

                // Set cursor for element selection
                this.Cursor = new Cursor(StandardCursorType.Hand);

                Log.Information("CTRL pressed - switched to auto highlight mode");
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            // Tab key for manual toggle (fallback)
            ToggleElementPickerMode();
            e.Handled = true;
        }
        else if (e.Key == Key.Space && _isElementPickerMode)
        {
            // Toggle between window and element detection mode
            _elementDetector?.ToggleDetectionMode();
            e.Handled = true;
        }
        else if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            // Ctrl+S: Export current selection to file
            if (_hasEditableSelection && _annotator != null)
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
            if (this.FindControl<SelectionOverlay>("Selector") is { } selector && selector is not null)
            {
                var r = selector.SelectionRect;
                if (r.Width > 0)
                {
                    // Raise region selected event
                    RegionSelected?.Invoke(this, new RegionSelectedEventArgs(r, false));

                    var _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await System.Threading.Tasks.Task.Delay(50);

                        // Close all overlay windows, not just this one
                        var overlayController = App.Services?.GetService(typeof(IOverlayController)) as IOverlayController;
                        if (overlayController != null)
                        {
                            overlayController.CloseAll();
                            Log.Information("All overlay windows closed after Enter key save");
                        }
                        else
                        {
                            // Fallback: close just this window
                            Close();
                            Log.Warning("Could not get overlay controller, closing only current window");
                        }
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
            // CTRL key released - switch back to free selection mode (only if in element picker mode and no editable selection)
            if (_isElementPickerMode && !_hasEditableSelection)
            {
                _isElementPickerMode = false;
                if (_elementHighlight != null)
                {
                    _elementHighlight.IsActive = false;
                    GlobalElementHighlightState.Instance.ClearOwner(_elementHighlight);
                }

                // Show selection overlay
                var selector = this.FindControl<SelectionOverlay>("Selector");
                if (selector != null)
                {
                    selector.IsVisible = true;
                    selector.IsHitTestVisible = true;
                }

                // Set cursor for free selection
                this.Cursor = new Cursor(StandardCursorType.Cross);

                Log.Information("CTRL released - switched back to free selection mode");
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
        var selector = this.FindControl<SelectionOverlay>("Selector");
        if (selector != null)
        {
            var index = grid.Children.IndexOf(selector) + 1;
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
        _isElementPickerMode = !_isElementPickerMode;

        if (_elementHighlight != null)
        {
            _elementHighlight.IsActive = _isElementPickerMode;
        }

        // Hide/show selection overlay based on mode
        var selector = this.FindControl<SelectionOverlay>("Selector");
        if (selector != null)
        {
            selector.IsHitTestVisible = !_isElementPickerMode;
            selector.IsVisible = !_isElementPickerMode;
        }

        // Set appropriate cursor
        this.Cursor = _isElementPickerMode ? new Cursor(StandardCursorType.Hand) : new Cursor(StandardCursorType.Cross);

        Log.Information("Element picker mode: {Active}", _isElementPickerMode);
    }

    private void SetElementPickerMode(bool enabled)
    {
        if (_isElementPickerMode != enabled)
        {
            _isElementPickerMode = enabled;

            // Update element highlight state
            if (_elementHighlight != null)
            {
                _elementHighlight.IsActive = _isElementPickerMode;
            }

            // Hide/show selection overlay based on mode
            var selector = this.FindControl<SelectionOverlay>("Selector");
            if (selector != null)
            {
                selector.IsHitTestVisible = !_isElementPickerMode;
                selector.IsVisible = !_isElementPickerMode;
            }

            // Set appropriate cursor
            this.Cursor = _isElementPickerMode ? new Cursor(StandardCursorType.Hand) : new Cursor(StandardCursorType.Cross);
        }
    }

    private void OnElementSelected(DetectedElement element)
    {
        Log.Information("Element selected: {Name} - {Bounds}", element.Name, element.Bounds);

        // Convert element bounds to overlay coordinates and set selection
        var selector = this.FindControl<SelectionOverlay>("Selector");
        if (selector != null)
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
            selector.IsVisible = true;
            selector.IsHitTestVisible = true;

            // Set the selection and switch back to normal mode
            selector.SetSelection(selectionRect);

            // Disable element picker mode and set editable selection flag
            _isElementPickerMode = false;
            _hasEditableSelection = true; // Mark that we now have an editable selection

            if (_elementHighlight != null)
            {
                _elementHighlight.IsActive = false;
                GlobalElementHighlightState.Instance.ClearOwner(_elementHighlight);
            }

            // Reset cursor to normal selection mode (let overlay handle cursor)
            // this.Cursor = new Cursor(StandardCursorType.Cross);

            // Raise public event with isEditableSelection = true
            RegionSelected?.Invoke(this, new RegionSelectedEventArgs(selectionRect, false, element, true));

            Log.Information("Switched to selection mode with element bounds - editable selection created");
        }
    }

    private void OnOverlayPointerMoved(object? sender, PointerEventArgs e)
    {
        // Only handle mouse events in element picker mode
        if (!_isElementPickerMode || _elementDetector == null || _elementHighlight == null)
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
        if (_isElementPickerMode && _elementDetector != null)
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

            // End text editing before capture to prevent TextBox background artifacts
            if (_annotator != null)
            {
                var endTextEditingMethod = _annotator.GetType()
                    .GetMethod("EndTextEditing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                endTextEditingMethod?.Invoke(_annotator, null);
            }

            // Temporarily hide UI elements
            var selector = this.FindControl<SelectionOverlay>("Selector");
            bool selectorWasVisible = selector?.IsVisible ?? false;
            if (selector != null)
            {
                selector.IsVisible = false;
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

                var exportService = new ExportService();
                return await exportService.CreateCompositeImageWithAnnotationsAsync(
                    baseScreenshot, annotations, region);
            }
            finally
            {
                // Restore UI elements
                if (selector != null && selectorWasVisible)
                {
                    selector.IsVisible = true;
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
        if (_annotator == null)
            return null;

        // Access annotation service using reflection (necessary due to private field)
        var annotationServiceField = _annotator.GetType()
            .GetField("_annotationService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var annotationService = annotationServiceField?.GetValue(_annotator) as Services.Annotation.IAnnotationService;
        
        return annotationService?.Manager?.Items;
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
            if (_annotator == null)
                return;

            var selectionRect = _annotator.SelectionRect;
            if (selectionRect.Width <= 0 || selectionRect.Height <= 0)
            {
                Log.Warning("Cannot export: no valid selection area");
                return;
            }

            // Show export settings dialog
            var exportService = new ExportService();
            var defaultSettings = exportService.GetDefaultSettings();
            var imageSize = new Avalonia.Size(selectionRect.Width, selectionRect.Height);
            var settingsDialog = new ExportSettingsDialog(defaultSettings, imageSize);

            var dialogResult = await settingsDialog.ShowDialog<bool?>(this);
            if (dialogResult != true)
            {
                Log.Information("Export cancelled by user");
                return;
            }

            var settings = settingsDialog.Settings;

            // Show save file dialog with appropriate file types
            var storageProvider = GetTopLevel(this)?.StorageProvider;
            if (storageProvider == null)
            {
                Log.Error("Cannot access storage provider for file dialog");
                return;
            }

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
                return;
            }

            // Show progress dialog and perform export
            var progressDialog = new ExportProgressDialog();
            progressDialog.SetFileInfo(System.IO.Path.GetFileName(file.Path.LocalPath), settings.Format.ToString());

            // Show progress dialog without blocking
            var progressTask = progressDialog.ShowDialog(this);

            // Hide selection border before capturing screenshot
            var selector = this.FindControl<SelectionOverlay>("Selector");
            var wasVisible = selector?.IsVisible ?? false;

            try
            {
                progressDialog.UpdateProgress(5, "Preparing capture...");

                if (selector != null)
                {
                    selector.IsVisible = false;
                    // Give a moment for the UI to update
                    await Task.Delay(50);
                }

                try
                {
                    // Use WYSIWYG capture: directly capture what user sees
                    progressDialog.UpdateProgress(10, "Capturing screenshot...");
                    var finalImage = await CaptureWindowRegionWithAnnotationsAsync(selectionRect);
                    if (finalImage == null)
                    {
                        throw new InvalidOperationException("Failed to capture screenshot");
                    }

                    // Export the captured image directly (no re-rendering)
                    await exportService.ExportToFileDirectAsync(finalImage, file.Path.LocalPath, settings,
                        (percentage, status) =>
                        {
                            // Ensure progress updates are always dispatched to UI thread
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                progressDialog.UpdateProgress(percentage, status),
                                Avalonia.Threading.DispatcherPriority.Background);
                        });

                    // Close progress dialog and overlay window immediately after successful export
                    progressDialog.Close();
                    Log.Information("Successfully exported annotated screenshot to {FilePath} with settings: {Format}, Quality: {Quality}",
                        file.Path.LocalPath, settings.Format, settings.Quality);

                    // Close all overlay windows, not just this one
                    var overlayController = App.Services?.GetService(typeof(IOverlayController)) as IOverlayController;
                    if (overlayController != null)
                    {
                        overlayController.CloseAll();
                        Log.Information("All overlay windows closed after export");
                    }
                    else
                    {
                        // Fallback: close just this window
                        Close();
                        Log.Warning("Could not get overlay controller, closing only current window");
                    }
                }
                finally
                {
                    // Restore selection border visibility
                    if (selector != null && wasVisible)
                    {
                        selector.IsVisible = true;
                    }
                }
            }
            catch (Exception exportEx)
            {
                // Restore selection border visibility on error
                if (selector != null && wasVisible)
                {
                    selector.IsVisible = true;
                }

                var errorMessage = exportEx.InnerException?.Message ?? exportEx.Message;
                progressDialog.ShowError($"Export failed: {errorMessage}");
                Log.Error(exportEx, "Failed to export screenshot");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to handle export request");
        }
    }

    /// <summary>
    /// Handle color picker request from annotation overlay
    /// </summary>
    private void HandleColorPickerRequest()
    {
        try
        {
            // Find the toolbar and trigger color picker
            if (this.FindControl<NewAnnotationToolbar>("Toolbar") is { } toolbar)
            {
                // Use reflection to call the private ShowColorPicker method
                var showColorPickerMethod = toolbar.GetType()
                    .GetMethod("ShowColorPicker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (showColorPickerMethod != null)
                {
                    showColorPickerMethod.Invoke(toolbar, null);
                    Log.Debug("Color picker opened via keyboard shortcut");
                }
                else
                {
                    Log.Warning("Could not find ShowColorPicker method in toolbar");
                }
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
}
