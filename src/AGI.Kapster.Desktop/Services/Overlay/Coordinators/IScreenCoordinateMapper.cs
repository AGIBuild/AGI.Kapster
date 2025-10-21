using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using System.Collections.Generic;

namespace AGI.Kapster.Desktop.Services.Overlay.Coordinators;

/// <summary>
/// Platform-specific coordinate mapping and DPI scaling interface
/// Pure coordinate transformation - no screen management
/// Transient lifetime: one instance per screenshot session
/// </summary>
public interface IScreenCoordinateMapper
{
    /// <summary>
    /// Map logical DIP rectangle to physical pixel rectangle for a specific screen
    /// </summary>
    /// <param name="logicalRect">Logical rectangle in DIPs</param>
    /// <param name="screen">Target screen (required)</param>
    /// <returns>Physical pixel rectangle</returns>
    PixelRect MapToPhysicalRect(Rect logicalRect, Screen screen);

    /// <summary>
    /// Map physical pixel rectangle to logical DIP rectangle for a specific screen
    /// </summary>
    /// <param name="physicalRect">Physical pixel rectangle</param>
    /// <param name="screen">Target screen (required)</param>
    /// <returns>Logical DIP rectangle</returns>
    Rect MapToLogicalRect(PixelRect physicalRect, Screen screen);

    /// <summary>
    /// Get DPI scale factors for a specific screen
    /// </summary>
    /// <param name="screen">Target screen (required)</param>
    /// <returns>(scaleX, scaleY) as tuple</returns>
    (double scaleX, double scaleY) GetScaleFactor(Screen screen);

    /// <summary>
    /// Get the screen containing a specific point
    /// </summary>
    /// <param name="point">Point in logical DIPs</param>
    /// <param name="screens">Available screens to search</param>
    /// <returns>Screen containing the point, or null if not found</returns>
    Screen? GetScreenFromPoint(PixelPoint point, IReadOnlyList<Screen> screens);
}


