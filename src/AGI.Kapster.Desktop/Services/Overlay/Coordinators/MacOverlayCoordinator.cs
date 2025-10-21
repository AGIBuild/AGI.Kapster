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
/// macOS-specific overlay coordinator: per-screen overlay windows (handles Retina scaling)
/// </summary>
[SupportedOSPlatform("macos")]
public class MacOverlayCoordinator : OverlayCoordinatorBase
{
    protected override string PlatformName => "MacCoordinator";

    public MacOverlayCoordinator(
        IOverlaySessionFactory sessionFactory,
        IOverlayWindowFactory windowFactory,
        IScreenCaptureStrategy? captureStrategy,
        IScreenCoordinateMapper coordinateMapper,
        IClipboardStrategy? clipboardStrategy = null)
        : base(sessionFactory, windowFactory, coordinateMapper, captureStrategy, clipboardStrategy)
    {
    }

    /// <summary>
    /// macOS-specific: One region per screen (independent Retina handling)
    /// </summary>
    protected override IEnumerable<Rect> CalculateTargetRegions(IReadOnlyList<Screen> screens)
    {
        // Each screen gets its own region
        foreach (var screen in screens)
        {
            yield return new Rect(
                screen.Bounds.X,
                screen.Bounds.Y,
                screen.Bounds.Width,
                screen.Bounds.Height);
        }
    }

    /// <summary>
    /// macOS-specific: Create one window per screen (handles Retina displays independently)
    /// </summary>
    protected override async Task CreateAndConfigureWindowsAsync(
        IOverlaySession session, 
        IReadOnlyList<Screen> screens,
        IEnumerable<Rect> targetRegions)
    {
        if (screens.Count == 0)
        {
            Log.Warning("[{Platform}] No screens available", PlatformName);
            throw new InvalidOperationException("No screens detected");
        }

        Log.Information("[{Platform}] Creating overlay windows for {Count} screen(s)", PlatformName, screens.Count);

        // Create window for each screen/region pair
        var screenList = screens.ToList();
        var regionList = targetRegions.ToList();
        
        for (int i = 0; i < screenList.Count && i < regionList.Count; i++)
        {
            await CreateWindowForScreenAsync(session, screenList[i], regionList[i]);
        }
    }

    private Task CreateWindowForScreenAsync(IOverlaySession session, Screen screen, Rect screenBounds)
    {
        try
        {
            Log.Debug("[{Platform}] Creating window for screen at {Position}, size {Size}", PlatformName,
                screenBounds.Position, screenBounds.Size);

            // Create window immediately for instant display
            var window = _windowFactory.Create();
            window.Position = new Avalonia.PixelPoint((int)screenBounds.X, (int)screenBounds.Y);
            window.Width = screenBounds.Width;
            window.Height = screenBounds.Height;

            // Associate window with session
            window.SetSession(session);
            window.SetScreens(new[] { screen }); // Single screen for this window
            window.SetMaskSize(screenBounds.Width, screenBounds.Height);

            // Subscribe to events
            window.RegionSelected += OnRegionSelected;
            window.Cancelled += OnCancelled;

            // Add window to session (convert to Window)
            session.AddWindow(window.AsWindow());

            // Asynchronously load background in parallel (non-blocking)
            _ = Task.Run(async () =>
            {
                try
                {
                    var background = await PrecaptureBackgroundAsync(screenBounds, screen);
                    if (background != null)
                    {
                        // Update background on UI thread
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            window.SetPrecapturedAvaloniaBitmap(background);
                            Log.Debug("[{Platform}] Background loaded for screen at {Position}", PlatformName, screenBounds.Position);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[{Platform}] Failed to load background asynchronously for screen", PlatformName);
                }
            });

            Log.Debug("[{Platform}] Window created for screen at {Position}", PlatformName, screenBounds.Position);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Platform}] Failed to create window for screen", PlatformName);
            throw;
        }
    }
}

