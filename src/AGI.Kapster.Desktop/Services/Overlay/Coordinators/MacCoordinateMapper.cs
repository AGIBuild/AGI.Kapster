using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Platform;

namespace AGI.Kapster.Desktop.Services.Overlay.Coordinators;

/// <summary>
/// macOS-specific screen coordinate mapper
/// Pure coordinate transformation with Retina display support
/// Transient lifetime: fresh instance per screenshot operation
/// </summary>
[SupportedOSPlatform("macos")]
public class MacCoordinateMapper : IScreenCoordinateMapper
{
    public PixelRect MapToPhysicalRect(Rect logicalRect, Screen screen)
    {
        var (scaleX, scaleY) = GetScaleFactor(screen);

        return new PixelRect(
            (int)(logicalRect.X * scaleX),
            (int)(logicalRect.Y * scaleY),
            (int)(logicalRect.Width * scaleX),
            (int)(logicalRect.Height * scaleY));
    }

    public Rect MapToLogicalRect(PixelRect physicalRect, Screen screen)
    {
        var (scaleX, scaleY) = GetScaleFactor(screen);

        return new Rect(
            physicalRect.X / scaleX,
            physicalRect.Y / scaleY,
            physicalRect.Width / scaleX,
            physicalRect.Height / scaleY);
    }

    public (double scaleX, double scaleY) GetScaleFactor(Screen screen)
    {
        // macOS Retina displays can have 2.0 scaling (e.g., 2880x1800 -> 1440x900 logical)
        var scaling = screen.Scaling;
        return (scaling, scaling);
    }

    public Screen? GetScreenFromPoint(PixelPoint point, IReadOnlyList<Screen> screens)
    {
        foreach (var screen in screens)
        {
            var bounds = screen.Bounds;
            if (point.X >= bounds.X && point.X < bounds.X + bounds.Width &&
                point.Y >= bounds.Y && point.Y < bounds.Y + bounds.Height)
            {
                return screen;
            }
        }

        // Return first screen as fallback
        return screens.FirstOrDefault();
    }
}


