using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.Clipboard;
using AGI.Kapster.Desktop.Services.Export.Imaging;
using AGI.Kapster.Desktop.Services.Overlay.State;
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
public class MacOverlayCoordinator : IOverlayCoordinator
{
    private readonly IOverlaySessionFactory _sessionFactory;
    private readonly IOverlayWindowFactory _windowFactory;
    private readonly IScreenCaptureStrategy? _captureStrategy;
    private readonly IScreenCoordinateMapper _coordinateMapper;
    private readonly IClipboardStrategy? _clipboardStrategy;

    private IOverlaySession? _currentSession;

    public MacOverlayCoordinator(
        IOverlaySessionFactory sessionFactory,
        IOverlayWindowFactory windowFactory,
        IScreenCaptureStrategy? captureStrategy,
        IScreenCoordinateMapper coordinateMapper,
        IClipboardStrategy? clipboardStrategy = null)
    {
        _sessionFactory = sessionFactory;
        _windowFactory = windowFactory;
        _captureStrategy = captureStrategy;
        _coordinateMapper = coordinateMapper;
        _clipboardStrategy = clipboardStrategy;
    }

    public bool HasActiveSession => _currentSession != null;

    public async Task<IOverlaySession> CreateAndShowSessionAsync()
    {
        try
        {
            Log.Information("[MacCoordinator] Creating new screenshot session");
            
            // Close any existing session
            CloseCurrentSession();

            // 1. Create session
            var session = _sessionFactory.CreateSession();

            // 2. Get all screens (macOS-specific: one window per screen)
            var screens = _coordinateMapper.GetAllScreens();
            if (screens.Count == 0)
            {
                Log.Warning("[MacCoordinator] No screens available");
                throw new InvalidOperationException("No screens detected");
            }

            Log.Information("[MacCoordinator] Creating overlay windows for {Count} screen(s)", screens.Count);

            // 3. Create window for each screen
            foreach (var screen in screens)
            {
                await CreateWindowForScreenAsync(session, screen);
            }

            // 4. Show all windows in session
            session.ShowAll();

            _currentSession = session;
            Log.Information("[MacCoordinator] Session created with {Count} window(s)", session.Windows.Count);

            return session;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MacCoordinator] Failed to create session");
            CloseCurrentSession();
            throw;
        }
    }

    public void CloseCurrentSession()
    {
        if (_currentSession == null)
            return;

        try
        {
            Log.Information("[MacCoordinator] Closing current session");

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

            Log.Debug("[MacCoordinator] Session closed");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MacCoordinator] Error closing session");
            _currentSession = null;
        }
    }

    private Task CreateWindowForScreenAsync(IOverlaySession session, Screen screen)
    {
        try
        {
            var screenBounds = new Avalonia.Rect(
                screen.Bounds.X,
                screen.Bounds.Y,
                screen.Bounds.Width,
                screen.Bounds.Height);

            Log.Debug("[MacCoordinator] Creating window for screen at {Position}, size {Size}",
                screenBounds.Position, screenBounds.Size);

            // Create window immediately for instant display
            var window = _windowFactory.Create();
            window.Position = new Avalonia.PixelPoint((int)screenBounds.X, (int)screenBounds.Y);
            window.Width = screenBounds.Width;
            window.Height = screenBounds.Height;

            // Associate window with session
            window.SetSession(session);
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
                    var background = await PrecaptureScreenBackgroundAsync(screen);
                    if (background != null)
                    {
                        // Update background on UI thread
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            window.SetPrecapturedAvaloniaBitmap(background);
                            Log.Debug("[MacCoordinator] Background loaded for screen at {Position}", screenBounds.Position);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[MacCoordinator] Failed to load background asynchronously for screen");
                }
            });

            Log.Debug("[MacCoordinator] Window created for screen at {Position}", screenBounds.Position);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MacCoordinator] Failed to create window for screen");
            throw;
        }
    }

    private async Task<Bitmap?> PrecaptureScreenBackgroundAsync(Screen screen)
    {
        if (_captureStrategy == null)
        {
            Log.Warning("[MacCoordinator] No capture strategy available for pre-capture");
            return null;
        }

        try
        {
            var pixelBounds = _coordinateMapper.MapToPhysicalRect(
                new Avalonia.Rect(
                    screen.Bounds.Position.ToPoint(1.0),
                    screen.Bounds.Size.ToSize(1.0)),
                screen);
            
            Log.Debug("[MacCoordinator] Pre-capturing screen background: {PixelBounds}", pixelBounds);

            var skBitmap = await _captureStrategy.CaptureRegionAsync(pixelBounds);
            if (skBitmap == null)
            {
                Log.Warning("[MacCoordinator] Screen capture returned null");
                return null;
            }

            return BitmapConverter.ConvertToAvaloniaBitmap(skBitmap);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[MacCoordinator] Failed to pre-capture screen background");
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
                Log.Debug("[MacCoordinator] Editable selection created, keeping session open for annotation");
                return;
            }

            // Only process final image (from double-click or export)
            if (e.FinalImage != null)
            {
                Log.Information("[MacCoordinator] Region selected: {Region}, copying to clipboard", e.SelectedRegion);

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
                            Log.Information("[MacCoordinator] Image copied to clipboard successfully");
                        }
                        else
                        {
                            Log.Warning("[MacCoordinator] Failed to copy image to clipboard");
                        }
                    }
                    else
                    {
                        Log.Warning("[MacCoordinator] Clipboard strategy not available");
                    }

                    skBitmap.Dispose();
                }
                else
                {
                    Log.Warning("[MacCoordinator] Failed to convert image for clipboard");
                }
            }

            // Close session after final image processing
            CloseCurrentSession();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MacCoordinator] Error handling region selection");
        }
    }

    private void OnCancelled(object? sender, OverlayCancelledEventArgs e)
    {
        Log.Information("[MacCoordinator] Screenshot cancelled");
        CloseCurrentSession();
    }
}

