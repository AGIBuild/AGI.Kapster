using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace AGI.Kapster.Desktop.Overlays.Layers;

/// <summary>
/// Toolbar layer interface for managing annotation toolbar UI
/// </summary>
public interface IToolbarLayer : IOverlayLayer
{
    /// <summary>
    /// Update toolbar position based on selection and screen context
    /// </summary>
    /// <param name="selection">Current selection rectangle</param>
    /// <param name="canvasSize">Size of the overlay canvas</param>
    /// <param name="windowPosition">Overlay window position</param>
    /// <param name="screens">Available screens</param>
    void UpdatePosition(Rect selection, Size canvasSize, PixelPoint windowPosition, IReadOnlyList<Screen>? screens);
    
    /// <summary>
    /// Show color picker dialog
    /// </summary>
    void ShowColorPicker();
    
    /// <summary>
    /// Set the target annotation layer for toolbar interaction
    /// </summary>
    void SetAnnotationLayer(IAnnotationLayer? annotationLayer);
}

