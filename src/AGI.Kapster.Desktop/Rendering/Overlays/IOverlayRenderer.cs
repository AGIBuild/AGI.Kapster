using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.Overlay;
using Avalonia;
using SkiaSharp;

namespace AGI.Kapster.Desktop.Rendering.Overlays;

/// <summary>
/// Platform-specific overlay renderer
/// </summary>
public interface IOverlayRenderer
{
    /// <summary>
    /// Renders a selection box
    /// </summary>
    void RenderSelectionBox(SKCanvas canvas, Rect bounds, bool isActive = true);

    /// <summary>
    /// Renders element highlight
    /// </summary>
    void RenderElementHighlight(SKCanvas canvas, IElementInfo element);

    /// <summary>
    /// Renders crosshair at the specified position
    /// </summary>
    void RenderCrosshair(SKCanvas canvas, Point position, double canvasWidth, double canvasHeight);

    /// <summary>
    /// Renders magnifier at the specified position
    /// </summary>
    void RenderMagnifier(SKCanvas canvas, Point position, SKBitmap source, double zoomLevel = 2.0);

    /// <summary>
    /// Renders dimension labels for a selection
    /// </summary>
    void RenderDimensionLabels(SKCanvas canvas, Rect bounds);

    /// <summary>
    /// Renders pixel color information
    /// </summary>
    void RenderPixelInfo(SKCanvas canvas, Point position, SKColor pixelColor);

    /// <summary>
    /// Renders capture mode indicator
    /// </summary>
    void RenderCaptureMode(SKCanvas canvas, CaptureMode mode, Rect canvasBounds);

    /// <summary>
    /// Gets or sets the theme for rendering
    /// </summary>
    OverlayTheme Theme { get; set; }
}

/// <summary>
/// Theme settings for overlay rendering
/// </summary>
public class OverlayTheme
{
    public SKColor SelectionBorderColor { get; set; } = SKColors.Blue;
    public SKColor SelectionFillColor { get; set; } = SKColors.Blue.WithAlpha(30);
    public SKColor ElementHighlightColor { get; set; } = SKColors.Red;
    public SKColor CrosshairColor { get; set; } = SKColors.White;
    public SKColor TextColor { get; set; } = SKColors.White;
    public SKColor TextBackgroundColor { get; set; } = SKColors.Black.WithAlpha(180);
    public float BorderWidth { get; set; } = 2f;
    public float CrosshairWidth { get; set; } = 1f;
    public SKTypeface TextTypeface { get; set; } = SKTypeface.Default;
    public float TextSize { get; set; } = 14f;
}


