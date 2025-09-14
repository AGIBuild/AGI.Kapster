using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AGI.Captor.Desktop.Services;
using Serilog;

namespace AGI.Captor.Desktop.Overlays;

/// <summary>
/// Overlay for highlighting detected UI elements
/// </summary>
public class ElementHighlightOverlay : UserControl
{
    private readonly IElementDetector _elementDetector;
    private DetectedElement? _currentElement;
    private bool _isActive;
    private Rect _lastHighlightRect;
    private bool _isRendering = false; // Prevent concurrent rendering

    // Element selection is now handled by parent OverlayWindow

    public ElementHighlightOverlay(IElementDetector elementDetector)
    {
        _elementDetector = elementDetector ?? throw new ArgumentNullException(nameof(elementDetector));
        
        Background = Brushes.Transparent;
        IsHitTestVisible = false; // Always disabled - this overlay should never intercept mouse events
        
        // Update highlight when detection mode changes
        _elementDetector.DetectionModeChanged += OnDetectionModeChanged;
        
        // Timer removed - all detection is now handled by OverlayWindow mouse events
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                _elementDetector.IsDetectionActive = value;
                
                if (!value)
                {
                    _currentElement = null;
                    InvalidateVisual();
                }
                
                Log.Information("Element highlight overlay active: {Active}", value);
            }
        }
    }

    // Remove all mouse and keyboard event handlers - this overlay is purely for display

    private void OnDetectionModeChanged(bool isWindowMode)
    {
        // Force refresh of current element
        if (IsActive)
        {
            InvalidateVisual();
        }
    }

    // Timer detection removed - all detection now handled by OverlayWindow mouse events

    /// <summary>
    /// Sets the current element to highlight (called from parent overlay)
    /// </summary>
    public void SetCurrentElement(DetectedElement? element)
    {
        // Use global state to prevent multiple highlights across screens
        bool shouldShow = GlobalElementHighlightState.Instance.SetCurrentElement(element, this);
        
        if (!shouldShow)
        {
            // Another overlay is handling this element, clear our highlight
            if (_currentElement != null)
            {
                _currentElement = null;
                _lastHighlightRect = default;
                InvalidateVisual();
            }
            return;
        }
        
        if (!ElementEquals(_currentElement, element))
        {
            _currentElement = element;
            
            // Calculate the new highlight rect
            Rect newRect = default;
            if (element != null)
            {
                var screenBounds = element.Bounds;
                var overlayTopLeft = this.PointToClient(new PixelPoint((int)screenBounds.X, (int)screenBounds.Y));
                var overlayBottomRight = this.PointToClient(new PixelPoint(
                    (int)(screenBounds.X + screenBounds.Width), 
                    (int)(screenBounds.Y + screenBounds.Height)));
                
                newRect = new Rect(
                    Math.Min(overlayTopLeft.X, overlayBottomRight.X),
                    Math.Min(overlayTopLeft.Y, overlayBottomRight.Y),
                    Math.Abs(overlayBottomRight.X - overlayTopLeft.X),
                    Math.Abs(overlayBottomRight.Y - overlayTopLeft.Y));
            }
            
            // Only invalidate if the rect actually changed significantly
            if (!RectsAreEqual(_lastHighlightRect, newRect, 3.0)) // Increased tolerance to 3 pixels
            {
                _lastHighlightRect = newRect;
                
                // Defer the visual update to prevent excessive redraws
                if (!_isRendering)
                {
                    _isRendering = true;
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            // Double-check that we still need to update (element might have changed again)
                            if (_currentElement == element && !_lastHighlightRect.Equals(default))
                            {
                                InvalidateVisual();
                            }
                        }
                        finally
                        {
                            _isRendering = false;
                        }
                    }, DispatcherPriority.Background); // Lower priority to reduce stuttering
                }
                
                if (element != null)
                {
                    Log.Debug("Detected element: {Name} ({ClassName}) - {Bounds}", 
                        element.Name, element.ClassName, element.Bounds);
                }
            }
        }
    }

    private static bool RectsAreEqual(Rect a, Rect b, double tolerance)
    {
        return Math.Abs(a.X - b.X) < tolerance &&
               Math.Abs(a.Y - b.Y) < tolerance &&
               Math.Abs(a.Width - b.Width) < tolerance &&
               Math.Abs(a.Height - b.Height) < tolerance;
    }

    private static bool ElementEquals(DetectedElement? a, DetectedElement? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        
        // Strict comparison to prevent unnecessary updates
        // Use window handle as primary identifier (most reliable)
        if (a.WindowHandle != b.WindowHandle)
            return false;
            
        // Additional checks for elements within the same window
        if (a.ClassName != b.ClassName || a.IsWindow != b.IsWindow)
            return false;
            
        // Use larger tolerance for bounds to account for minor coordinate variations
        return RectsAreEqual(a.Bounds, b.Bounds, 5.0); // Larger tolerance for more stability
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        
        if (!IsActive || _currentElement == null || _lastHighlightRect == default)
            return;
        
        var element = _currentElement;
        
        // Use the cached highlight rect instead of recalculating
        // This prevents coordinate inconsistencies that cause flickering
        var rect = _lastHighlightRect;
        
        // Validate rect before drawing
        if (rect.Width <= 0 || rect.Height <= 0)
            return;
        
        try
        {
            // Draw highlight border with stable visual
            var borderColor = element.IsWindow ? Colors.Orange : Colors.DeepSkyBlue;
            var borderBrush = new SolidColorBrush(borderColor);
            var borderPen = new Pen(borderBrush, 2); // Slightly thinner to reduce visual noise
            context.DrawRectangle(null, borderPen, rect);
            
            // Draw inner border for better contrast
            var innerPen = new Pen(Brushes.White, 1);
            var innerRect = rect.Deflate(new Thickness(1));
            if (innerRect.Width > 2 && innerRect.Height > 2)
            {
                context.DrawRectangle(null, innerPen, innerRect);
            }
            
            // Draw subtle semi-transparent fill
            var fillBrush = new SolidColorBrush(borderColor, 0.05); // More subtle
            context.DrawRectangle(fillBrush, null, rect);
            
            // Draw element info text (only if rect is large enough)
            if (rect.Width > 100 && rect.Height > 50)
            {
                DrawElementInfo(context, element, rect);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error rendering element highlight");
        }
    }

    private void DrawElementInfo(DrawingContext context, DetectedElement element, Rect bounds)
    {
        var displayText = $"{element.Name}\n{element.ClassName}\n{element.ProcessName}";
        if (element.IsWindow)
            displayText += "\n[Window]";
        else
            displayText += "\n[Element]";
        
        var typeface = new Typeface("Segoe UI");
        var formattedText = new FormattedText(
            displayText,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            12,
            Brushes.White);
        
        // Position text above the element if possible, otherwise below
        var textY = bounds.Y - formattedText.Height - 5;
        if (textY < 0)
            textY = bounds.Bottom + 5;
        
        var textRect = new Rect(bounds.X, textY, formattedText.Width + 8, formattedText.Height + 4);
        
        // Draw background
        context.DrawRectangle(new SolidColorBrush(Colors.Black, 0.8), null, textRect);
        
        // Draw text
        context.DrawText(formattedText, new Point(textRect.X + 4, textRect.Y + 2));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        
        _elementDetector.DetectionModeChanged -= OnDetectionModeChanged;
        
        // Clear global state if this overlay was the owner
        GlobalElementHighlightState.Instance.ClearOwner(this);
    }
}
