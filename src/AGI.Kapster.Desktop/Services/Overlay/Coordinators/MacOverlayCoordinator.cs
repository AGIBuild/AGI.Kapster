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
    private readonly IScreenCaptureStrategy? _captureStrategy;
    private readonly IClipboardStrategy? _clipboardStrategy;

    protected override string PlatformName => "MacCoordinator";

    public MacOverlayCoordinator(
        IOverlaySessionFactory sessionFactory,
        IOverlayWindowFactory windowFactory,
        IScreenCaptureStrategy? captureStrategy,
        IScreenCoordinateMapper coordinateMapper,
        IClipboardStrategy? clipboardStrategy = null)
        : base(sessionFactory, windowFactory, coordinateMapper)
    {
        _captureStrategy = captureStrategy;
        _clipboardStrategy = clipboardStrategy;
    }

    /// <summary>
    /// macOS-specific: Get screens using temporary window
    /// </summary>
    protected override async Task<IReadOnlyList<Screen>> GetScreensAsync()
    {
        return await GetScreensUsingTempWindowAsync();
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

    public override void CloseCurrentSession()
    {
        if (_currentSession == null)
            return;

        try
        {
            Log.Information("[{Platform}] Closing current session", PlatformName);

            // Unsubscribe from all windows
            foreach (var window in _currentSession.Windows)
            {
                if (window is OverlayWindow overlayWindow)
                {
                    overlayWindow.RegionSelected -= OnRegionSelected;
                    overlayWindow.Cancelled -= OnCancelled;
                }
            }

            // Dispose session (will close all windows automatically)
            _currentSession.Dispose();
            _currentSession = null;

            Log.Debug("[{Platform}] Session closed", PlatformName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Platform}] Error closing session", PlatformName);
            _currentSession = null;
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

            // Add window to session
            session.AddWindow(window);

            // Asynchronously load background in parallel (non-blocking)
            _ = Task.Run(async () =>
            {
                try
                {
                    var background = await PrecaptureScreenBackgroundAsync(screenBounds, screen);
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

    private async Task<Bitmap?> PrecaptureScreenBackgroundAsync(Rect screenBounds, Screen screen)
    {
        if (_captureStrategy == null)
        {
            Log.Warning("[{Platform}] No capture strategy available for pre-capture", PlatformName);
            return null;
        }

        try
        {
            var pixelBounds = _coordinateMapper.MapToPhysicalRect(screenBounds, screen);
            
            Log.Debug("[{Platform}] Pre-capturing screen background: {PixelBounds}", PlatformName, pixelBounds);

            var skBitmap = await _captureStrategy.CaptureRegionAsync(pixelBounds);
            if (skBitmap == null)
            {
                Log.Warning("[{Platform}] Screen capture returned null", PlatformName);
                return null;
            }

            return BitmapConverter.ConvertToAvaloniaBitmap(skBitmap);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[{Platform}] Failed to pre-capture screen background", PlatformName);
            return null;
        }
    }

    private async void OnRegionSelected(object? sender, RegionSelectedEventArgs e)
    {
        try
        {
            // If this is an editable selection (for annotation), don't close the session
            if (e.IsEditableSelection)
            {
                Log.Debug("[{Platform}] Editable selection created, keeping session open for annotation", PlatformName);
                return;
            }

            // Only process final image (from double-click or export)
            if (e.FinalImage != null)
            {
                Log.Information("[{Platform}] Region selected: {Region}, copying to clipboard", PlatformName, e.SelectedRegion);

                // Convert Avalonia Bitmap to SKBitmap
                var skBitmap = BitmapConverter.ConvertToSKBitmap(e.FinalImage);
                if (skBitmap != null)
                {
                    // Copy to clipboard
                    if (_clipboardStrategy != null)
                    {
                        var success = await _clipboardStrategy.SetImageAsync(skBitmap);
                        if (success)
                        {
                            Log.Information("[{Platform}] Image copied to clipboard successfully", PlatformName);
                        }
                        else
                        {
                            Log.Warning("[{Platform}] Failed to copy image to clipboard", PlatformName);
                        }
                    }
                    else
                    {
                        Log.Warning("[{Platform}] Clipboard strategy not available", PlatformName);
                    }

                    skBitmap.Dispose();
                }
                else
                {
                    Log.Warning("[{Platform}] Failed to convert image for clipboard", PlatformName);
                }
            }

            // Close session after final image processing
            CloseCurrentSession();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Platform}] Error handling region selection", PlatformName);
        }
    }

    private void OnCancelled(object? sender, OverlayCancelledEventArgs e)
    {
        Log.Information("[{Platform}] Screenshot cancelled", PlatformName);
        CloseCurrentSession();
    }
}

