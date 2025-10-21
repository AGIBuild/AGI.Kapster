using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using System.Collections.Generic;

namespace AGI.Kapster.Desktop.Services.Overlay.Coordinators;

/// <summary>
/// Platform-specific screen coordinate mapping and scaling interface
/// Transient lifetime: one instance per screenshot session
/// </summary>
public interface IScreenCoordinateMapper
{
    /// <summary>
    /// Cached screen information for current session
    /// Call InitializeScreens() at session start to populate this cache
    /// Valid only for the lifetime of this mapper instance (one screenshot operation)
    /// </summary>
    IReadOnlyList<Screen> Screens { get; }

    /// <summary>
    /// Initialize/refresh screen information cache
    /// Should be called at the start of each screenshot session
    /// Handles screen hot-plug scenarios by fetching latest screen configuration
    /// </summary>
    void InitializeScreens();

    /// <summary>
    /// Map logical DIP rectangle to physical pixel rectangle for a specific screen
    /// </summary>
    /// <param name="logicalRect">Logical rectangle in DIPs</param>
    /// <param name="screen">Target screen (null for primary screen)</param>
    /// <returns>Physical pixel rectangle</returns>
    PixelRect MapToPhysicalRect(Rect logicalRect, Screen? screen = null);

    /// <summary>
    /// Map physical pixel rectangle to logical DIP rectangle for a specific screen
    /// </summary>
    /// <param name="physicalRect">Physical pixel rectangle</param>
    /// <param name="screen">Target screen (null for primary screen)</param>
    /// <returns>Logical DIP rectangle</returns>
    Rect MapToLogicalRect(PixelRect physicalRect, Screen? screen = null);

    /// <summary>
    /// Get DPI scale factors for a specific screen
    /// </summary>
    /// <param name="screen">Target screen (null for primary screen)</param>
    /// <returns>(scaleX, scaleY) as tuple</returns>
    (double scaleX, double scaleY) GetScaleFactor(Screen? screen = null);

    /// <summary>
    /// Get the virtual desktop bounds (all screens combined)
    /// </summary>
    /// <returns>Virtual desktop bounds in logical DIPs</returns>
    Rect GetVirtualDesktopBounds();

    /// <summary>
    /// Get all available screens (uses cached Screens property)
    /// </summary>
    /// <returns>List of all screens</returns>
    IReadOnlyList<Screen> GetAllScreens();

    /// <summary>
    /// Get the screen containing a specific point
    /// </summary>
    /// <param name="point">Point in logical DIPs</param>
    /// <returns>Screen containing the point, or primary screen if not found</returns>
    Screen? GetScreenFromPoint(PixelPoint point);
}


