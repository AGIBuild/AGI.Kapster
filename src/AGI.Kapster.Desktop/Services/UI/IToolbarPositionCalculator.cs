using Avalonia;
using Avalonia.Platform;
using System.Collections.Generic;

namespace AGI.Kapster.Desktop.Services.UI;

/// <summary>
/// Service for calculating optimal toolbar position in overlay windows
/// </summary>
public interface IToolbarPositionCalculator
{
    /// <summary>
    /// Calculate optimal toolbar position
    /// </summary>
    Point CalculatePosition(ToolbarPositionContext context);
}

/// <summary>
/// Context data for toolbar position calculation
/// </summary>
public record ToolbarPositionContext(
    Rect Selection,
    Size ToolbarSize,
    PixelPoint OverlayPosition,
    IReadOnlyList<Screen>? Screens,
    double Margin = 8.0);

