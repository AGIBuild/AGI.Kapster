using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Services.Annotation;
using AGI.Kapster.Desktop.Commands;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Serilog;
using System;
using System.Linq;

namespace AGI.Kapster.Desktop.Overlays.Handlers;

/// <summary>
/// Resize handle positions for annotation transformation
/// </summary>
public enum ResizeHandle
{
    None,
    TopLeft,
    TopCenter,
    TopRight,
    MiddleRight,
    BottomRight,
    BottomCenter,
    BottomLeft,
    MiddleLeft
}

/// <summary>
/// Handles selection and transformation of annotations
/// </summary>
public class AnnotationTransformHandler
{
    private readonly NewAnnotationOverlay _overlay;
    private readonly IAnnotationService _annotationService;
    private readonly CommandManager _commandManager;

    // Selection and transformation state
    private bool _isDragging;
    private bool _isResizing;
    private Point _dragStartPoint;
    private Rect _dragStartBounds;
    private ResizeHandle _activeResizeHandle = ResizeHandle.None;

    public AnnotationTransformHandler(
        NewAnnotationOverlay overlay,
        IAnnotationService annotationService,
        CommandManager commandManager)
    {
        _overlay = overlay;
        _annotationService = annotationService;
        _commandManager = commandManager;
    }

    /// <summary>
    /// Handle pointer press in selection mode
    /// </summary>
    public void HandleSelectionPress(Point point, bool addToSelection)
    {
        // Check if clicking on a resize handle first
        var resizeHandle = HitTestResizeHandle(point);
        if (resizeHandle != ResizeHandle.None)
        {
            StartResize(resizeHandle, point);
            return;
        }

        // Check if clicking on an annotation
        var hitItem = _annotationService.HitTest(point);
        if (hitItem != null)
        {
            if (!addToSelection)
            {
                // Clear other selections
                _annotationService.Manager.ClearSelection();
            }

            // Select the item
            _annotationService.Manager.SelectItem(hitItem);
            StartDrag(point);
        }
        else
        {
            // Clicked on empty space - clear selection
            _annotationService.Manager.ClearSelection();
        }
    }

    /// <summary>
    /// Handle pointer move during selection/transformation
    /// </summary>
    public void HandleSelectionMove(Point point)
    {
        if (_isDragging)
        {
            HandleDrag(point);
        }
        else if (_isResizing)
        {
            HandleResize(point);
        }
    }

    /// <summary>
    /// Handle pointer release during selection/transformation
    /// </summary>
    public void HandleSelectionRelease(Point point)
    {
        if (_isDragging)
        {
            EndDrag();
        }
        else if (_isResizing)
        {
            EndResize();
        }
    }

    /// <summary>
    /// End any ongoing transformation
    /// </summary>
    public void EndTransformation()
    {
        if (_isDragging || _isResizing)
        {
            _isDragging = false;
            _isResizing = false;
            _activeResizeHandle = ResizeHandle.None;
        }
    }

    /// <summary>
    /// Start drag operation
    /// </summary>
    private void StartDrag(Point point)
    {
        var selectedItem = _annotationService.Manager.SelectedItems.FirstOrDefault();
        if (selectedItem == null) return;

        _isDragging = true;
        _dragStartPoint = point;
        _dragStartBounds = selectedItem.Bounds;
    }

    /// <summary>
    /// Handle drag operation
    /// </summary>
    private void HandleDrag(Point point)
    {
        if (!_isDragging) return;

        var selectedItem = _annotationService.Manager.SelectedItems.FirstOrDefault();
        if (selectedItem == null) return;

        var delta = point - _dragStartPoint;
        var newBounds = new Rect(
            _dragStartBounds.X + delta.X,
            _dragStartBounds.Y + delta.Y,
            _dragStartBounds.Width,
            _dragStartBounds.Height);

        // Update the item bounds using Move method
        var currentBounds = selectedItem.Bounds;
        var offset = newBounds.TopLeft - currentBounds.TopLeft;
        selectedItem.Move(offset);
        _overlay.RefreshRender();
    }

    /// <summary>
    /// End drag operation
    /// </summary>
    private void EndDrag()
    {
        if (!_isDragging) return;

        var selectedItem = _annotationService.Manager.SelectedItems.FirstOrDefault();
        if (selectedItem != null)
        {
            // Create command for undo/redo
            var delta = selectedItem.Bounds.TopLeft - _dragStartBounds.TopLeft;
            // TODO: Get renderer from overlay or pass it as parameter
            // For now, skip command creation
            // var command = new MoveAnnotationCommand(renderer, _annotationService.Manager, selectedItem, delta, _overlay);
            // _commandManager.ExecuteCommand(command);
        }

        _isDragging = false;
    }

    /// <summary>
    /// Start resize operation
    /// </summary>
    private void StartResize(ResizeHandle handle, Point point)
    {
        var selectedItem = _annotationService.Manager.SelectedItems.FirstOrDefault();
        if (selectedItem == null) return;

        _isResizing = true;
        _activeResizeHandle = handle;
        _dragStartPoint = point;
        _dragStartBounds = selectedItem.Bounds;
    }

    /// <summary>
    /// Handle resize operation
    /// </summary>
    private void HandleResize(Point point)
    {
        if (!_isResizing) return;

        var selectedItem = _annotationService.Manager.SelectedItems.FirstOrDefault();
        if (selectedItem == null) return;

        var newBounds = CalculateResizeBounds(_dragStartBounds, point, _activeResizeHandle);
        
        // Calculate scale factors
        var scaleX = newBounds.Width / _dragStartBounds.Width;
        var scaleY = newBounds.Height / _dragStartBounds.Height;
        var scale = Math.Min(scaleX, scaleY); // Use uniform scaling
        
        // Calculate the center point for scaling
        var center = _dragStartBounds.Center;
        
        // Apply scaling
        selectedItem.Scale(scale, center);
        
        // Apply translation to match the new position
        var offset = newBounds.TopLeft - _dragStartBounds.TopLeft;
        selectedItem.Move(offset);
        
        // Force immediate rendering to show resize progress
        _overlay.RefreshRender();
    }

    /// <summary>
    /// End resize operation
    /// </summary>
    private void EndResize()
    {
        if (!_isResizing) return;

        var selectedItem = _annotationService.Manager.SelectedItems.FirstOrDefault();
        if (selectedItem != null)
        {
            // For now, just update the item directly
            // TODO: Implement proper resize command
            // IAnnotationItem doesn't have Position/Width/Height properties
            // The bounds will be updated through the Move method or other appropriate methods
        }

        _isResizing = false;
        _activeResizeHandle = ResizeHandle.None;
    }

    /// <summary>
    /// Hit test for resize handles
    /// </summary>
    private ResizeHandle HitTestResizeHandle(Point point)
    {
        var selectedItem = _annotationService.Manager.SelectedItems.FirstOrDefault();
        if (selectedItem == null) return ResizeHandle.None;

        var bounds = selectedItem.Bounds;
        var handleSize = 8.0; // Handle size in pixels
        var halfHandle = handleSize / 2;

        // Check each handle position
        if (IsPointInHandle(point, new Point(bounds.Left - halfHandle, bounds.Top - halfHandle), handleSize))
            return ResizeHandle.TopLeft;
        if (IsPointInHandle(point, new Point(bounds.Left + bounds.Width / 2 - halfHandle, bounds.Top - halfHandle), handleSize))
            return ResizeHandle.TopCenter;
        if (IsPointInHandle(point, new Point(bounds.Right - halfHandle, bounds.Top - halfHandle), handleSize))
            return ResizeHandle.TopRight;
        if (IsPointInHandle(point, new Point(bounds.Right - halfHandle, bounds.Top + bounds.Height / 2 - halfHandle), handleSize))
            return ResizeHandle.MiddleRight;
        if (IsPointInHandle(point, new Point(bounds.Right - halfHandle, bounds.Bottom - halfHandle), handleSize))
            return ResizeHandle.BottomRight;
        if (IsPointInHandle(point, new Point(bounds.Left + bounds.Width / 2 - halfHandle, bounds.Bottom - halfHandle), handleSize))
            return ResizeHandle.BottomCenter;
        if (IsPointInHandle(point, new Point(bounds.Left - halfHandle, bounds.Bottom - halfHandle), handleSize))
            return ResizeHandle.BottomLeft;
        if (IsPointInHandle(point, new Point(bounds.Left - halfHandle, bounds.Top + bounds.Height / 2 - halfHandle), handleSize))
            return ResizeHandle.MiddleLeft;

        return ResizeHandle.None;
    }

    /// <summary>
    /// Check if point is within a resize handle
    /// </summary>
    private bool IsPointInHandle(Point point, Point handleCenter, double handleSize)
    {
        var halfSize = handleSize / 2;
        return point.X >= handleCenter.X - halfSize && point.X <= handleCenter.X + halfSize &&
               point.Y >= handleCenter.Y - halfSize && point.Y <= handleCenter.Y + halfSize;
    }


    /// <summary>
    /// Calculate new bounds during resize
    /// </summary>
    private Rect CalculateResizeBounds(Rect originalBounds, Point currentPoint, ResizeHandle handle)
    {
        var delta = currentPoint - _dragStartPoint;
        
        return handle switch
        {
            ResizeHandle.TopLeft => new Rect(
                originalBounds.X + delta.X,
                originalBounds.Y + delta.Y,
                originalBounds.Width - delta.X,
                originalBounds.Height - delta.Y),
            ResizeHandle.TopCenter => new Rect(
                originalBounds.X,
                originalBounds.Y + delta.Y,
                originalBounds.Width,
                originalBounds.Height - delta.Y),
            ResizeHandle.TopRight => new Rect(
                originalBounds.X,
                originalBounds.Y + delta.Y,
                originalBounds.Width + delta.X,
                originalBounds.Height - delta.Y),
            ResizeHandle.MiddleRight => new Rect(
                originalBounds.X,
                originalBounds.Y,
                originalBounds.Width + delta.X,
                originalBounds.Height),
            ResizeHandle.BottomRight => new Rect(
                originalBounds.X,
                originalBounds.Y,
                originalBounds.Width + delta.X,
                originalBounds.Height + delta.Y),
            ResizeHandle.BottomCenter => new Rect(
                originalBounds.X,
                originalBounds.Y,
                originalBounds.Width,
                originalBounds.Height + delta.Y),
            ResizeHandle.BottomLeft => new Rect(
                originalBounds.X + delta.X,
                originalBounds.Y,
                originalBounds.Width - delta.X,
                originalBounds.Height + delta.Y),
            ResizeHandle.MiddleLeft => new Rect(
                originalBounds.X + delta.X,
                originalBounds.Y,
                originalBounds.Width - delta.X,
                originalBounds.Height),
            _ => originalBounds
        };
    }
}
