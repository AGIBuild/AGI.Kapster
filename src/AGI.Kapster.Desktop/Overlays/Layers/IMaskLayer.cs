using System;
using Avalonia;
using Avalonia.Media;

namespace AGI.Kapster.Desktop.Overlays.Layers;

/// <summary>
/// Mask layer interface for managing overlay mask with cutout regions
/// </summary>
public interface IMaskLayer : IOverlayLayer
{
    /// <summary>
    /// Set the size of the mask
    /// </summary>
    void SetMaskSize(Size size);
    
    /// <summary>
    /// Set mask opacity (0.0 - 1.0)
    /// </summary>
    void SetMaskOpacity(double opacity);
    
    /// <summary>
    /// Set mask color
    /// </summary>
    void SetMaskColor(Color color);
    
    /// <summary>
    /// Set cutout rectangle (creates a "hole" in the mask)
    /// </summary>
    void SetCutout(Rect rect);
    
    /// <summary>
    /// Clear the cutout (restore full mask)
    /// </summary>
    void ClearCutout();
    
    /// <summary>
    /// Get current cutout rectangle
    /// </summary>
    Rect GetCurrentCutout();
    
    /// <summary>
    /// Check if a point is within the current cutout region
    /// </summary>
    bool IsPointInCutout(Point point);
    
    /// <summary>
    /// Event raised when cutout changes
    /// </summary>
    event EventHandler<CutoutChangedEventArgs>? CutoutChanged;
}

/// <summary>
/// Event args for cutout changes
/// </summary>
public class CutoutChangedEventArgs : EventArgs
{
    public Rect OldCutout { get; }
    public Rect NewCutout { get; }
    
    public CutoutChangedEventArgs(Rect oldCutout, Rect newCutout)
    {
        OldCutout = oldCutout;
        NewCutout = newCutout;
    }
}

