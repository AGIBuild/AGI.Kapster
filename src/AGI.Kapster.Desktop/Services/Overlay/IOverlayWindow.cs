using System;
using Avalonia;
using Avalonia.Platform;
using SkiaSharp;

namespace AGI.Kapster.Desktop.Services.Overlay;

/// <summary>
/// Represents a platform-specific overlay window
/// </summary>
public interface IOverlayWindow : IDisposable
{
    /// <summary>
    /// Shows the overlay window
    /// </summary>
    void Show();

    /// <summary>
    /// Closes the overlay window
    /// </summary>
    void Close();

    /// <summary>
    /// Sets the window to full screen mode on the specified screen
    /// </summary>
    void SetFullScreen(Screen screen);

    /// <summary>
    /// Sets the window to cover a specific region
    /// </summary>
    void SetRegion(PixelRect region);

    /// <summary>
    /// Gets whether the window is currently visible
    /// </summary>
    bool IsVisible { get; }

    /// <summary>
    /// Gets or sets whether element detection is enabled
    /// </summary>
    bool ElementDetectionEnabled { get; set; }

    /// <summary>
    /// Gets the screen this overlay is displayed on
    /// </summary>
    Screen? Screen { get; }

    /// <summary>
    /// Raised when a capture region is selected
    /// </summary>
    event EventHandler<CaptureRegionEventArgs>? RegionSelected;

    /// <summary>
    /// Raised when the capture is cancelled
    /// </summary>
    event EventHandler? Cancelled;

    /// <summary>
    /// Raised when the window is closed
    /// </summary>
    event EventHandler? Closed;
}

/// <summary>
/// Event arguments for region selection
/// </summary>
public class CaptureRegionEventArgs : EventArgs
{
    public PixelRect Region { get; }
    public CaptureMode Mode { get; }
    public object? CaptureTarget { get; } // Could be Window handle, Element info, etc.

    public CaptureRegionEventArgs(PixelRect region, CaptureMode mode, object? captureTarget = null)
    {
        Region = region;
        Mode = mode;
        CaptureTarget = captureTarget;
    }
}

/// <summary>
/// Capture mode enumeration
/// </summary>
public enum CaptureMode
{
    FullScreen,
    Window,
    Region,
    Element
}
