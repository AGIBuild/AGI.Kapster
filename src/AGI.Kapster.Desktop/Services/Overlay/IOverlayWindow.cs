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

    /// <summary>
    /// Gets the overlay window instance that originated the capture event, if applicable.
    /// This is set when the capture was initiated from a specific overlay window, and can be used to
    /// distinguish between multiple overlays. Unlike <see cref="CaptureTarget"/>, which identifies the
    /// object being captured (such as a window handle or element), <c>SourceWindow</c> refers to the overlay
    /// window that triggered the capture event.
    /// </summary>
    public IOverlayWindow? SourceWindow { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CaptureRegionEventArgs"/> class with the specified region, mode, and optional capture target.
    /// </summary>
    public object? CaptureTarget { get; } // Could be Window handle, Element info, etc.

    /// <summary>
    /// Gets the overlay window instance that originated the capture event.
    /// This is set when the capture was initiated from a specific overlay window,
    /// and can be used to distinguish between multiple overlays.
    /// Unlike <see cref="CaptureTarget"/>, which represents the logical target of the capture
    /// (such as a window handle or element information), <c>SourceWindow</c> refers to the overlay
    /// window that raised the event.
    /// </summary>
    public IOverlayWindow? SourceWindow { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CaptureRegionEventArgs"/> class.
    /// </summary>
    /// <param name="region">The selected region.</param>
    /// <param name="mode">The capture mode.</param>
    /// <param name="captureTarget">The logical target of the capture (e.g., window handle, element info).</param>
    /// <param name="sourceWindow">
    /// The overlay window instance that originated the capture event.
    /// This is typically set when the capture was initiated from a specific overlay window.
    /// </param>
    public CaptureRegionEventArgs(PixelRect region, CaptureMode mode, object? captureTarget = null)
        : this(region, mode, captureTarget, null)
    {
    }

    /// <inheritdoc cref="CaptureRegionEventArgs(PixelRect, CaptureMode, object?, IOverlayWindow?)"/>
    /// <summary>
    /// Initializes a new instance of the <see cref="CaptureRegionEventArgs"/> class with the specified region, mode, capture target, and source window.
    /// </summary>
    /// <param name="region">The selected region.</param>
    /// <param name="mode">The capture mode.</param>
    /// <param name="captureTarget">The object being captured (e.g., window handle, element info), or null.</param>
    /// <param name="sourceWindow">
    /// The overlay window instance that originated the capture event, or null if not applicable.
    /// This is typically set when the capture is initiated from a specific overlay window, and is used to
    /// distinguish the source of the event from the target being captured.
    /// </param>
    public CaptureRegionEventArgs(PixelRect region, CaptureMode mode, object? captureTarget, IOverlayWindow? sourceWindow)
    {
        Region = region;
        Mode = mode;
        CaptureTarget = captureTarget;
        SourceWindow = sourceWindow;
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
