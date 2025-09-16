using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform;
using Avalonia.VisualTree;
using SkiaSharp;

namespace AGI.Captor.Desktop.Services.Overlay;

/// <summary>
/// Platform-specific screen capture strategy
/// </summary>
public interface IScreenCaptureStrategy
{
    /// <summary>
    /// Captures the entire screen
    /// </summary>
    Task<SKBitmap?> CaptureFullScreenAsync(Screen screen);
    
    /// <summary>
    /// Captures a specific window
    /// </summary>
    Task<SKBitmap?> CaptureWindowAsync(IntPtr windowHandle);
    
    /// <summary>
    /// Captures a specific region
    /// </summary>
    Task<SKBitmap?> CaptureRegionAsync(PixelRect region);
    
    /// <summary>
    /// Captures a region from window coordinates (with DPI scaling)
    /// </summary>
    /// <param name="windowRect">Rectangle in window coordinates</param>
    /// <param name="window">The window for coordinate conversion</param>
    Task<SKBitmap?> CaptureWindowRegionAsync(Avalonia.Rect windowRect, Avalonia.Visual window);
    
    /// <summary>
    /// Captures a UI element (if platform supports it)
    /// </summary>
    Task<SKBitmap?> CaptureElementAsync(IElementInfo element);
    
    /// <summary>
    /// Gets whether the platform supports window capture
    /// </summary>
    bool SupportsWindowCapture { get; }
    
    /// <summary>
    /// Gets whether the platform supports element capture
    /// </summary>
    bool SupportsElementCapture { get; }
    
    /// <summary>
    /// Gets whether hardware acceleration is available
    /// </summary>
    bool IsHardwareAccelerated { get; }
}

/// <summary>
/// Represents information about a UI element
/// </summary>
public interface IElementInfo
{
    /// <summary>
    /// Gets the element bounds in screen coordinates
    /// </summary>
    PixelRect Bounds { get; }
    
    /// <summary>
    /// Gets the element type (e.g., Button, TextBox, etc.)
    /// </summary>
    string ElementType { get; }
    
    /// <summary>
    /// Gets the element name or identifier
    /// </summary>
    string? Name { get; }
    
    /// <summary>
    /// Gets the window handle containing this element
    /// </summary>
    IntPtr WindowHandle { get; }
    
    /// <summary>
    /// Gets platform-specific element data
    /// </summary>
    object? PlatformData { get; }
}


