using Avalonia;

namespace AGI.Kapster.Desktop.Overlays.Layers;

/// <summary>
/// P3 Fix: Unified selection validation logic
/// Centralizes selection validity criteria to prevent inconsistencies across layers
/// </summary>
public static class SelectionValidator
{
    /// <summary>
    /// Minimum width for a valid selection (in pixels)
    /// </summary>
    public const double MinWidth = 2.0;

    /// <summary>
    /// Minimum height for a valid selection (in pixels)
    /// </summary>
    public const double MinHeight = 2.0;

    /// <summary>
    /// Validates if a selection rectangle meets minimum size requirements
    /// </summary>
    /// <param name="rect">The selection rectangle to validate</param>
    /// <returns>True if selection is valid (meets minimum size), false otherwise</returns>
    public static bool IsValid(Rect rect)
    {
        return rect.Width >= MinWidth && rect.Height >= MinHeight;
    }

    /// <summary>
    /// Validates if a selection rectangle is valid and has positive dimensions
    /// More strict than IsValid() - checks for non-default rect
    /// </summary>
    /// <param name="rect">The selection rectangle to validate</param>
    /// <returns>True if selection exists and is valid, false otherwise</returns>
    public static bool Exists(Rect rect)
    {
        return rect != default && IsValid(rect);
    }
}

