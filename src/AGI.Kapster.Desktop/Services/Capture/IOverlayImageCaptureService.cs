using System.Collections.Generic;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AGI.Kapster.Desktop.Services.Capture;

/// <summary>
/// Service for capturing and compositing images from overlay windows
/// Handles base screenshot extraction, annotation overlay, and region capture
/// </summary>
public interface IOverlayImageCaptureService
{
    /// <summary>
    /// Capture a region from the overlay window with annotations
    /// </summary>
    Task<Bitmap?> CaptureWindowRegionWithAnnotationsAsync(
        Rect region,
        Bitmap? frozenBackground,
        IEnumerable<IAnnotationItem>? annotations,
        Size windowBounds);

    /// <summary>
    /// Extract a region from the frozen background with DPI-aware scaling
    /// </summary>
    Bitmap? ExtractRegionFromBackground(Rect region, Bitmap frozenBackground, Size windowBounds);

    /// <summary>
    /// Get base screenshot for a region (uses frozen background if available, otherwise live capture)
    /// </summary>
    Task<Bitmap?> GetBaseScreenshotForRegionAsync(
        Rect region,
        Bitmap? frozenBackground,
        Size windowBounds,
        Window window);

    /// <summary>
    /// Capture a region using the screen capture strategy
    /// </summary>
    Task<Bitmap?> CaptureRegionAsync(Rect rect, Window window);

    /// <summary>
    /// Get full screen screenshot for color sampling
    /// </summary>
    Task<Bitmap?> GetFullScreenScreenshotAsync(Window window, Size bounds);

    /// <summary>
    /// Determine which screen the selection region is on (using center point)
    /// </summary>
    Screen? GetScreenForSelection(Rect selectionRect, IReadOnlyList<Screen>? screens);
}

