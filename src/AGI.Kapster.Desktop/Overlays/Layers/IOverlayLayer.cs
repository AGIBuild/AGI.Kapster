using Avalonia.Input;
using Avalonia.Media;

namespace AGI.Kapster.Desktop.Overlays.Layers;

/// <summary>
/// Defines the contract for an overlay layer
/// Each layer represents a specific functionality (mask, selection, annotation, etc.)
/// </summary>
public interface IOverlayLayer
{
    /// <summary>
    /// Unique identifier for this layer
    /// </summary>
    string LayerId { get; }
    
    /// <summary>
    /// Z-index for layer ordering (higher values are on top)
    /// </summary>
    int ZIndex { get; set; }
    
    /// <summary>
    /// Whether the layer is visible
    /// </summary>
    bool IsVisible { get; set; }
    
    /// <summary>
    /// Whether the layer can receive pointer/keyboard events
    /// </summary>
    bool IsInteractive { get; set; }
    
    /// <summary>
    /// Called when the layer is activated
    /// </summary>
    void OnActivate();
    
    /// <summary>
    /// Called when the layer is deactivated
    /// </summary>
    void OnDeactivate();
    
    /// <summary>
    /// Handle pointer events (move, press, release)
    /// </summary>
    /// <returns>True if event was handled and should not propagate</returns>
    bool HandlePointerEvent(PointerEventArgs e);
    
    /// <summary>
    /// Handle keyboard events
    /// </summary>
    /// <returns>True if event was handled and should not propagate</returns>
    bool HandleKeyEvent(KeyEventArgs e);
    
    /// <summary>
    /// Check if this layer can handle the specified overlay mode
    /// </summary>
    bool CanHandle(OverlayMode mode);
}

/// <summary>
/// Overlay operating modes
/// </summary>
public enum OverlayMode
{
    /// <summary>
    /// Initial/uninitialized state, no mode active
    /// </summary>
    None,
    
    /// <summary>
    /// Free-form drag selection mode
    /// </summary>
    FreeSelection,
    
    /// <summary>
    /// Element detection and selection mode (Ctrl key)
    /// </summary>
    ElementPicker,
    
    /// <summary>
    /// Annotation editing mode
    /// </summary>
    Annotation,
    
    /// <summary>
    /// Color picker mode
    /// </summary>
    ColorPicker
}

