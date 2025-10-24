using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.Clipboard;
using AGI.Kapster.Desktop.Services.Overlay;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using AGI.Kapster.Desktop.Services.Overlay.State;
using Avalonia;
using Avalonia.Platform;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Screenshot;

/// <summary>
/// Windows-specific screenshot service: single window covering virtual desktop
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsScreenshotService : ScreenshotServiceBase
{
    protected override string PlatformName => "WindowsScreenshot";

    public WindowsScreenshotService(
        IScreenMonitorService screenMonitor,
        IOverlaySessionFactory sessionFactory,
        IScreenCaptureStrategy? captureStrategy,
        IScreenCoordinateMapper coordinateMapper,
        IClipboardStrategy? clipboardStrategy = null)
        : base(screenMonitor, sessionFactory, coordinateMapper, captureStrategy, clipboardStrategy)
    {
    }

    /// <summary>
    /// Windows-specific: Single virtual desktop region (all screens combined)
    /// </summary>
    protected override IEnumerable<Rect> CalculateTargetRegions(IReadOnlyList<Screen> screens)
    {
        var virtualBounds = CalculateVirtualDesktopBounds(screens);
        Log.Debug("[{Platform}] Virtual desktop bounds: {Bounds}", PlatformName, virtualBounds);
        yield return virtualBounds;
    }

    /// <summary>
    /// Windows-specific: Create single window covering virtual desktop
    /// Session handles capture, creation, and background setting
    /// </summary>
    protected override async Task CreateAndConfigureWindowsAsync(
        IOverlaySession session, 
        IReadOnlyList<Screen> screens,
        IEnumerable<Rect> targetRegions)
    {
        var virtualBounds = targetRegions.First(); // Single region for Windows

        // Session handles the entire process: Capture → Create → Set
        await session.CreateWindowWithBackgroundAsync(virtualBounds, screens);
    }
}

