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
public class WindowsOverlayCoordinator : IOverlayCoordinator
{
    private readonly IOverlaySessionFactory _sessionFactory;
    private readonly IOverlayWindowFactory _windowFactory;
    private readonly IScreenCaptureStrategy? _captureStrategy;
    private readonly IScreenCoordinateMapper _coordinateMapper;
    private readonly IClipboardStrategy? _clipboardStrategy;

    private IOverlaySession? _currentSession;

    public WindowsOverlayCoordinator(
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
            Log.Information("[WindowsCoordinator] Creating new screenshot session");
            
            // Close any existing session
            CloseCurrentSession();

            // 1. Create session
            var session = _sessionFactory.CreateSession();

            // 2. Get virtual desktop bounds (Windows-specific)
            var virtualBounds = _coordinateMapper.GetVirtualDesktopBounds();
            Log.Debug("[WindowsCoordinator] Virtual desktop bounds: {Bounds}", virtualBounds);

            // 3. Pre-capture background for instant display
            var background = await PrecaptureBackgroundAsync(virtualBounds);

            // 4. Create window
            var window = _windowFactory.Create();
            window.Position = new Avalonia.PixelPoint((int)virtualBounds.X, (int)virtualBounds.Y);
            window.Width = virtualBounds.Width;
            window.Height = virtualBounds.Height;

            // 5. Associate window with session
            window.SetSession(session);
            window.SetMaskSize(virtualBounds.Width, virtualBounds.Height);

            if (background != null)
            {
                window.SetPrecapturedAvaloniaBitmap(background);
            }

            // 6. Subscribe to events
            window.RegionSelected += OnRegionSelected;
            window.Cancelled += OnCancelled;

            // 7. Add window to session
            session.AddWindow(window);

            // 8. Show all windows in session
            session.ShowAll();

            _currentSession = session;
            Log.Information("[WindowsCoordinator] Session created and shown successfully");

            return session;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WindowsCoordinator] Failed to create session");
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
            Log.Information("[WindowsCoordinator] Closing current session");

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

            Log.Debug("[WindowsCoordinator] Session closed");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WindowsCoordinator] Error closing session");
            _currentSession = null;
        }
    }

    private async Task<Bitmap?> PrecaptureBackgroundAsync(Avalonia.Rect virtualBounds)
    {
        if (_captureStrategy == null)
        {
            Log.Warning("[WindowsCoordinator] No capture strategy available for pre-capture");
            return null;
        }

        try
        {
            var physicalBounds = _coordinateMapper.MapToPhysicalRect(virtualBounds);
            Log.Debug("[WindowsCoordinator] Pre-capturing virtual desktop: {PhysicalBounds}", physicalBounds);

            var skBitmap = await _captureStrategy.CaptureRegionAsync(physicalBounds);
            if (skBitmap == null)
            {
                Log.Warning("[WindowsCoordinator] Screen capture returned null");
                return null;
            }

            return BitmapConverter.ConvertToAvaloniaBitmap(skBitmap);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[WindowsCoordinator] Failed to pre-capture background");
            return null;
        }
    }

    private async void OnRegionSelected(object? sender, RegionSelectedEventArgs e)
    {
        try
        {
            if (e.FinalImage != null)
            {
                Log.Information("[WindowsCoordinator] Region selected: {Region}, copying to clipboard", e.SelectedRegion);

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
                            Log.Information("[WindowsCoordinator] Image copied to clipboard successfully");
                        }
                        else
                        {
                            Log.Warning("[WindowsCoordinator] Failed to copy image to clipboard");
                        }
                    }
                    else
                    {
                        Log.Warning("[WindowsCoordinator] Clipboard strategy not available");
                    }

                    skBitmap.Dispose();
                }
                else
                {
                    Log.Warning("[WindowsCoordinator] Failed to convert image for clipboard");
                }
            }

            // Close session after successful selection
            CloseCurrentSession();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WindowsCoordinator] Error handling region selection");
        }
    }

    private void OnCancelled(object? sender, OverlayCancelledEventArgs e)
    {
        Log.Information("[WindowsCoordinator] Screenshot cancelled");
        CloseCurrentSession();
    }
}

