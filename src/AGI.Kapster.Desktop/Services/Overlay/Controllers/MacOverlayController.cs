using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.Clipboard;
using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Services.Export.Imaging;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using AGI.Kapster.Desktop.Services.Overlay.State;
using Avalonia.Controls;
using Avalonia.Platform;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Overlay.Controllers;

/// <summary>
/// macOS-specific overlay controller: per-screen overlay windows (handles Retina scaling)
/// </summary>
[SupportedOSPlatform("macos")]
public class MacOverlayController : IOverlayController
{
    private readonly IScreenCaptureStrategy? _captureStrategy;
    private readonly IScreenCoordinateMapper _coordinateMapper;
    private readonly IOverlayWindowFactory _windowFactory;
    
    private readonly List<OverlayWindow> _windows = new();

    public MacOverlayController(
        IScreenCaptureStrategy? captureStrategy,
        IScreenCoordinateMapper coordinateMapper,
        IOverlayWindowFactory windowFactory)
    {
        _captureStrategy = captureStrategy;
        _coordinateMapper = coordinateMapper;
        _windowFactory = windowFactory;
    }

    public bool IsActive => _windows.Count > 0;

    public async Task ShowAll()
    {
        try
        {
            Log.Information("[macOS] Showing per-screen overlay windows");
            CloseAll();

            var screens = _coordinateMapper.GetAllScreens();
            if (screens.Count == 0)
            {
                Log.Warning("[macOS] No screens available");
                return;
            }

            Log.Information("[macOS] Found {Count} screen(s)", screens.Count);

            // Create one overlay window per screen
            foreach (var screen in screens)
            {
                await CreateWindowForScreen(screen);
            }

            Log.Information("[macOS] All overlay windows shown successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[macOS] Failed to show overlay windows");
            CloseAll();
            throw;
        }
    }

    private async Task CreateWindowForScreen(Screen screen)
    {
        try
        {
            var screenBounds = screen.Bounds;
            Log.Debug("[macOS] Creating overlay for screen: {Bounds}, Scale: {Scale}", 
                screenBounds, screen.Scaling);

            // Pre-capture background for this screen
            var skBitmap = await PrecaptureScreenBackgroundAsync(screen);
            var avaloniaBitmap = skBitmap != null ? BitmapConverter.ConvertToAvaloniaBitmapFast(skBitmap) : null;
            
            if (avaloniaBitmap == null && skBitmap != null)
            {
                // Fallback to slow path if fast conversion failed
                avaloniaBitmap = BitmapConverter.ConvertToAvaloniaBitmap(skBitmap);
            }

            // Create window via factory (with DI-injected dependencies)
            var window = _windowFactory.Create();
            window.Position = new Avalonia.PixelPoint(screenBounds.X, screenBounds.Y);
            window.Width = screenBounds.Width;
            window.Height = screenBounds.Height;

            // Set mask size for this screen (each screen has independent coordinate system on macOS)
            // This handles Retina displays correctly (logical vs physical pixels)
            window.SetMaskSize(screenBounds.Width, screenBounds.Height);

            // Set pre-captured background before Show()
            if (avaloniaBitmap != null)
            {
                window.SetPrecapturedAvaloniaBitmap(avaloniaBitmap);
            }

            // Subscribe to events
            window.RegionSelected += OnRegionSelected;
            window.Cancelled += OnCancelled;
            window.Closed += OnWindowClosed;

            // Register and show
            OverlayState.RegisterWindow(window);
            window.Show();
            _windows.Add(window);

            Log.Debug("[macOS] Overlay window created for screen at {Position}", screenBounds.Position);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[macOS] Failed to create overlay window for screen");
            throw;
        }
    }

    public void CloseAll()
    {
        if (_windows.Count == 0)
            return;

        Log.Information("[macOS] Closing {Count} overlay window(s)", _windows.Count);

        foreach (var window in _windows.ToList())
        {
            try
            {
                // Unsubscribe events
                window.RegionSelected -= OnRegionSelected;
                window.Cancelled -= OnCancelled;
                window.Closed -= OnWindowClosed;

                // Unregister and close
                OverlayState.UnregisterWindow(window);
                window.Close();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[macOS] Error closing overlay window");
            }
        }

        _windows.Clear();
    }

    private async Task<SkiaSharp.SKBitmap?> PrecaptureScreenBackgroundAsync(Screen screen)
    {
        if (_captureStrategy == null)
        {
            Log.Warning("[macOS] No capture strategy available for pre-capture");
            return null;
        }

        try
        {
            var pixelBounds = _coordinateMapper.MapToPhysicalRect(new Avalonia.Rect(screen.Bounds.Position.ToPoint(1.0), screen.Bounds.Size.ToSize(1.0)), screen);
            Log.Debug("[macOS] Pre-capturing screen background: {PixelBounds}", pixelBounds);
            
            return await _captureStrategy.CaptureRegionAsync(pixelBounds);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[macOS] Failed to pre-capture screen background");
            return null;
        }
    }

    private void OnRegionSelected(object? sender, RegionSelectedEventArgs e)
    {
        try
        {
            if (e.FinalImage != null)
            {
                Log.Information("[macOS] Region selected: {Region}", e.SelectedRegion);
                // Note: Clipboard operations are handled by external services
            }

            // Close all windows after successful selection
            CloseAll();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[macOS] Error handling region selection");
        }
    }

    private void OnCancelled(object? sender, OverlayCancelledEventArgs e)
    {
        Log.Information("[macOS] Overlay cancelled: {Reason}", e.Reason);
        CloseAll();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        Log.Information("[macOS] Overlay window closed by user");
        CloseAll();
    }
}

