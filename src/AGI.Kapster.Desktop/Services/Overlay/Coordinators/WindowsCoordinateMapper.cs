using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Platform;

namespace AGI.Kapster.Desktop.Services.Overlay.Coordinators;

/// <summary>
/// Windows-specific screen coordinate mapper
/// Pure coordinate transformation - no screen management
/// Transient lifetime: fresh instance per screenshot operation
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsCoordinateMapper : IScreenCoordinateMapper
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

