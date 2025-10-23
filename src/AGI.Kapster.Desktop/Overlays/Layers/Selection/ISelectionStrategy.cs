using System;
using Avalonia;
using Avalonia.Input;
using AGI.Kapster.Desktop.Services.ElementDetection;

namespace AGI.Kapster.Desktop.Overlays.Layers.Selection;

/// <summary>
/// Strategy pattern for different selection methods
/// </summary>
public interface ISelectionStrategy
{
    /// <summary>
    /// Activate this selection strategy
    /// </summary>
    void Activate();
    
    /// <summary>
    /// Deactivate this selection strategy
    /// </summary>
    void Deactivate();
    
    /// <summary>
    /// Handle pointer event
    /// </summary>
    /// <returns>True if event was handled</returns>
    bool HandlePointerEvent(PointerEventArgs e);
    
    /// <summary>
    /// Get current selection rectangle
    /// </summary>
    Rect? GetSelection();
    
    /// <summary>
    /// Get selected element (if applicable)
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
/// Event args for selection changes
/// </summary>
public class SelectionChangedEventArgs : EventArgs
{
    public Rect Selection { get; }
    
    public SelectionChangedEventArgs(Rect selection)
    {
        Selection = selection;
    }
}

/// <summary>
/// Event args for selection confirmation
/// </summary>
public class SelectionConfirmedEventArgs : EventArgs
{
    public Rect Selection { get; }
    public DetectedElement? Element { get; }
    
    public SelectionConfirmedEventArgs(Rect selection, DetectedElement? element = null)
    {
        Selection = selection;
        Element = element;
    }
}

