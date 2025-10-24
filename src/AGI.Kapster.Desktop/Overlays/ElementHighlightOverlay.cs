using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Serilog;
using SkiaSharp;
using AGI.Kapster.Desktop.Services.Overlay;
using AGI.Kapster.Desktop.Services.Overlay.State;
using AGI.Kapster.Desktop.Services.ElementDetection;

namespace AGI.Kapster.Desktop.Overlays;

/// <summary>
/// Event args for element highlight changes
/// </summary>
public class ElementHighlightChangedEventArgs : EventArgs
{
    public DetectedElement? Element { get; }
    public Rect HighlightRect { get; }

    public ElementHighlightChangedEventArgs(DetectedElement? element, Rect rect)
    {
        Element = element;
        HighlightRect = rect;
    }
}

/// <summary>
/// GPU-accelerated overlay for highlighting detected UI elements
/// Uses SkiaSharp for high-performance rendering
/// </summary>
public class ElementHighlightOverlay : UserControl
{
    private readonly IElementDetector _elementDetector;
    private IOverlaySession? _session; // Session-scoped coordination (replaces GlobalElementHighlightState)
    private DetectedElement? _currentElement;
    private bool _isActive;
    private Rect _lastHighlightRect;
    private WriteableBitmap? _renderBitmap;
    private PixelSize _lastBitmapSize;
    private bool _needsRedraw;

    // Event for notifying mask cutout changes with element info
    public event EventHandler<ElementHighlightChangedEventArgs>? HighlightChanged;

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
    
    /// <summary>
    /// Sets the session for this highlight overlay (called during initialization)
    /// Replaces global singleton with session-scoped coordination
    /// </summary>
    public void SetSession(IOverlaySession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Log.Debug("ElementHighlightOverlay: Session reference set");
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
    /// GPU-accelerated version with minimal overhead
    /// Uses session-scoped coordination instead of global singleton
    /// </summary>
    public void SetCurrentElement(DetectedElement? element)
    {
        // Use session state to prevent multiple highlights across screens
        if (_session == null)
        {
            Log.Error("ElementHighlightOverlay: Session not set, cannot highlight element. Call SetSession() first.");
            return;
        }
        
        bool shouldShow = _session.SetHighlightedElement(element, this);

        if (!shouldShow)
        {
            // Another overlay is handling this element, clear our highlight
            if (_currentElement != null)
            {
                _currentElement = null;
                _lastHighlightRect = default;
                _needsRedraw = true;
                InvalidateVisual();
                
                // Notify mask to clear cutout
                HighlightChanged?.Invoke(this, new ElementHighlightChangedEventArgs(null, default));
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

            // Always update when element changes - no tolerance check
            _lastHighlightRect = newRect;
            _needsRedraw = true;

            // Immediate visual update (GPU-accelerated rendering is fast enough)
            InvalidateVisual();
            
            // Notify mask layer for cutout update with element info
            HighlightChanged?.Invoke(this, new ElementHighlightChangedEventArgs(element, newRect));

            if (element != null)
            {
                Log.Debug("Element highlight updated: {Name} ({ClassName}) - {Bounds}",
                    element.Name, element.ClassName, element.Bounds);
            }
        }
    }

    private static bool ElementEquals(DetectedElement? a, DetectedElement? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;

        // Simple comparison by window handle and class name
        return a.WindowHandle == b.WindowHandle && 
               a.ClassName == b.ClassName && 
               a.IsWindow == b.IsWindow;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (!IsActive || _currentElement == null || _lastHighlightRect == default)
        {
            // Clear any existing bitmap
            if (_renderBitmap != null)
            {
                _renderBitmap.Dispose();
                _renderBitmap = null;
                _needsRedraw = false;
            }
            return;
        }

        var rect = _lastHighlightRect;
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        try
        {
            // Create or recreate bitmap if size changed
            var currentSize = new PixelSize((int)this.Bounds.Width, (int)this.Bounds.Height);
            if (_renderBitmap == null || _lastBitmapSize != currentSize || _needsRedraw)
            {
                _renderBitmap?.Dispose();
                
                // Create new WriteableBitmap for GPU-accelerated rendering
                _renderBitmap = new WriteableBitmap(
                    currentSize,
                    new Vector(96, 96),
                    Avalonia.Platform.PixelFormat.Bgra8888,
                    AlphaFormat.Premul);
                
                _lastBitmapSize = currentSize;
                _needsRedraw = false;
                
                // Render using SkiaSharp (GPU-accelerated)
                RenderHighlightToBitmap(_renderBitmap, rect, _currentElement);
            }
            
            // Draw the cached bitmap (extremely fast)
            if (_renderBitmap != null)
            {
                context.DrawImage(_renderBitmap, 
                    new Rect(0, 0, _renderBitmap.PixelSize.Width, _renderBitmap.PixelSize.Height));
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error rendering GPU-accelerated highlight");
        }
    }
    
    /// <summary>
    /// Render highlight to bitmap using SkiaSharp for GPU acceleration
    /// </summary>
    private void RenderHighlightToBitmap(WriteableBitmap bitmap, Rect rect, DetectedElement element)
    {
        using (var frameBuffer = bitmap.Lock())
        {
            var info = new SKImageInfo(
                frameBuffer.Size.Width,
                frameBuffer.Size.Height,
                SKColorType.Bgra8888,
                SKAlphaType.Premul);
            
            using (var surface = SKSurface.Create(info, frameBuffer.Address, frameBuffer.RowBytes))
            {
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.Transparent);
                
                // Draw semi-transparent fill
                var borderColor = element.IsWindow 
                    ? new SKColor(255, 165, 0, 12) // Orange with 5% opacity
                    : new SKColor(0, 191, 255, 12); // DeepSkyBlue with 5% opacity
                
                using (var fillPaint = new SKPaint())
                {
                    fillPaint.Color = borderColor;
                    fillPaint.IsAntialias = true;
                    fillPaint.Style = SKPaintStyle.Fill;
                    
                    canvas.DrawRect(
                        (float)rect.X, (float)rect.Y,
                        (float)rect.Width, (float)rect.Height,
                        fillPaint);
                }
                
                // Draw outer border
                var strokeColor = element.IsWindow 
                    ? new SKColor(255, 165, 0) // Orange
                    : new SKColor(0, 191, 255); // DeepSkyBlue
                
                using (var borderPaint = new SKPaint())
                {
                    borderPaint.Color = strokeColor;
                    borderPaint.IsAntialias = true;
                    borderPaint.Style = SKPaintStyle.Stroke;
                    borderPaint.StrokeWidth = 3;
                    
                    canvas.DrawRect(
                        (float)rect.X, (float)rect.Y,
                        (float)rect.Width, (float)rect.Height,
                        borderPaint);
                }
                
                // Draw inner white border for contrast
                using (var innerPaint = new SKPaint())
                {
                    innerPaint.Color = SKColors.White;
                    innerPaint.IsAntialias = true;
                    innerPaint.Style = SKPaintStyle.Stroke;
                    innerPaint.StrokeWidth = 1;
                    
                    var innerRect = rect.Deflate(new Thickness(2));
                    if (innerRect.Width > 4 && innerRect.Height > 4)
                    {
                        canvas.DrawRect(
                            (float)innerRect.X, (float)innerRect.Y,
                            (float)innerRect.Width, (float)innerRect.Height,
                            innerPaint);
                    }
                }
                
                // Draw element info text (only if rect is large enough)
                if (rect.Width > 100 && rect.Height > 50)
                {
                    DrawElementInfoSkia(canvas, element, rect);
                }
            }
        }
    }
    
    /// <summary>
    /// Draw element info using SkiaSharp with modern API
    /// </summary>
    private void DrawElementInfoSkia(SKCanvas canvas, DetectedElement element, Rect bounds)
    {
        var displayText = $"{element.Name}\n{element.ClassName}\n{element.ProcessName}";
        displayText += element.IsWindow ? "\n[Window]" : "\n[Element]";
        
        using (var font = new SKFont())
        {
            font.Size = 12;
            font.Typeface = SKTypeface.FromFamilyName("Segoe UI");
            
            using (var textPaint = new SKPaint())
            {
                textPaint.Color = SKColors.White;
                textPaint.IsAntialias = true;
                
                // Measure text using modern API
                var textBounds = new SKRect();
                font.MeasureText(displayText, out textBounds);

        // Position text above the element if possible, otherwise below
                float textY = (float)bounds.Y - textBounds.Height - 10;
        if (textY < 0)
                    textY = (float)bounds.Bottom + 20;
                
                var bgRect = new SKRect(
                    (float)bounds.X - 4,
                    textY + textBounds.Top - 4,
                    (float)bounds.X + textBounds.Width + 8,
                    textY + textBounds.Bottom + 4);

        // Draw background
                using (var bgPaint = new SKPaint())
                {
                    bgPaint.Color = new SKColor(0, 0, 0, 204); // 80% opacity
                    bgPaint.IsAntialias = true;
                    canvas.DrawRect(bgRect, bgPaint);
                }
                
                // Draw text using modern API
                canvas.DrawText(displayText, (float)bounds.X, textY, SKTextAlign.Left, font, textPaint);
            }
        }
    }


    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _elementDetector.DetectionModeChanged -= OnDetectionModeChanged;

        // Clear session state if this overlay was the owner
        _session?.ClearHighlightOwner(this);
        
        // Dispose GPU resources
        if (_renderBitmap != null)
        {
            _renderBitmap.Dispose();
            _renderBitmap = null;
        }
    }
}
