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
    private readonly IScreenCoordinateMapper? _coordinateMapper;

    public ToolbarPositionCalculator(IScreenCoordinateMapper? coordinateMapper = null)
    {
        _coordinateMapper = coordinateMapper;
    }

    public Point CalculatePosition(ToolbarPositionContext context)
    {
        // Get target screen bounds
        var screenBounds = GetScreenBoundsForSelection(context);

        // Calculate available space
        var spaceBelow = screenBounds.Bottom - context.Selection.Bottom;
        var spaceAbove = context.Selection.Top - screenBounds.Top;

        // Determine position with priority: below -> above -> inside
        Point position;
        double rightAlign = context.Selection.Right - context.ToolbarSize.Width;

        if (spaceBelow >= context.ToolbarSize.Height + context.Margin)
        {
            // Priority: below selection (outside)
            position = new Point(rightAlign, context.Selection.Bottom + context.Margin);
            Log.Debug("Toolbar: below selection (outside), space={Space}px", spaceBelow);
        }
        else if (spaceAbove >= context.ToolbarSize.Height + context.Margin)
        {
            // Fallback: above selection (outside)
            position = new Point(rightAlign, context.Selection.Top - context.ToolbarSize.Height - context.Margin);
            Log.Debug("Toolbar: above selection (outside), space={Space}px", spaceAbove);
        }
        else
        {
            // Final fallback: inside selection at bottom-right corner
            position = new Point(
                context.Selection.Right - context.ToolbarSize.Width - context.Margin,
                context.Selection.Bottom - context.ToolbarSize.Height - context.Margin);
            Log.Debug("Toolbar: inside selection (bottom-right)");
        }

        // Clamp to screen bounds
        var finalX = Math.Clamp(position.X, screenBounds.Left, screenBounds.Right - context.ToolbarSize.Width);
        var finalY = Math.Clamp(position.Y, screenBounds.Top, screenBounds.Bottom - context.ToolbarSize.Height);

        Log.Debug("Toolbar final position: ({X}, {Y})", finalX, finalY);
        return new Point(finalX, finalY);
    }

    private Rect GetScreenBoundsForSelection(ToolbarPositionContext context)
    {
        if (context.Screens == null || context.Screens.Count == 0 || _coordinateMapper == null)
        {
            // Fallback: return full selection as bounds
            return new Rect(0, 0, context.Selection.Right + 100, context.Selection.Bottom + 100);
        }

        // Use a point well inside the selection to avoid screen gaps
        var inset = Math.Max(MinToolbarInset, (int)context.ToolbarSize.Height);
        var globalBottomRight = new PixelPoint(
            context.OverlayPosition.X + (int)context.Selection.Right - inset,
            context.OverlayPosition.Y + (int)context.Selection.Bottom - inset);

        // Find screen containing this point
        var screen = _coordinateMapper.GetScreenFromPoint(globalBottomRight, context.Screens);
        if (screen == null)
        {
            Log.Warning("Cannot determine screen for toolbar positioning");
            return new Rect(0, 0, context.Selection.Right + 100, context.Selection.Bottom + 100);
        }

        // Convert global screen bounds to overlay local coordinates
        var globalBounds = screen.WorkingArea;
        var localBounds = new Rect(
            globalBounds.X - context.OverlayPosition.X,
            globalBounds.Y - context.OverlayPosition.Y,
            globalBounds.Width,
            globalBounds.Height);

        Log.Debug("Screen bounds (local): {Bounds}", localBounds);
        return localBounds;
    }
}

