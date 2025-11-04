using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.Clipboard;
using AGI.Kapster.Desktop.Services.Export.Imaging;
using AGI.Kapster.Desktop.Services.Overlay.State;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Serilog;
using SkiaSharp;

namespace AGI.Kapster.Desktop.Services.Overlay.Coordinators;

/// <summary>
/// Windows-specific overlay coordinator: single window covering virtual desktop
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsOverlayCoordinator : OverlayCoordinatorBase
{
    protected override string PlatformName => "WindowsCoordinator";

    public WindowsOverlayCoordinator(
        IScreenMonitorService screenMonitor,
        IOverlaySessionFactory sessionFactory,
        IOverlayWindowFactory windowFactory,
        IScreenCaptureStrategy? captureStrategy,
        IScreenCoordinateMapper coordinateMapper,
        IClipboardStrategy? clipboardStrategy = null)
        : base(screenMonitor, sessionFactory, windowFactory, coordinateMapper, captureStrategy, clipboardStrategy)
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

        // Create window immediately for instant display
        var window = _windowFactory.Create();
        window.Position = new Avalonia.PixelPoint((int)virtualBounds.X, (int)virtualBounds.Y);
        window.Width = virtualBounds.Width;
        window.Height = virtualBounds.Height;

        // Windows-specific: Remove system decorations to ensure client area matches window size
        var avaloniaWindow = window.AsWindow();
        avaloniaWindow.SystemDecorations = Avalonia.Controls.SystemDecorations.None;

        // Configure window
        window.SetScreens(screens);
        window.SetMaskSize(virtualBounds.Width, virtualBounds.Height);

        // Add window to session (session will subscribe to window events automatically)
        session.AddWindow(window.AsWindow());

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

