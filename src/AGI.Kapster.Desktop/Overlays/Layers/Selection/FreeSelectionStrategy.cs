using System;
using Avalonia;
using Avalonia.Input;
using Serilog;
using AGI.Kapster.Desktop.Services.ElementDetection;

namespace AGI.Kapster.Desktop.Overlays.Layers.Selection;

/// <summary>
/// Free-form drag selection strategy
/// Wraps the existing SelectionOverlay control
/// </summary>
public class FreeSelectionStrategy : ISelectionStrategy
{
    private readonly SelectionOverlay _selectionOverlay;
    
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<SelectionFinishedEventArgs>? SelectionFinished;
    public event EventHandler<SelectionConfirmedEventArgs>? SelectionConfirmed;

    public FreeSelectionStrategy(SelectionOverlay selectionOverlay)
    {
        _selectionOverlay = selectionOverlay ?? throw new ArgumentNullException(nameof(selectionOverlay));
        
        // Wire up events from SelectionOverlay
        _selectionOverlay.SelectionChanged += OnSelectionOverlayChanged;
        _selectionOverlay.SelectionFinished += OnSelectionOverlayFinished;
        _selectionOverlay.ConfirmRequested += OnSelectionOverlayConfirmed;
    }

    public void Activate()
    {
        _selectionOverlay.IsVisible = true;
        _selectionOverlay.IsHitTestVisible = true;
        
        Log.Debug("Free selection strategy activated");
    }

    public void Deactivate()
    {
        _selectionOverlay.IsVisible = false;
        _selectionOverlay.IsHitTestVisible = false;
        
        Log.Debug("Free selection strategy deactivated");
    }

    public bool HandlePointerEvent(PointerEventArgs e)
    {
        // SelectionOverlay handles its own pointer events through Avalonia's event system
        // We don't need to manually route events here
        return false;
    }

    public Rect? GetSelection()
    {
        var rect = _selectionOverlay.SelectionRect;
        return rect.Width > 0 && rect.Height > 0 ? rect : null;
    }

    public DetectedElement? GetSelectedElement()
    {
        // Free selection doesn't have an associated element
        return null;
    }

    private void OnSelectionOverlayChanged(Rect rect)
    {
        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(rect));
    }

    private void OnSelectionOverlayFinished(Rect rect)
    {
        // SelectionFinished = user finished dragging, entering editable state
        SelectionFinished?.Invoke(this, new SelectionFinishedEventArgs(rect, isEditableSelection: true));
    }
    
    private void OnSelectionOverlayConfirmed(Rect rect)
    {
        // ConfirmRequested = user double-clicked or pressed Enter to finalize
        SelectionConfirmed?.Invoke(this, new SelectionConfirmedEventArgs(rect));
    }
}

