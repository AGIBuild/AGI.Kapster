using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
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
    private Screen? _screen;
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

    public Screen? Screen => _screen;

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

    public void SetFullScreen(Screen screen)
    {
        _screen = screen;

        if (_window == null)
        {
            CreateWindow();
        }

        // Position window directly on the target screen using screen bounds
        _window!.Position = new PixelPoint(screen.Bounds.Position.X, screen.Bounds.Position.Y);
        _window.WindowStartupLocation = WindowStartupLocation.Manual;
        _window.Width = screen.Bounds.Width;
        _window.Height = screen.Bounds.Height;
        _window.WindowState = WindowState.Normal; // Use Normal state instead of FullScreen for better multi-screen support

        Log.Debug("macOS overlay window set to fullscreen on screen at {X},{Y}",
            screen.Bounds.Position.X, screen.Bounds.Position.Y);
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

        // Pass composite image (if available) through CaptureTarget
        // This is used for macOS to include annotations in the screenshot
        object? captureTarget = null;
        if (e.CompositeImage != null)
        {
            captureTarget = e.CompositeImage;
        }
        else if (e.DetectedElement != null)
        {
            captureTarget = ElementInfoAdapter.FromDetectedElement(e.DetectedElement);
        }

        PixelRect screenRect;
        if (_window != null)
        {
            var topLeft = _window.PointToScreen(new Point(e.Region.X, e.Region.Y));
            var bottomRight = _window.PointToScreen(new Point(e.Region.Right, e.Region.Bottom));

            var x = (int)Math.Round((double)Math.Min(topLeft.X, bottomRight.X), MidpointRounding.AwayFromZero);
            var y = (int)Math.Round((double)Math.Min(topLeft.Y, bottomRight.Y), MidpointRounding.AwayFromZero);
            var width = (int)Math.Round((double)Math.Abs(bottomRight.X - topLeft.X), MidpointRounding.AwayFromZero);
            var height = (int)Math.Round((double)Math.Abs(bottomRight.Y - topLeft.Y), MidpointRounding.AwayFromZero);

            width = Math.Max(1, width);
            height = Math.Max(1, height);
            screenRect = new PixelRect(x, y, width, height);
        }
        else
        {
            screenRect = new PixelRect(
                (int)Math.Round((double)e.Region.X, MidpointRounding.AwayFromZero),
                (int)Math.Round((double)e.Region.Y, MidpointRounding.AwayFromZero),
                Math.Max(1, (int)Math.Round((double)e.Region.Width, MidpointRounding.AwayFromZero)),
                Math.Max(1, (int)Math.Round((double)e.Region.Height, MidpointRounding.AwayFromZero)));
        }

        RegionSelected?.Invoke(this, new CaptureRegionEventArgs(
            screenRect,
            e.IsFullScreen ? CaptureMode.FullScreen : CaptureMode.Region,
            captureTarget,
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
