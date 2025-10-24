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
    /// </summary>
    protected override Task CreateAndConfigureWindowsAsync(
        IOverlaySession session, 
        IReadOnlyList<Screen> screens,
        IEnumerable<Rect> targetRegions)
    {
        var virtualBounds = targetRegions.First(); // Single region for Windows

        // Create and configure window using builder (unified setup)
        // Events automatically forwarded to session
        var window = session.CreateWindowBuilder()
            .WithBounds(virtualBounds)
            .WithScreens(screens)
            .Build();

        // Asynchronously load background in parallel (non-blocking)
        var primaryScreen = screens.FirstOrDefault();
        if (primaryScreen != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var background = await PrecaptureBackgroundAsync(virtualBounds, primaryScreen);
                    if (background != null)
                    {
                        // Update background on UI thread
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            window.SetPrecapturedAvaloniaBitmap(background);
                            Log.Debug("[{Platform}] Background loaded and set", PlatformName);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[{Platform}] Failed to load background asynchronously", PlatformName);
                }
            });
        }

        return Task.CompletedTask;
    }
}

