namespace AGI.Kapster.Desktop.Overlays;

/// <summary>
/// Constants for overlay window behavior and UI
/// </summary>
internal static class OverlayConstants
{
    #region UI Update Delays (milliseconds)

    /// <summary>
    /// Short delay for UI initialization updates
    /// </summary>
    public const int ShortUiUpdateDelay = 10;

    /// <summary>
    /// Frame delay for opacity changes (approximately one frame at 60fps)
    /// </summary>
    public const int FrameDelay = 16;

    /// <summary>
    /// Standard delay for UI state changes and visual updates
    /// </summary>
    public const int StandardUiDelay = 50;

    #endregion

    #region Opacity Values

    /// <summary>
    /// Fully transparent window (used during screen capture)
    /// </summary>
    public const double TransparentOpacity = 0.0;

    /// <summary>
    /// Fully opaque window (normal state)
    /// </summary>
    public const double OpaqueOpacity = 1.0;

    #endregion

    #region Hit Testing Offsets

    /// <summary>
    /// Offset in pixels for detecting selection edges and corners
    /// </summary>
    public const double EdgeDetectionOffset = 8.0;

    #endregion
}

