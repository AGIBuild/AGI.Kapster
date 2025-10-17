using System;
using System.Linq;
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
/// macOS-specific overlay window implementation
/// </summary>
public class MacOverlayWindow : IOverlayWindow
{
    private readonly IServiceProvider _serviceProvider;
    private OverlayWindow? _window;
    private bool _disposed;
    private static OverlayWindow? _primaryWindow; // Used for screen enumeration

    public MacOverlayWindow(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public bool IsVisible => _window?.IsVisible ?? false;

    public bool ElementDetectionEnabled
    {
        get => false; // Element detection not supported on macOS yet
        set { /* No-op */ }
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

        // Store as primary window if this is the first one
        if (_primaryWindow == null)
        {
            _primaryWindow = _window;
        }

        Log.Debug("macOS overlay window shown");
    }

    public void Close()
    {
        if (_window != null)
        {
            UnsubscribeEvents();

            // Clear primary window reference if this was it
            if (_primaryWindow == _window)
            {
                _primaryWindow = null;
            }

            _window.Close();
            _window = null;
            Log.Debug("macOS overlay window closed");
        }
    }

    public void SetRegion(PixelRect region)
    {
        if (_window == null)
        {
            CreateWindow();
        }

        _window!.Position = new PixelPoint(region.X, region.Y);
        _window.Width = region.Width;
        _window.Height = region.Height;
        _window.WindowState = WindowState.Normal;

        Log.Debug("macOS overlay window set to region {Region}", region);
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
            WindowStartupLocation = WindowStartupLocation.Manual,
            // macOS-specific window settings can be added here
        };

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

        RegionSelected?.Invoke(this, new CaptureRegionEventArgs(
            new PixelRect(0, 0, e.CompositeImage.PixelSize.Width, e.CompositeImage.PixelSize.Height),
            CaptureMode.Region,
            e.CompositeImage, // Pass final image directly
            this));
    }

    private void OnCancelled(object? sender, OverlayCancelledEventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
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
