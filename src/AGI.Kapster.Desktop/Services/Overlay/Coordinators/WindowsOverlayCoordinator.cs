using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.Clipboard;
using AGI.Kapster.Desktop.Services.Export.Imaging;
using AGI.Kapster.Desktop.Services.Overlay.State;
using Avalonia.Media.Imaging;
using Serilog;
using SkiaSharp;

namespace AGI.Kapster.Desktop.Services.Overlay.Coordinators;

/// <summary>
/// Windows-specific overlay coordinator: single window covering virtual desktop
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsOverlayCoordinator : OverlayCoordinatorBase
{
    private readonly IScreenCaptureStrategy? _captureStrategy;
    private readonly IClipboardStrategy? _clipboardStrategy;

    protected override string PlatformName => "WindowsCoordinator";

    public WindowsOverlayCoordinator(
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
    /// Windows-specific: Create single window covering virtual desktop
    /// </summary>
    protected override Task CreateAndConfigureWindowsAsync(IOverlaySession session)
    {
        // Get virtual desktop bounds (Windows-specific: all screens combined)
        var virtualBounds = _coordinateMapper.GetVirtualDesktopBounds();
        Log.Debug("[{Platform}] Virtual desktop bounds: {Bounds}", PlatformName, virtualBounds);

        // Create window immediately for instant display
        var window = _windowFactory.Create();
        window.Position = new Avalonia.PixelPoint((int)virtualBounds.X, (int)virtualBounds.Y);
        window.Width = virtualBounds.Width;
        window.Height = virtualBounds.Height;

        // Associate window with session
        window.SetSession(session);
        window.SetMaskSize(virtualBounds.Width, virtualBounds.Height);

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
                var background = await PrecaptureBackgroundAsync(virtualBounds);
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

        return Task.CompletedTask;
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

    private async Task<Bitmap?> PrecaptureBackgroundAsync(Avalonia.Rect virtualBounds)
    {
        if (_captureStrategy == null)
        {
            Log.Warning("[{Platform}] No capture strategy available for pre-capture", PlatformName);
            return null;
        }

        try
        {
            var physicalBounds = _coordinateMapper.MapToPhysicalRect(virtualBounds);
            Log.Debug("[{Platform}] Pre-capturing virtual desktop: {PhysicalBounds}", PlatformName, physicalBounds);

            var skBitmap = await _captureStrategy.CaptureRegionAsync(physicalBounds);
            if (skBitmap == null)
            {
                Log.Warning("[{Platform}] Screen capture returned null", PlatformName);
                return null;
            }

            return BitmapConverter.ConvertToAvaloniaBitmap(skBitmap);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[{Platform}] Failed to pre-capture background", PlatformName);
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

