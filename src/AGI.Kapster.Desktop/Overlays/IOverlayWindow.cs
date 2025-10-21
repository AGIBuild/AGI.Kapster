using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Collections.Generic;

namespace AGI.Kapster.Desktop.Overlays;

/// <summary>
/// Interface for overlay window, enabling DI and testability
/// Represents a window that displays screenshot selection overlay
/// </summary>
public interface IOverlayWindow
{
    // --- Window Properties (from Avalonia.Controls.Window) ---
    
    /// <summary>
    /// Gets or sets the window position in screen coordinates
    /// </summary>
    PixelPoint Position { get; set; }
    
    /// <summary>
    /// Gets or sets the window width
    /// </summary>
    double Width { get; set; }
    
    /// <summary>
    /// Gets or sets the window height
    /// </summary>
    double Height { get; set; }
    
    /// <summary>
    /// Shows the overlay window
    /// </summary>
    void Show();
    
    /// <summary>
    /// Closes the overlay window
    /// </summary>
    void Close();
    
    // --- Overlay-Specific Configuration ---
    
    /// <summary>
    /// Gets or sets whether element detection mode is enabled
    /// When true, allows user to select specific UI elements
    /// </summary>
    bool ElementDetectionEnabled { get; set; }
    
    /// <summary>
    /// Sets the pre-captured background bitmap for instant display
    /// Should be called before Show() for optimal user experience
    /// </summary>
    /// <param name="bitmap">Pre-captured screen background</param>
    void SetPrecapturedAvaloniaBitmap(Bitmap? bitmap);
    
    /// <summary>
    /// Sets the mask size for the overlay
    /// Used to correctly cover the entire visible area in multi-monitor setups
    /// </summary>
    /// <param name="width">Mask width in logical pixels</param>
    /// <param name="height">Mask height in logical pixels</param>
    void SetMaskSize(double width, double height);
    
    /// <summary>
    /// Sets the overlay session for state management
    /// Must be called before showing the window
    /// </summary>
    /// <param name="session">The overlay session managing this window</param>
    void SetSession(AGI.Kapster.Desktop.Services.Overlay.State.IOverlaySession? session);
    
    /// <summary>
    /// Sets the screens information for coordinate mapping
    /// Used for DPI scaling and multi-monitor support
    /// </summary>
    /// <param name="screens">List of screens relevant to this overlay</param>
    void SetScreens(IReadOnlyList<Screen>? screens);
    
    // --- Events ---
    
    /// <summary>
    /// Raised when user selects a region (single-click or annotation export)
    /// </summary>
    event EventHandler<RegionSelectedEventArgs>? RegionSelected;
    
    /// <summary>
    /// Raised when user cancels the overlay (Escape key)
    /// </summary>
    event EventHandler<OverlayCancelledEventArgs>? Cancelled;
    
    // --- Window as base type (for IOverlaySession compatibility) ---
    
    /// <summary>
    /// Gets the underlying Window instance
    /// Required for IOverlaySession.AddWindow(Window) compatibility
    /// </summary>
    Window AsWindow();
}

