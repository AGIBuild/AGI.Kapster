using System;
using System.Collections.Generic;
using System.Linq;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.Clipboard;
using AGI.Kapster.Desktop.Services.Export.Imaging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Overlay;

/// <summary>
/// Simplified overlay manager that uses DI directly without factory pattern
/// </summary>
public class SimplifiedOverlayManager : IOverlayController
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<IOverlayWindow> _windows = new();
    private IScreenCaptureStrategy? _captureStrategy;
    private IClipboardStrategy? _clipboardStrategy;

    public SimplifiedOverlayManager(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeServices();
    }

    public bool IsActive => _windows.Count > 0;

    public void ShowAll()
    {
        try
        {
            Log.Information("Showing overlay window (single window covering all screens)");

            CloseAll(); // Clean up any existing windows

            var screens = GetAvailableScreens();
            if (screens == null || screens.Count == 0)
            {
                Log.Warning("No screens available, creating default window with fallback dimensions");
                var defaultWindow = CreateWindow();
                var defaultBounds = new PixelRect(0, 0, 1920, 1080);
                defaultWindow.SetRegion(defaultBounds);
                defaultWindow.Show();
                _windows.Add(defaultWindow);
                return;
            }

            // Calculate bounding box covering all screens (including negative coordinates)
            var virtualDesktopBounds = CalculateVirtualDesktopBounds(screens);
            Log.Information("Virtual desktop bounds: {Bounds}", virtualDesktopBounds);

            // Create a single overlay window covering the entire virtual desktop
            var overlayWindow = CreateWindow();
            overlayWindow.SetRegion(virtualDesktopBounds);
            overlayWindow.Show();
            _windows.Add(overlayWindow);

            Log.Information("Created single overlay window covering all screens");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show overlay windows");
            CloseAll();
        }
    }

    public void CloseAll()
    {
        Log.Information("Closing all overlay windows");

        foreach (var window in _windows)
        {
            try
            {
                UnsubscribeWindowEvents(window);
                window.Close();
                window.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error closing overlay window");
            }
        }

        _windows.Clear();
    }

    private void InitializeServices()
    {
        _captureStrategy = _serviceProvider.GetService<IScreenCaptureStrategy>();
        _clipboardStrategy = _serviceProvider.GetService<IClipboardStrategy>();

        if (_captureStrategy == null)
        {
            Log.Warning("No screen capture strategy available");
        }

        if (_clipboardStrategy == null)
        {
            Log.Warning("No clipboard strategy available");
        }
    }

    private IOverlayWindow CreateWindow()
    {
        var window = _serviceProvider.GetRequiredService<IOverlayWindow>();
        ConfigureWindow(window);
        return window;
    }

    private void ConfigureWindow(IOverlayWindow window)
    {
        window.RegionSelected += OnRegionSelected;
        window.Cancelled += OnCancelled;
        window.Closed += OnWindowClosed;
    }

    private void UnsubscribeWindowEvents(IOverlayWindow window)
    {
        window.RegionSelected -= OnRegionSelected;
        window.Cancelled -= OnCancelled;
        window.Closed -= OnWindowClosed;
    }

    private List<Screen>? GetAvailableScreens()
    {
        try
        {
            // Create a temporary window to get screen information
            var tempWindow = new Window
            {
                Width = 1,
                Height = 1,
                ShowInTaskbar = false,
                WindowState = WindowState.Minimized,
                SystemDecorations = SystemDecorations.None,
                Opacity = 0
            };

            tempWindow.Show();
            var screens = tempWindow.Screens?.All?.ToList();
            tempWindow.Close();

            if (screens != null)
            {
                Log.Information("Detected {Count} screen(s):", screens.Count);
                foreach (var screen in screens)
                {
                    Log.Information("  Screen: {Name}, Primary={Primary}, Bounds={Bounds}, WorkingArea={WorkingArea}, Scaling={Scaling}", 
                        screen.DisplayName ?? "(unnamed)", 
                        screen.IsPrimary,
                        screen.Bounds,
                        screen.WorkingArea,
                        screen.Scaling);
                }
            }

            return screens;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get available screens");
            return null;
        }
    }

    private PixelRect CalculateVirtualDesktopBounds(List<Screen> screens)
    {
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        foreach (var screen in screens)
        {
            var bounds = screen.Bounds;
            minX = Math.Min(minX, bounds.X);
            minY = Math.Min(minY, bounds.Y);
            maxX = Math.Max(maxX, bounds.X + bounds.Width);
            maxY = Math.Max(maxY, bounds.Y + bounds.Height);
        }

        return new PixelRect(minX, minY, maxX - minX, maxY - minY);
    }

    private async void OnRegionSelected(object? sender, CaptureRegionEventArgs e)
    {
        try
        {
            Log.Information("Region selected: {W}x{H}", e.Region.Width, e.Region.Height);

            if (_clipboardStrategy == null)
            {
                Log.Warning("Clipboard strategy not available");
                return;
            }

            // OverlayWindow always provides the final image through CaptureTarget
            if (e.CaptureTarget is not Avalonia.Media.Imaging.Bitmap finalImage)
            {
                Log.Warning("No final image provided from OverlayWindow");
                CloseAll();
                return;
            }

            Log.Debug("Using final image from OverlayWindow: {W}x{H}", 
                finalImage.PixelSize.Width, finalImage.PixelSize.Height);

            var skBitmap = BitmapConverter.ConvertToSKBitmap(finalImage);
            if (skBitmap != null)
            {
                // Copy to clipboard
                var success = await _clipboardStrategy.SetImageAsync(skBitmap);
                Log.Information("Screenshot copied to clipboard: {Success}", success);
            }
            else
            {
                Log.Warning("Failed to convert final image to SKBitmap");
            }

            CloseAll();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling region selection");
        }
    }

    private void OnCancelled(object? sender, EventArgs e)
    {
        Log.Information("Overlay cancelled");
        CloseAll();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is IOverlayWindow window && _windows.Contains(window))
        {
            _windows.Remove(window);
            UnsubscribeWindowEvents(window);

            if (_windows.Count == 0)
            {
                Log.Information("All overlay windows closed");
            }
        }
    }

}
