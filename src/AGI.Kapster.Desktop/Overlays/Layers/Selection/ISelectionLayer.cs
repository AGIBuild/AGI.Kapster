using System;
using Avalonia;
using AGI.Kapster.Desktop.Services.ElementDetection;

namespace AGI.Kapster.Desktop.Overlays.Layers.Selection;

/// <summary>
/// Selection layer interface for managing different selection strategies
/// </summary>
public interface ISelectionLayer : IOverlayLayer
{
    /// <summary>
    /// Current selection mode
    /// </summary>
    SelectionMode CurrentMode { get; }
    
    /// <summary>
    /// Switch to a different selection mode
    /// </summary>
    void SwitchMode(SelectionMode mode);
    
    /// <summary>
    /// Get current selection rectangle
    /// </summary>
    Rect? GetCurrentSelection();
    
    /// <summary>
    /// Get selected element (if in element mode)
    /// </summary>
    DetectedElement? GetSelectedElement();
    
    /// <summary>
    /// Event raised when selection changes
    /// </summary>
    event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    
    /// <summary>
    /// Event raised when selection is confirmed
    /// </summary>
    event EventHandler<SelectionConfirmedEventArgs>? SelectionConfirmed;
}

/// <summary>
/// Selection modes
/// </summary>
public enum SelectionMode
{
    /// <summary>
    /// Free-form drag selection
    /// </summary>
    Free,
    
    /// <summary>
    /// Element detection and selection (Ctrl key)
    /// </summary>
    Element
}

