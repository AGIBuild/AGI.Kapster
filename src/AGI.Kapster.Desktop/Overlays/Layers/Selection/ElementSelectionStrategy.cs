using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Serilog;
using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Overlays.Events;

namespace AGI.Kapster.Desktop.Overlays.Layers.Selection;

/// <summary>
/// Element detection and selection strategy (Ctrl key mode)
/// Detects UI elements under mouse cursor and highlights them
/// </summary>
public class ElementSelectionStrategy : ISelectionStrategy
{
    private readonly IElementDetector _detector;
    private readonly ElementHighlightOverlay _highlight;
    private readonly IMaskLayer _maskLayer;
    private readonly Window _overlayWindow;
    private readonly IOverlayEventBus _eventBus;
    
    private DetectedElement? _currentElement;
    private Rect _currentHighlightRect;
    
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<SelectionConfirmedEventArgs>? SelectionConfirmed;

    public ElementSelectionStrategy(
        IElementDetector detector,
        ElementHighlightOverlay highlight,
        IMaskLayer maskLayer,
        Window overlayWindow,
        IOverlayEventBus eventBus)
    {
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        _highlight = highlight ?? throw new ArgumentNullException(nameof(highlight));
        _maskLayer = maskLayer ?? throw new ArgumentNullException(nameof(maskLayer));
        _overlayWindow = overlayWindow ?? throw new ArgumentNullException(nameof(overlayWindow));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        
        // Subscribe to highlight changes
        _highlight.HighlightChanged += OnHighlightChanged;
    }

    public void Activate()
    {
        _highlight.IsActive = true;
        
        // Immediately detect element at current mouse position
        DetectAtCurrentMousePosition();
        
        Log.Debug("Element selection strategy activated");
    }

    public void Deactivate()
    {
        _highlight.IsActive = false;
        _highlight.SetCurrentElement(null);
        
        _currentElement = null;
        _currentHighlightRect = default;
        
        // Clear mask cutout
        _maskLayer.ClearCutout();
        
        Log.Debug("Element selection strategy deactivated");
    }

    public bool HandlePointerEvent(PointerEventArgs e)
    {
        if (e.RoutedEvent == InputElement.PointerMovedEvent)
        {
            return HandlePointerMoved(e);
        }
        else if (e.RoutedEvent == InputElement.PointerPressedEvent && e is PointerPressedEventArgs pressedArgs)
        {
            return HandlePointerPressed(pressedArgs);
        }
        
        return false;
    }

    public Rect? GetSelection()
    {
        return _currentHighlightRect != default ? _currentHighlightRect : null;
    }

    public DetectedElement? GetSelectedElement()
    {
        return _currentElement;
    }

    private bool HandlePointerMoved(PointerEventArgs e)
    {
        var position = e.GetPosition(_overlayWindow);
        
        // Fast check: if mouse is within current cutout rect, no need to update
        if (_maskLayer.IsPointInCutout(position))
        {
            return true; // Event handled, don't propagate
        }
        
        // Mouse is outside cutout area - detect and update
        var screenPos = _overlayWindow.PointToScreen(position);
        
        try
        {
            var overlayHandle = _overlayWindow.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            var element = _detector.DetectElementAt((int)screenPos.X, (int)screenPos.Y, overlayHandle);
            
            // Update element (will trigger highlight and mask cutout update via event)
            _currentElement = element;
            _highlight.SetCurrentElement(element);
            
            Log.Debug("Mouse outside cutout - element updated");
            
            return true; // Event handled
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error during element detection on mouse move");
            return false;
        }
    }

    private bool HandlePointerPressed(PointerPressedEventArgs e)
    {
        if (_currentElement != null && _currentHighlightRect != default)
        {
            // Confirm selection with cached element and rect
            SelectionConfirmed?.Invoke(this, 
                new SelectionConfirmedEventArgs(_currentHighlightRect, _currentElement));
            
            Log.Debug("Element selected: {Name}", _currentElement.Name);
            
            return true; // Event handled
        }
        
        Log.Warning("No element currently highlighted for selection");
        return false;
    }

    private void OnHighlightChanged(object? sender, ElementHighlightChangedEventArgs e)
    {
        _currentHighlightRect = e.HighlightRect;
        
        // Update mask cutout
        _maskLayer.SetCutout(e.HighlightRect);
        
        // Publish event
        _eventBus.Publish(new ElementHighlightedEvent(e.Element, e.HighlightRect));
        
        // Notify listeners
        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(e.HighlightRect));
    }

    /// <summary>
    /// Detect element at current mouse position when Ctrl is pressed
    /// </summary>
    private void DetectAtCurrentMousePosition()
    {
        try
        {
            // Get current mouse screen position using Win32 API
            if (!GetCursorPos(out var point))
            {
                Log.Warning("Failed to get cursor position");
                return;
            }
            
            var screenPos = new PixelPoint(point.X, point.Y);
            var overlayHandle = _overlayWindow.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            var element = _detector.DetectElementAt((int)screenPos.X, (int)screenPos.Y, overlayHandle);
            
            // Update element immediately
            _currentElement = element;
            _highlight.SetCurrentElement(element);
            
            Log.Debug("Ctrl pressed - immediate element detection at {ScreenPos}", screenPos);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error during immediate element detection on Ctrl press");
        }
    }

    // Win32 API for getting cursor position
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);
    
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}

