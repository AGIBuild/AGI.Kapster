using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AGI.Kapster.Desktop.Overlays;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Services.Adapters;
using AGI.Kapster.Desktop.Services.Capture;

namespace AGI.Kapster.Desktop.Services.Overlay.Platforms;

/// <summary>
/// Windows-specific overlay window implementation
/// </summary>
public class WindowsOverlayWindow : IOverlayWindow
{
    private readonly IServiceProvider _serviceProvider;
    private OverlayWindow? _window;
    private bool _disposed;

    public WindowsOverlayWindow(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public bool IsVisible => _window?.IsVisible ?? false;

    public bool ElementDetectionEnabled
    {
        get => _window?.ElementDetectionEnabled ?? false;
        set
        {
            if (_window != null)
                _window.ElementDetectionEnabled = value;
        }
    }

    public event EventHandler<CaptureRegionEventArgs>? RegionSelected;
    public event EventHandler? Cancelled;
    public event EventHandler? Closed;

    public void Show()
    {
        if (_window == null)
        {
            CreateWindow();
        }

        _window!.Show();
        Log.Debug("Windows overlay window shown");
    }

    public void Close()
    {
        if (_window != null)
        {
            UnsubscribeEvents();
            _window.Close();
            _window = null;
            Log.Debug("Windows overlay window closed");
        }
    }

    public void SetRegion(PixelRect region)
    {
        if (_window == null)
        {
            CreateWindow();
        }

        // Position window at region origin (may be negative for secondary screens)
        _window!.Position = new PixelPoint(region.X, region.Y);
        _window.WindowStartupLocation = WindowStartupLocation.Manual;

        // Calculate DPI scaling at this position
        var dipProbe = new Point(100, 100);
        var p1 = _window.PointToScreen(new Point(0, 0));
        var p2 = _window.PointToScreen(dipProbe);
        var scaleX = Math.Max(0.1, (p2.X - p1.X) / dipProbe.X);
        var scaleY = Math.Max(0.1, (p2.Y - p1.Y) / dipProbe.Y);

        // Convert pixel size to DIPs
        var widthDip = region.Width / scaleX;
        var heightDip = region.Height / scaleY;

        _window.Width = widthDip;
        _window.Height = heightDip;
        _window.WindowState = WindowState.Normal;

        Log.Information("Windows overlay window set to region {Region}, DIPs: {W}x{H}, Scale: {SX}x{SY}", 
            region, widthDip, heightDip, scaleX, scaleY);
    }

    public void SetPrecapturedAvaloniaBitmap(Bitmap? bitmap)
    {
        if (_window == null)
        {
            CreateWindow();
        }

        _window!.SetPrecapturedAvaloniaBitmap(bitmap);
    }

    private void CreateWindow()
    {
        var elementDetector = _serviceProvider.GetService<IElementDetector>();
        var screenCaptureStrategy = _serviceProvider.GetService<IScreenCaptureStrategy>();
        _window = new OverlayWindow(elementDetector, screenCaptureStrategy)
        {
            SystemDecorations = SystemDecorations.None,
            ShowInTaskbar = false,
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.Manual
        };

        // DPI adjustment is now handled in SetRegion method

        SubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (_window == null) return;

        _window.RegionSelected += OnRegionSelected;
        _window.Cancelled += OnCancelled;
        _window.Closed += OnClosed;
    }

    private void UnsubscribeEvents()
    {
        if (_window == null) return;

        _window.RegionSelected -= OnRegionSelected;
        _window.Cancelled -= OnCancelled;
        _window.Closed -= OnClosed;
    }

    private void OnRegionSelected(object? sender, RegionSelectedEventArgs e)
    {
        // Skip if this is an editable selection (for annotations)
        if (e.IsEditableSelection)
        {
            Log.Debug("Skipping region selected event for editable selection");
            return;
        }

        // OverlayWindow always provides the final image (from frozen background)
        // No need to convert coordinates or re-capture
        if (e.CompositeImage == null)
        {
            Log.Warning("No final image provided from OverlayWindow - this should not happen");
            return;
        }

        Log.Debug("Received final image from overlay: {W}x{H}", 
            e.CompositeImage.PixelSize.Width, e.CompositeImage.PixelSize.Height);

        var args = new CaptureRegionEventArgs(
            new PixelRect(0, 0, e.CompositeImage.PixelSize.Width, e.CompositeImage.PixelSize.Height),
            CaptureMode.Region,
            e.CompositeImage, // Pass final image directly
            this);

        RegionSelected?.Invoke(this, args);
    }

    private void OnCancelled(object? sender, EventArgs e)
    {
        Cancelled?.Invoke(this, e);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed?.Invoke(this, e);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Close();
            _disposed = true;
        }
    }
}
