using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Overlay.Coordinators;

/// <summary>
/// macOS-specific screen coordinate mapper
/// Handles Retina display scaling and per-screen coordinate mapping
/// Transient lifetime: fresh instance per screenshot operation
/// </summary>
[SupportedOSPlatform("macos")]
public class MacCoordinateMapper : IScreenCoordinateMapper
{
    // Instance-level cache: valid for single screenshot operation
    private IReadOnlyList<Screen>? _screensCache;
    public PixelRect MapToPhysicalRect(Rect logicalRect, Screen? screen = null)
    {
        var targetScreen = screen ?? GetScreenFromLogicalRect(logicalRect);
        var (scaleX, scaleY) = GetScaleFactor(targetScreen);

        return new PixelRect(
            (int)(logicalRect.X * scaleX),
            (int)(logicalRect.Y * scaleY),
            (int)(logicalRect.Width * scaleX),
            (int)(logicalRect.Height * scaleY));
    }

    public Rect MapToLogicalRect(PixelRect physicalRect, Screen? screen = null)
    {
        var targetScreen = screen ?? GetScreenFromPhysicalRect(physicalRect);
        var (scaleX, scaleY) = GetScaleFactor(targetScreen);

        return new Rect(
            physicalRect.X / scaleX,
            physicalRect.Y / scaleY,
            physicalRect.Width / scaleX,
            physicalRect.Height / scaleY);
    }

    public (double scaleX, double scaleY) GetScaleFactor(Screen? screen = null)
    {
        var targetScreen = screen ?? GetPrimaryScreen();
        // macOS Retina displays can have 2.0 scaling (e.g., 2880x1800 -> 1440x900 logical)
        var scaling = targetScreen?.Scaling ?? 1.0;
        return (scaling, scaling);
    }

    public Rect GetVirtualDesktopBounds()
    {
        var screens = GetAllScreens();
        if (screens.Count == 0)
        {
            // Fallback to default
            return new Rect(0, 0, 1920, 1080);
        }

        // Calculate bounding box of all screens
        // Note: On macOS, screen coordinates may have different origins (y=0 at top or bottom)
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        foreach (var screen in screens)
        {
            var bounds = screen.Bounds;
            minX = Math.Min(minX, bounds.X);
            minY = Math.Min(minY, bounds.Y);
            maxX = Math.Max(maxX, bounds.X + bounds.Width);
            maxY = Math.Max(maxY, bounds.Y + bounds.Height);
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    public IReadOnlyList<Screen> GetAllScreens()
    {
        // Return cached screens for this screenshot operation
        if (_screensCache != null)
            return _screensCache;

        try
        {
            // Create minimal temporary window to access screen information
            var tempWindow = new Window
            {
                Width = 1,
                Height = 1,
                ShowInTaskbar = false,
                WindowState = WindowState.Minimized,
                SystemDecorations = SystemDecorations.None,
                Opacity = 0
            };

            tempWindow.Show();
            _screensCache = tempWindow.Screens?.All?.ToList() ?? (IReadOnlyList<Screen>)Array.Empty<Screen>();
            tempWindow.Close();
            
            return _screensCache;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[macOS] Failed to detect screens");
            _screensCache = Array.Empty<Screen>();
            return _screensCache;
        }
    }

    public Screen? GetScreenFromPoint(PixelPoint point)
    {
        var screens = GetAllScreens();
        foreach (var screen in screens)
        {
            var bounds = screen.Bounds;
            if (point.X >= bounds.X && point.X < bounds.X + bounds.Width &&
                point.Y >= bounds.Y && point.Y < bounds.Y + bounds.Height)
            {
                return screen;
            }
        }

        return GetPrimaryScreen();
    }

    private Screen? GetScreenFromLogicalRect(Rect logicalRect)
    {
        // Find screen containing the center of the rect
        var centerPoint = new PixelPoint(
            (int)(logicalRect.X + logicalRect.Width / 2),
            (int)(logicalRect.Y + logicalRect.Height / 2));
        return GetScreenFromPoint(centerPoint);
    }

    private Screen? GetScreenFromPhysicalRect(PixelRect physicalRect)
    {
        // Find screen containing the center of the rect
        var centerPoint = new PixelPoint(
            physicalRect.X + physicalRect.Width / 2,
            physicalRect.Y + physicalRect.Height / 2);
        return GetScreenFromPoint(centerPoint);
    }

    private Screen? GetPrimaryScreen()
    {
        try
        {
            var screens = GetAllScreens();
            return screens.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}


