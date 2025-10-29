using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Rendering;
using AGI.Kapster.Desktop.Services.Annotation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AGI.Kapster.Desktop.Overlays.Handlers;

/// <summary>
/// Handles rendering-related logic for annotations
/// </summary>
public class AnnotationRenderingHandler
{
    private readonly NewAnnotationOverlay _overlay;
    private readonly IAnnotationService _annotationService;
    private readonly IAnnotationRenderer _renderer;

    public AnnotationRenderingHandler(
        NewAnnotationOverlay overlay,
        IAnnotationService annotationService,
        IAnnotationRenderer renderer)
    {
        _overlay = overlay;
        _annotationService = annotationService;
        _renderer = renderer;
    }

    /// <summary>
    /// Render all annotations
    /// </summary>
    public void RenderAll()
    {
        try
        {
            // Clear existing renders
            ClearAllRenders();

            // Get all annotations
            var annotations = _annotationService.Manager?.Items ?? Enumerable.Empty<IAnnotationItem>();
            
            // Render each annotation
            foreach (var item in annotations)
            {
                if (item.IsVisible)
                {
                    RenderAnnotation(item);
                }
            }

            Log.Debug("Rendered {Count} annotations", annotations.Count());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error rendering all annotations");
        }
    }

    /// <summary>
    /// Render a specific annotation
    /// </summary>
    public void RenderAnnotation(IAnnotationItem item)
    {
        try
        {
            if (!item.IsVisible) return;

            // Use the annotation renderer to render the item
            _renderer.Render(_overlay, item);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error rendering annotation {ItemId} of type {ItemType}", item.Id, item.GetType().Name);
        }
    }

    /// <summary>
    /// Clear all rendered annotations
    /// </summary>
    public void ClearAllRenders()
    {
        try
        {
            // Remove all annotation-related controls from the overlay
            var controlsToRemove = _overlay.Children
                .OfType<Control>()
                .Where(c => c.Tag?.ToString()?.StartsWith("annotation_") == true)
                .ToList();

            foreach (var control in controlsToRemove)
            {
                _overlay.Children.Remove(control);
            }

            Log.Debug("Cleared {Count} rendered annotations", controlsToRemove.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error clearing all renders");
        }
    }

    /// <summary>
    /// Clear render for a specific annotation
    /// </summary>
    public void ClearRender(IAnnotationItem item)
    {
        try
        {
            // Remove controls associated with this annotation
            var controlsToRemove = _overlay.Children
                .OfType<Control>()
                .Where(c => c.Tag?.ToString() == $"annotation_{item.Id}")
                .ToList();

            foreach (var control in controlsToRemove)
            {
                _overlay.Children.Remove(control);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error clearing render for annotation {ItemId}", item.Id);
        }
    }

    /// <summary>
    /// Update render for a specific annotation
    /// </summary>
    public void UpdateRender(IAnnotationItem item)
    {
        try
        {
            // Clear existing render
            ClearRender(item);
            
            // Render the updated item
            RenderAnnotation(item);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating render for annotation {ItemId}", item.Id);
        }
    }

    /// <summary>
    /// Render selection handles for selected annotations
    /// </summary>
    public void RenderSelectionHandles(IAnnotationItem item)
    {
        try
        {
            if (item.State != AnnotationState.Selected) return;

            var bounds = item.Bounds;
            var handleSize = 8.0;
            var halfHandle = handleSize / 2;

            // Create handles for each corner and edge
            var handles = new[]
            {
                new { Position = new Point(bounds.Left - halfHandle, bounds.Top - halfHandle), Type = "TopLeft" },
                new { Position = new Point(bounds.Left + bounds.Width / 2 - halfHandle, bounds.Top - halfHandle), Type = "TopCenter" },
                new { Position = new Point(bounds.Right - halfHandle, bounds.Top - halfHandle), Type = "TopRight" },
                new { Position = new Point(bounds.Right - halfHandle, bounds.Top + bounds.Height / 2 - halfHandle), Type = "MiddleRight" },
                new { Position = new Point(bounds.Right - halfHandle, bounds.Bottom - halfHandle), Type = "BottomRight" },
                new { Position = new Point(bounds.Left + bounds.Width / 2 - halfHandle, bounds.Bottom - halfHandle), Type = "BottomCenter" },
                new { Position = new Point(bounds.Left - halfHandle, bounds.Bottom - halfHandle), Type = "BottomLeft" },
                new { Position = new Point(bounds.Left - halfHandle, bounds.Top + bounds.Height / 2 - halfHandle), Type = "MiddleLeft" }
            };

            foreach (var handle in handles)
            {
                var handleControl = new Border
                {
                    Width = handleSize,
                    Height = handleSize,
                    Background = Brushes.White,
                    BorderBrush = Brushes.Blue,
                    BorderThickness = new Thickness(1),
                    Tag = $"annotation_{item.Id}_handle_{handle.Type}",
                    Cursor = GetHandleCursor(handle.Type)
                };

                Canvas.SetLeft(handleControl, handle.Position.X);
                Canvas.SetTop(handleControl, handle.Position.Y);
                handleControl.ZIndex = 1000; // High Z-index for handles

                _overlay.Children.Add(handleControl);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error rendering selection handles for annotation {ItemId}", item.Id);
        }
    }

    /// <summary>
    /// Get cursor for resize handle
    /// </summary>
    private Cursor GetHandleCursor(string handleType)
    {
        return handleType switch
        {
            "TopLeft" or "BottomRight" => new Cursor(StandardCursorType.TopLeftCorner),
            "TopRight" or "BottomLeft" => new Cursor(StandardCursorType.TopRightCorner),
            "TopCenter" or "BottomCenter" => new Cursor(StandardCursorType.SizeNorthSouth),
            "MiddleLeft" or "MiddleRight" => new Cursor(StandardCursorType.SizeWestEast),
            _ => Cursor.Default
        };
    }

    /// <summary>
    /// Clear selection handles for an annotation
    /// </summary>
    public void ClearSelectionHandles(IAnnotationItem item)
    {
        try
        {
            var handlesToRemove = _overlay.Children
                .OfType<Control>()
                .Where(c => c.Tag?.ToString()?.StartsWith($"annotation_{item.Id}_handle_") == true)
                .ToList();

            foreach (var handle in handlesToRemove)
            {
                _overlay.Children.Remove(handle);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error clearing selection handles for annotation {ItemId}", item.Id);
        }
    }

    /// <summary>
    /// Update Z-index for all annotations
    /// </summary>
    public void UpdateZIndex()
    {
        try
        {
            var annotations = _annotationService.Manager?.Items ?? Enumerable.Empty<IAnnotationItem>();
            
            foreach (var item in annotations)
            {
                var controls = _overlay.Children
                    .OfType<Control>()
                    .Where(c => c.Tag?.ToString() == $"annotation_{item.Id}")
                    .ToList();

                foreach (var control in controls)
                {
                    control.ZIndex = item.ZIndex;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating Z-index for annotations");
        }
    }

    /// <summary>
    /// Handle annotation visibility changes
    /// </summary>
    public void HandleVisibilityChanged(IAnnotationItem item)
    {
        try
        {
            if (item.IsVisible)
            {
                RenderAnnotation(item);
            }
            else
            {
                ClearRender(item);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling visibility change for annotation {ItemId}", item.Id);
        }
    }

    /// <summary>
    /// Handle annotation selection changes
    /// </summary>
    public void HandleSelectionChanged(IAnnotationItem item, bool isSelected)
    {
        try
        {
            if (isSelected)
            {
                RenderSelectionHandles(item);
            }
            else
            {
                ClearSelectionHandles(item);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling selection change for annotation {ItemId}", item.Id);
        }
    }
}
