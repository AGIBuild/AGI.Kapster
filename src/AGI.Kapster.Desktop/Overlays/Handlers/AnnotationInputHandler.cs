using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Services.Annotation;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using Serilog;
using System;
using System.Linq;

namespace AGI.Kapster.Desktop.Overlays.Handlers;

/// <summary>
/// Handles input events for annotation overlay
/// </summary>
public class AnnotationInputHandler
{
    private readonly NewAnnotationOverlay _overlay;
    private readonly IAnnotationService _annotationService;
    private readonly AnnotationTransformHandler _transformHandler;
    private readonly AnnotationEditingHandler _editingHandler;
    private readonly AnnotationRenderingHandler _renderingHandler;

    public AnnotationInputHandler(
        NewAnnotationOverlay overlay,
        IAnnotationService annotationService,
        AnnotationTransformHandler transformHandler,
        AnnotationEditingHandler editingHandler,
        AnnotationRenderingHandler renderingHandler)
    {
        _overlay = overlay;
        _annotationService = annotationService;
        _transformHandler = transformHandler;
        _editingHandler = editingHandler;
        _renderingHandler = renderingHandler;
    }

    /// <summary>
    /// Handle pointer pressed events
    /// </summary>
    public void HandlePointerPressed(PointerPressedEventArgs e)
    {
        try
        {
            var point = e.GetPosition(_overlay);
            var isLeftButton = e.GetCurrentPoint(_overlay).Properties.IsLeftButtonPressed;
            var isRightButton = e.GetCurrentPoint(_overlay).Properties.IsRightButtonPressed;

            // Handle text editing mode first
            if (_editingHandler.IsEditing)
            {
                _editingHandler.HandlePointerPressed(e);
                if (e.Handled) return;
            }

            if (!isLeftButton) return;

            // Handle different modes
            if (_overlay.CurrentTool == AnnotationToolType.None)
            {
                // Selection mode - handle selection and transformation
                _transformHandler.HandleSelectionPress(point, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            }
            else
            {
                // Annotation creation mode
                HandleAnnotationCreation(point);
            }

            e.Handled = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in HandlePointerPressed at {Point}", e.GetPosition(_overlay));
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handle pointer moved events
    /// </summary>
    public void HandlePointerMoved(PointerEventArgs e)
    {
        try
        {
            var point = e.GetCurrentPoint(_overlay).Position;

            if (_overlay.CurrentTool == AnnotationToolType.None)
            {
                // Selection mode - handle transformation
                _transformHandler.HandleSelectionMove(point);
            }
            else
            {
                // Annotation creation mode
                HandleAnnotationUpdate(point);
            }

            // Force immediate UI update to reduce visual latency
            Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in HandlePointerMoved at {Point}", e.GetCurrentPoint(_overlay).Position);
        }
    }

    /// <summary>
    /// Handle pointer released events
    /// </summary>
    public void HandlePointerReleased(PointerReleasedEventArgs e)
    {
        try
        {
            var point = e.GetPosition(_overlay);
            var hasSelection = _overlay.SelectionRect.Width >= 2 && _overlay.SelectionRect.Height >= 2;

            if (_overlay.CurrentTool == AnnotationToolType.None)
            {
                // Selection mode - end transformation
                _transformHandler.HandleSelectionRelease(point);
            }
            else
            {
                // Annotation creation mode
                HandleAnnotationComplete(point, hasSelection);
            }

            e.Handled = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in HandlePointerReleased at {Point}", e.GetPosition(_overlay));
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handle pointer capture lost events
    /// </summary>
    public void HandlePointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        try
        {
            // End any ongoing transformation
            _transformHandler.EndTransformation();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in HandlePointerCaptureLost");
        }
    }

    /// <summary>
    /// Handle key down events
    /// </summary>
    public void HandleKeyDown(KeyEventArgs e)
    {
        try
        {
            // Handle text editing keys
            if (_editingHandler.IsEditing)
            {
                _editingHandler.HandleKeyDown(e);
                return;
            }

            // Handle tool selection hotkeys
            switch (e.Key)
            {
                case Key.A:
                    _overlay.CurrentTool = AnnotationToolType.Arrow;
                    e.Handled = true;
                    break;
                case Key.R:
                    _overlay.CurrentTool = AnnotationToolType.Rectangle;
                    e.Handled = true;
                    break;
                case Key.E:
                    _overlay.CurrentTool = AnnotationToolType.Ellipse;
                    e.Handled = true;
                    break;
                case Key.T:
                    _overlay.CurrentTool = AnnotationToolType.Text;
                    e.Handled = true;
                    break;
                case Key.F:
                    _overlay.CurrentTool = AnnotationToolType.Freehand;
                    e.Handled = true;
                    break;
                case Key.M:
                    _overlay.CurrentTool = AnnotationToolType.Mosaic;
                    e.Handled = true;
                    break;
                case Key.J:
                    _overlay.CurrentTool = AnnotationToolType.Emoji;
                    e.Handled = true;
                    break;
                case Key.S:
                    _overlay.CurrentTool = AnnotationToolType.None; // Selection mode
                    e.Handled = true;
                    break;
                case Key.C:
                    // Color picker
                    _overlay.ShowColorPicker();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    _overlay.CurrentTool = AnnotationToolType.None;
                    e.Handled = true;
                    break;
            }

            // Handle arrow key movement for selected annotations (works in any mode)
            if (_annotationService.Manager.SelectedItems.Count > 0)
            {
                var moveStep = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10.0 : 1.0; // Shift = 10px, normal = 1px
                var offset = Vector.Zero;

                switch (e.Key)
                {
                    case Key.Up:
                        offset = new Vector(0, -moveStep);
                        e.Handled = true;
                        break;
                    case Key.Down:
                        offset = new Vector(0, moveStep);
                        e.Handled = true;
                        break;
                    case Key.Left:
                        offset = new Vector(-moveStep, 0);
                        e.Handled = true;
                        break;
                    case Key.Right:
                        offset = new Vector(moveStep, 0);
                        e.Handled = true;
                        break;
                }

                if (offset != Vector.Zero)
                {
                    // Move all selected annotations
                    foreach (var item in _annotationService.Manager.SelectedItems)
                    {
                        item.Move(offset);
                    }
                    
                    // Force re-render to update selection handles
                    _overlay.RefreshRender();
                    
                    // Also trigger selection change event to update handles
                    var firstSelected = _annotationService.Manager.SelectedItems.FirstOrDefault();
                    if (firstSelected != null)
                    {
                        _renderingHandler?.HandleSelectionChanged(firstSelected, true);
                    }
                }
            }

            // Handle size tool shortcuts (Ctrl + Plus/Minus)
            if ((e.Key == Key.Add || e.Key == Key.OemPlus) && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                // Ctrl + Plus: Increase stroke width
                var currentWidth = _overlay.CurrentStyle.StrokeWidth;
                var newWidth = Math.Min(currentWidth + 1, 20); // Max 20px
                _overlay.SetStrokeWidth(newWidth);
                e.Handled = true;
            }
            else if ((e.Key == Key.Subtract || e.Key == Key.OemMinus) && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                // Ctrl + Minus: Decrease stroke width
                var currentWidth = _overlay.CurrentStyle.StrokeWidth;
                var newWidth = Math.Max(currentWidth - 1, 1); // Min 1px
                _overlay.SetStrokeWidth(newWidth);
                e.Handled = true;
            }

            // Update cursor based on tool
            UpdateCursor();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in HandleKeyDown for key {Key}", e.Key);
        }
    }

    /// <summary>
    /// Handle annotation creation start
    /// </summary>
    private void HandleAnnotationCreation(Point point)
    {
        if (_overlay.CurrentTool == AnnotationToolType.None) return;

        // Special handling for text tool - create and start editing immediately
        if (_overlay.CurrentTool == AnnotationToolType.Text)
        {
            var textItem = _annotationService.StartAnnotation(point) as TextAnnotation;
            if (textItem != null)
            {
                // Start text editing immediately
                _editingHandler.StartTextEditing(textItem);
                return;
            }
        }

        // Create new annotation item based on current tool
        var item = _annotationService.StartAnnotation(point);
        if (item != null)
        {
            _overlay.SetCreatingItem(item);
            _overlay.SetIsCreating(true);
        }
    }

    /// <summary>
    /// Handle annotation update during creation
    /// </summary>
    private void HandleAnnotationUpdate(Point point)
    {
        if (!_overlay.IsCreating || _overlay.CreatingItem == null) return;

        // Update the creating item based on current tool
        _annotationService.UpdateAnnotation(point, _overlay.CreatingItem);
        
        // Force immediate rendering to show drawing progress
        _overlay.RefreshRender();
        
        // Force immediate UI update to reduce visual latency
        Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
    }

    /// <summary>
    /// Handle annotation creation completion
    /// </summary>
    private void HandleAnnotationComplete(Point point, bool hasSelection)
    {
        if (!_overlay.IsCreating || _overlay.CreatingItem == null) return;

        // Complete the annotation
        var success = _annotationService.FinishCreate(_overlay.CreatingItem);
        
        // Clear creation state
        _overlay.SetCreatingItem(null);
        _overlay.SetIsCreating(false);
        _overlay.RefreshRender();
    }

    /// <summary>
    /// Update cursor based on current tool
    /// </summary>
    private void UpdateCursor()
    {
        if (_overlay.CurrentTool != AnnotationToolType.None)
        {
            _overlay.Cursor = new Cursor(StandardCursorType.Cross);
        }
        else
        {
            _overlay.Cursor = Cursor.Default;
        }
    }
}
