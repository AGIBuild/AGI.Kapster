using System;
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
/// Windows-specific overlay controller: single window covering virtual desktop
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsOverlayController : IOverlayController
{
    private readonly IScreenCaptureStrategy? _captureStrategy;
    private readonly IScreenCoordinateMapper _coordinateMapper;
    private readonly IOverlayWindowFactory _windowFactory;
    private readonly IClipboardStrategy? _clipboardStrategy;
    private readonly IOverlaySessionFactory _sessionFactory;
    
    private OverlayWindow? _currentWindow;
    private IOverlaySession? _currentSession;

    public WindowsOverlayController(
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

    public bool IsActive => _currentWindow != null;

    public async Task ShowAll()
    {
        try
        {
            Log.Information("[Windows] Showing single overlay window covering virtual desktop");
            CloseAll();
            
            // Create new session for this screenshot operation
            _currentSession = _sessionFactory.CreateSession();
            Log.Debug("[Windows] Created new overlay session");

            var virtualBounds = _coordinateMapper.GetVirtualDesktopBounds();
            Log.Debug("[Windows] Virtual desktop bounds: {Bounds}", virtualBounds);

            // Pre-capture background for instant display
            var skBitmap = await PrecaptureBackgroundAsync();
            var avaloniaBitmap = skBitmap != null ? BitmapConverter.ConvertToAvaloniaBitmapFast(skBitmap) : null;
            
            if (avaloniaBitmap == null && skBitmap != null)
            {
                // Fallback to slow path if fast conversion failed
                avaloniaBitmap = BitmapConverter.ConvertToAvaloniaBitmap(skBitmap);
            }

            // Create window via factory (with DI-injected dependencies)
            _currentWindow = _windowFactory.Create();
            _currentWindow.Position = new Avalonia.PixelPoint((int)virtualBounds.X, (int)virtualBounds.Y);
            _currentWindow.Width = virtualBounds.Width;
            _currentWindow.Height = virtualBounds.Height;
            
            // Associate window with session
            _currentWindow.SetSession(_currentSession);

            // Set mask size for the virtual desktop (important for multi-monitor with negative coordinates)
            // Mask always starts at (0,0) in window coordinates, but covers the full virtual desktop
            _currentWindow.SetMaskSize(virtualBounds.Width, virtualBounds.Height);

            // Set pre-captured background before Show() for instant display
            if (avaloniaBitmap != null)
            {
                _currentWindow.SetPrecapturedAvaloniaBitmap(avaloniaBitmap);
            }

            // Subscribe to events
            _currentWindow.RegionSelected += OnRegionSelected;
            _currentWindow.Cancelled += OnCancelled;
            _currentWindow.Closed += OnWindowClosed;

            // Register window in session
            _currentSession.RegisterWindow(_currentWindow);
            _currentWindow.Show();
            
            Log.Information("[Windows] Overlay window shown successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Windows] Failed to show overlay window");
            CloseAll();
            throw;
        }
    }

    public void CloseAll()
    {
        try
        {
            if (_currentWindow != null)
            {
                Log.Information("[Windows] Closing overlay window");
                
                // Unsubscribe events
                _currentWindow.RegionSelected -= OnRegionSelected;
                _currentWindow.Cancelled -= OnCancelled;
                _currentWindow.Closed -= OnWindowClosed;

                // Unregister from session
                _currentSession?.UnregisterWindow(_currentWindow);
                
                _currentWindow.Close();
                _currentWindow = null;
            }
            
            // Dispose session (automatic cleanup)
            if (_currentSession != null)
            {
                _currentSession.Dispose();
                _currentSession = null;
                Log.Debug("[Windows] Session disposed");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Windows] Error closing overlay window");
            _currentWindow = null;
            _currentSession?.Dispose();
            _currentSession = null;
        }
    }

    private async Task<SkiaSharp.SKBitmap?> PrecaptureBackgroundAsync()
    {
        if (_captureStrategy == null)
        {
            Log.Warning("[Windows] No capture strategy available for pre-capture");
            return null;
        }

        try
        {
            var virtualBounds = _coordinateMapper.GetVirtualDesktopBounds();
            var pixelBounds = _coordinateMapper.MapToPhysicalRect(virtualBounds);
            Log.Debug("[Windows] Pre-capturing background: {PixelBounds}", pixelBounds);
            
            return await _captureStrategy.CaptureRegionAsync(pixelBounds);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[Windows] Failed to pre-capture background");
            return null;
        }
    }

    private async void OnRegionSelected(object? sender, RegionSelectedEventArgs e)
    {
        try
        {
            if (e.FinalImage != null)
            {
                Log.Information("[Windows] Region selected: {Region}, copying to clipboard", e.SelectedRegion);
                
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
                            Log.Information("[Windows] Image copied to clipboard successfully");
                        }
                        else
                        {
                            Log.Warning("[Windows] Failed to copy image to clipboard");
                        }
                    }
                    else
                    {
                        Log.Warning("[Windows] Clipboard strategy not available");
                    }
                    
                    skBitmap.Dispose();
                }
                else
                {
                    Log.Warning("[Windows] Failed to convert image for clipboard");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Windows] Error handling region selection");
        }
    }

    private void OnCancelled(object? sender, OverlayCancelledEventArgs e)
    {
        Log.Information("[Windows] Overlay cancelled: {Reason}", e.Reason);
        CloseAll();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        Log.Information("[Windows] Overlay window closed by user");
        CloseAll();
    }
}

