using System;
using Avalonia;

namespace AGI.Captor.Desktop.Services;

/// <summary>
/// Detected UI element information
/// </summary>
public record DetectedElement(
    Rect Bounds,
    string Name,
    string ClassName,
    string ProcessName,
    IntPtr WindowHandle,
    bool IsWindow
);

/// <summary>
/// Interface for detecting UI elements and windows at specific screen coordinates
/// </summary>
public interface IElementDetector : IDisposable
{
    /// <summary>
    /// Detects the UI element at the specified screen coordinates
    /// </summary>
    /// <param name="x">Screen X coordinate</param>
    /// <param name="y">Screen Y coordinate</param>
    /// <param name="ignoreWindow">Window handle to ignore during detection (e.g., overlay window)</param>
    /// <returns>Information about the detected element, or null if none found</returns>
    DetectedElement? DetectElementAt(int x, int y, IntPtr ignoreWindow = default);

    /// <summary>
    /// Detects the UI element at the specified screen coordinates (convenience method)
    /// </summary>
    /// <param name="x">Screen X coordinate</param>
    /// <param name="y">Screen Y coordinate</param>
    /// <returns>Information about the detected element, or null if none found</returns>
    DetectedElement? DetectElement(int x, int y) => DetectElementAt(x, y);

    /// <summary>
    /// Event fired when element detection mode changes
    /// </summary>
    event Action<bool>? DetectionModeChanged;

    /// <summary>
    /// Gets or sets whether element detection is currently active
    /// </summary>
    bool IsDetectionActive { get; set; }

    /// <summary>
    /// Toggles between window detection and element detection modes
    /// </summary>
    void ToggleDetectionMode();

    /// <summary>
    /// Gets whether currently detecting windows (true) or elements (false)
    /// </summary>
    bool IsWindowMode { get; }

    /// <summary>
    /// Gets whether element detection is supported on this platform
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Gets whether the application has the necessary permissions for element detection
    /// </summary>
    bool HasPermissions { get; }
}


