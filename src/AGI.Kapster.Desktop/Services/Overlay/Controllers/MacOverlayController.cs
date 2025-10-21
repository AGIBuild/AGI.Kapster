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
using SkiaSharp;

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
    private readonly IClipboardStrategy? _clipboardStrategy;
    private readonly IOverlaySessionFactory _sessionFactory;
    
    private readonly List<OverlayWindow> _windows = new();
    private IOverlaySession? _currentSession;

    public MacOverlayController(
        IScreenCaptureStrategy? captureStrategy,
        IScreenCoordinateMapper coordinateMapper,
        IOverlayWindowFactory windowFactory,
        IOverlaySessionFactory sessionFactory,
        IClipboardStrategy? clipboardStrategy = null)
    {
        _captureStrategy = captureStrategy;
        _coordinateMapper = coordinateMapper;
        _windowFactory = windowFactory;
        _sessionFactory = sessionFactory;
        _clipboardStrategy = clipboardStrategy;
    }

    public bool IsActive => _windows.Count > 0;

    public async Task ShowAll()
    {
        try
        {
            Log.Information("[macOS] Showing per-screen overlay windows");
            CloseAll();
            
            // Create new session for this screenshot operation
            _currentSession = _sessionFactory.CreateSession();
            Log.Debug("[macOS] Created new overlay session");

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
            
            // Associate window with session
            window.SetSession(_currentSession);

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

            // Register window in session (should always exist at this point)
            if (_currentSession != null)
            {
                _currentSession.RegisterWindow(window);
            }
            else
            {
                Log.Warning("[macOS] Session is null when registering window");
            }
            
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
        try
        {
            if (_windows.Count > 0)
            {
                Log.Information("[macOS] Closing {Count} overlay window(s)", _windows.Count);

                foreach (var window in _windows.ToList())
                {
                    try
                    {
                        // Unsubscribe events
                        window.RegionSelected -= OnRegionSelected;
                        window.Cancelled -= OnCancelled;
                        window.Closed -= OnWindowClosed;

                        // Unregister from session
                        _currentSession?.UnregisterWindow(window);
                        
                        window.Close();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[macOS] Error closing overlay window");
                    }
                }

                _windows.Clear();
            }
            
            // Dispose session (automatic cleanup)
            if (_currentSession != null)
            {
                _currentSession.Dispose();
                _currentSession = null;
                Log.Debug("[macOS] Session disposed");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[macOS] Error in CloseAll");
            _windows.Clear();
            _currentSession?.Dispose();
            _currentSession = null;
        }
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

    private async void OnRegionSelected(object? sender, RegionSelectedEventArgs e)
    {
        try
        {
            if (e.FinalImage != null)
            {
                Log.Information("[macOS] Region selected: {Region}, copying to clipboard", e.SelectedRegion);
                
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
                            Log.Information("[macOS] Image copied to clipboard successfully");
                        }
                        else
                        {
                            Log.Warning("[macOS] Failed to copy image to clipboard");
                        }
                    }
                    else
                    {
                        Log.Warning("[macOS] Clipboard strategy not available");
                    }
                    
                    skBitmap.Dispose();
                }
                else
                {
                    Log.Warning("[macOS] Failed to convert image for clipboard");
                }
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

