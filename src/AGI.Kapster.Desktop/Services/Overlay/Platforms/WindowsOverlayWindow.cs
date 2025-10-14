using System;
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
/// Windows-specific overlay window implementation
/// </summary>
public class WindowsOverlayWindow : IOverlayWindow
{
    private readonly IServiceProvider _serviceProvider;
    private OverlayWindow? _window;
    private Screen? _screen;
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

    public void SetFullScreen(Screen screen)
    {
        _screen = screen;

        if (_window == null)
        {
            CreateWindow();
        }

        // Position window on the target screen
        _window!.Position = new PixelPoint(screen.Bounds.Position.X, screen.Bounds.Position.Y);
        _window.WindowStartupLocation = WindowStartupLocation.Manual;

        // Use temporary anchor window for proper screen enumeration
        var anchor = new Window
        {
            ShowInTaskbar = false,
            SystemDecorations = SystemDecorations.None,
            Opacity = 0.01,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = new PixelPoint(screen.Bounds.Position.X, screen.Bounds.Position.Y),
            Width = 1,
            Height = 1
        };

        try
        {
            anchor.Show();

            // Get screens from anchor
            if (anchor.Screens != null)
            {
                _window.WindowState = WindowState.FullScreen;
            }
        }
        finally
        {
            anchor.Close();
        }

        Log.Debug("Windows overlay window set to fullscreen on screen at {X},{Y}",
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

        Log.Debug("Windows overlay window set to region {Region}", region);
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

        var captureMode = CaptureMode.Region;
        object? captureTarget = null;

        // Determine capture mode based on selection
        if (e.IsFullScreen)
        {
            captureMode = CaptureMode.FullScreen;
        }
        else if (e.DetectedElement != null)
        {
            if (e.DetectedElement.IsWindow)
            {
                captureMode = CaptureMode.Window;
                captureTarget = e.DetectedElement.WindowHandle;
            }
            else
            {
                captureMode = CaptureMode.Element;
                captureTarget = ElementInfoAdapter.FromDetectedElement(e.DetectedElement);
            }
        }

        PixelRect screenRect;
        if (_window != null)
        {
            var topLeft = _window.PointToScreen(e.Region.Position);
            var bottomRight = _window.PointToScreen(new Point(e.Region.Right, e.Region.Bottom));

            var x = (int)Math.Round((double)Math.Min(topLeft.X, bottomRight.X), MidpointRounding.AwayFromZero);
            var y = (int)Math.Round((double)Math.Min(topLeft.Y, bottomRight.Y), MidpointRounding.AwayFromZero);
            var width = (int)Math.Round((double)Math.Abs(bottomRight.X - topLeft.X), MidpointRounding.AwayFromZero);
            var height = (int)Math.Round((double)Math.Abs(bottomRight.Y - topLeft.Y), MidpointRounding.AwayFromZero);

            // Ensure width/height at least 1 to avoid invalid PixelRect
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

        var args = new CaptureRegionEventArgs(
            screenRect,
            captureMode,
            captureTarget,
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
