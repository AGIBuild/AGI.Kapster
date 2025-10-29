using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Services.ElementDetection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Serilog;
using System;

namespace AGI.Kapster.Desktop.Overlays.Handlers;

/// <summary>
/// Manages element detection, highlighting, and element picker mode
/// Handles throttling and mouse event processing for element selection
/// </summary>
internal sealed class ElementDetectionHandler
{
    private readonly Window _window;
    private readonly IElementDetector _elementDetector;
    private readonly ElementHighlightOverlay _elementHighlight;
    
    // Throttling for element detection to prevent excessive updates
    private PixelPoint _lastDetectionPos;
    private DateTime _lastDetectionTime = DateTime.MinValue;
    private const double MinMovementThreshold = 8.0; // pixels
    private static readonly TimeSpan MinDetectionInterval = TimeSpan.FromMilliseconds(30); // ~33 FPS max

    // Events
    public event Action<DetectedElement>? ElementSelected;

    public ElementDetectionHandler(
        Window window,
        IElementDetector elementDetector,
        ElementHighlightOverlay elementHighlight)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _elementDetector = elementDetector ?? throw new ArgumentNullException(nameof(elementDetector));
        _elementHighlight = elementHighlight ?? throw new ArgumentNullException(nameof(elementHighlight));
        
        // Initially disable element highlight
        _elementHighlight.IsActive = false;
    }

    /// <summary>
    /// Enable element picker mode (shows element highlights)
    /// </summary>
    public void EnableElementPicker()
    {
        Log.Debug("EnableElementPicker called");
        
        // Ensure element highlight is properly initialized before enabling
        if (_elementHighlight == null)
        {
            Log.Warning("ElementHighlight is null, cannot enable element picker");
            return;
        }
        
        _elementHighlight.IsActive = true;
        
        // Additional verification - check if element detector is ready
        if (!_elementDetector.IsDetectionActive)
        {
            Log.Warning("Element detector is not active, element highlighting may not work");
        }
        
        Log.Debug("Element picker enabled - IsActive: {IsActive}", _elementHighlight.IsActive);
    }

    /// <summary>
    /// Disable element picker mode (hides element highlights)
    /// </summary>
    public void DisableElementPicker()
    {
        _elementHighlight.IsActive = false;
    }

    /// <summary>
    /// Toggle detection mode (window vs element)
    /// </summary>
    public void ToggleDetectionMode()
    {
        _elementDetector.ToggleDetectionMode();
    }

    /// <summary>
    /// Handle pointer moved event - updates element highlight if in element picker mode
    /// </summary>
    public void HandlePointerMoved(PointerEventArgs e, bool isElementPickerMode)
    {
        if (!isElementPickerMode)
            return;

        var position = e.GetPosition(_window);
        var screenPos = _window.PointToScreen(position);

        // Throttle element detection to prevent excessive updates
        if (ShouldUpdateElementDetection(screenPos))
        {
            try
            {
                var overlayHandle = _window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                var element = _elementDetector.DetectElementAt((int)screenPos.X, (int)screenPos.Y, overlayHandle);
                
                // Debug log removed to avoid performance impact in high-frequency handler
                
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

    /// <summary>
    /// Handle pointer pressed event - selects element if in element picker mode
    /// </summary>
    /// <returns>True if event was handled</returns>
    public bool HandlePointerPressed(PointerPressedEventArgs e, bool isElementPickerMode)
    {
        if (!isElementPickerMode)
            return false;

        var position = e.GetPosition(_window);
        var screenPos = _window.PointToScreen(position);

        try
        {
            var overlayHandle = _window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            var element = _elementDetector.DetectElementAt((int)screenPos.X, (int)screenPos.Y, overlayHandle);
            
            if (element != null)
            {
                Log.Information("Element detected and selected: {Name} - {Bounds}", element.Name, element.Bounds);
                ElementSelected?.Invoke(element);
                return true;
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

        return false;
    }

    /// <summary>
    /// Convert detected element bounds from screen coordinates to overlay window coordinates
    /// </summary>
    public Rect ConvertElementBoundsToOverlay(DetectedElement element)
    {
        var screenBounds = element.Bounds;
        var overlayTopLeft = _window.PointToClient(new PixelPoint((int)screenBounds.X, (int)screenBounds.Y));
        var overlayBottomRight = _window.PointToClient(new PixelPoint(
            (int)(screenBounds.X + screenBounds.Width),
            (int)(screenBounds.Y + screenBounds.Height)));

        return new Rect(
            Math.Min(overlayTopLeft.X, overlayBottomRight.X),
            Math.Min(overlayTopLeft.Y, overlayBottomRight.Y),
            Math.Abs(overlayBottomRight.X - overlayTopLeft.X),
            Math.Abs(overlayBottomRight.Y - overlayTopLeft.Y));
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
}
