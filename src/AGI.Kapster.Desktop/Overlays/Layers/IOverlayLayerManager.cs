using System;
using Avalonia;
using Avalonia.Input;

namespace AGI.Kapster.Desktop.Overlays.Layers;

/// <summary>
/// Manages all overlay layers and coordinates their interactions
/// </summary>
public interface IOverlayLayerManager
{
    /// <summary>
    /// Register a new layer
    /// </summary>
    void RegisterLayer(string layerId, IOverlayLayer layer);
    
    /// <summary>
    /// Unregister a layer
    /// </summary>
    void UnregisterLayer(string layerId);
    
    /// <summary>
    /// Get a layer by ID
    /// </summary>
    IOverlayLayer? GetLayer(string layerId);
    
    /// <summary>
    /// Show a specific layer
    /// </summary>
    void ShowLayer(string layerId);
    
    /// <summary>
    /// Hide a specific layer
    /// </summary>
    void HideLayer(string layerId);
    
    /// <summary>
    /// Set Z-index for a layer
    /// </summary>
    void SetLayerZIndex(string layerId, int zIndex);
    
    /// <summary>
    /// Set the currently active layer (receives events first)
    /// </summary>
    void SetActiveLayer(string layerId);
    
    /// <summary>
    /// Get the currently active layer
    /// </summary>
    IOverlayLayer? GetActiveLayer();
    
    /// <summary>
    /// Route pointer event to appropriate layers
    /// </summary>
    /// <returns>True if event was handled by any layer</returns>
    bool RoutePointerEvent(PointerEventArgs e);
    
    /// <summary>
    /// Route keyboard event to appropriate layers
    /// </summary>
    /// <returns>True if event was handled by any layer</returns>
    bool RouteKeyEvent(KeyEventArgs e);
    
    /// <summary>
    /// Switch overlay to a different mode
    /// This will activate/deactivate layers based on the mode
    /// </summary>
    void SwitchMode(OverlayMode mode);
    
    /// <summary>
    /// Get current overlay mode
    /// </summary>
    OverlayMode CurrentMode { get; }
    
    /// <summary>
    /// Register and attach a layer with visual to the host
    /// Combines RegisterLayer + AttachTo for cleaner API
    /// </summary>
    void RegisterAndAttachLayer(string layerId, IOverlayLayer layer, ILayerHost host, IOverlayContext context);
    
    /// <summary>
    /// Unregister and detach a layer from the host
    /// Combines Detach + UnregisterLayer for cleanup
    /// </summary>
    void UnregisterAndDetachLayer(string layerId);
    
    // === State Management (Phase 1) ===
    
    /// <summary>
    /// Current selection rectangle (single source of truth)
    /// </summary>
    Rect CurrentSelection { get; }
    
    /// <summary>
    /// Check if current selection is valid for annotation (width and height > 2px)
    /// </summary>
    bool HasValidSelection { get; }
    
    /// <summary>
    /// Set current selection rectangle
    /// Replaces event-based data passing with centralized state management
    /// </summary>
    void SetSelection(Rect selection);
    
    /// <summary>
    /// Clear current selection
    /// </summary>
    void ClearSelection();
    
    /// <summary>
    /// Event raised when selection changes (no data - pull from CurrentSelection)
    /// Layers should subscribe to this and query CurrentSelection when notified
    /// </summary>
    event EventHandler? SelectionChanged;
    
    /// <summary>
    /// Event raised when mode changes (no data - pull from CurrentMode)
    /// </summary>
    event EventHandler? ModeChanged;
}

