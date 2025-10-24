using Avalonia;
using Avalonia.Platform;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using Serilog;
using System;
using System.Collections.Generic;

namespace AGI.Kapster.Desktop.Services.UI;

/// <summary>
/// Calculates optimal toolbar position for multi-screen scenarios
/// </summary>
public class ToolbarPositionCalculator : IToolbarPositionCalculator
{
    private const int MinToolbarInset = 50;
    private readonly IScreenCoordinateMapper _coordinateMapper;

    public ToolbarPositionCalculator(IScreenCoordinateMapper coordinateMapper)
    {
        _coordinateMapper = coordinateMapper ?? throw new ArgumentNullException(nameof(coordinateMapper));
    }

    public Point CalculatePosition(ToolbarPositionContext context)
    {
        // Get target screen bounds (based on selection's bottom-right corner)
        var screenBounds = GetScreenBoundsForSelection(context);

        // Clip selection to target screen bounds to handle cross-screen selections
        var clippedSelection = new Rect(
            Math.Max(context.Selection.Left, screenBounds.Left),
            Math.Max(context.Selection.Top, screenBounds.Top),
            Math.Min(context.Selection.Right, screenBounds.Right) - Math.Max(context.Selection.Left, screenBounds.Left),
            Math.Min(context.Selection.Bottom, screenBounds.Bottom) - Math.Max(context.Selection.Top, screenBounds.Top));

        // Calculate right-align position: toolbar right edge aligns with clipped selection right edge
        double rightAlign = clippedSelection.Right - context.ToolbarSize.Width;

        // Calculate available space for vertical positioning
        var spaceBelow = screenBounds.Bottom - clippedSelection.Bottom;
        var spaceAbove = clippedSelection.Top - screenBounds.Top;

        // Determine position with priority: below -> above -> inside
        Point position;
        if (spaceBelow >= context.ToolbarSize.Height + context.Margin)
        {
            // Priority: below selection (outside)
            position = new Point(rightAlign, clippedSelection.Bottom + context.Margin);
        }
        else if (spaceAbove >= context.ToolbarSize.Height + context.Margin)
        {
            // Fallback: above selection (outside)
            position = new Point(rightAlign, clippedSelection.Top - context.ToolbarSize.Height - context.Margin);
        }
        else
        {
            // Final fallback: inside selection at bottom edge, right-aligned
            position = new Point(
                clippedSelection.Right - context.ToolbarSize.Width,
                clippedSelection.Bottom - context.ToolbarSize.Height - context.Margin);
        }

        // Smart clamp to screen bounds: preserve right-alignment priority
        double finalX;
        if (position.X < screenBounds.Left)
        {
            // Toolbar would exceed left screen boundary, align to left edge
            finalX = screenBounds.Left;
        }
        else if (position.X + context.ToolbarSize.Width > screenBounds.Right)
        {
            // Toolbar would exceed right screen boundary, adjust to fit
            finalX = Math.Max(screenBounds.Left, screenBounds.Right - context.ToolbarSize.Width);
        }
        else
        {
            // Toolbar fits within screen, keep right-aligned position
            finalX = position.X;
        }

        // Y coordinate: clamp to keep toolbar within screen vertical bounds
        var maxY = Math.Max(screenBounds.Top, screenBounds.Bottom - context.ToolbarSize.Height);
        var finalY = Math.Clamp(position.Y, screenBounds.Top, maxY);

        return new Point(finalX, finalY);
    }

    private Rect GetScreenBoundsForSelection(ToolbarPositionContext context)
    {
        if (context.Screens == null || context.Screens.Count == 0)
        {
            // Fallback if no screen info available
            return new Rect(0, 0, context.Selection.Right + 100, context.Selection.Bottom + 100);
        }

        // Use selection's bottom-right corner to determine target screen
        var globalBottomRight = new PixelPoint(
            context.OverlayPosition.X + (int)context.Selection.Right - 1,
            context.OverlayPosition.Y + (int)context.Selection.Bottom - 1);

        // Find screen containing this point
        var screen = _coordinateMapper.GetScreenFromPoint(globalBottomRight, context.Screens);
        if (screen == null)
        {
            return new Rect(0, 0, context.Selection.Right + 100, context.Selection.Bottom + 100);
        }

        // Convert global screen bounds to overlay local coordinates
        var globalBounds = screen.Bounds;
        return new Rect(
            globalBounds.X - context.OverlayPosition.X,
            globalBounds.Y - context.OverlayPosition.Y,
            globalBounds.Width,
            globalBounds.Height);
    }
}

