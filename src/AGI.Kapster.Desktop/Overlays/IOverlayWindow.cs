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
    
    // --- Accessors for Session initialization (called by Session.NotifyWindowReady) ---
    
    /// <summary>
    /// Gets the LayerHost for Orchestrator initialization
    /// Called by Session when window is ready
    /// </summary>
    ILayerHost? GetLayerHost();
    
    /// <summary>
    /// Gets the mask size for Orchestrator initialization
    /// Called by Session when window is ready
    /// </summary>
    Size GetMaskSize();
    
    /// <summary>
    /// Gets this window as TopLevel for Orchestrator initialization
    /// Called by Session when window is ready
    /// </summary>
    TopLevel AsTopLevel();
    
    // --- Window as base type (for IOverlaySession compatibility) ---
    
    /// <summary>
    /// Gets the underlying Window instance
    /// Required for IOverlaySession.AddWindow(Window) compatibility
    /// </summary>
    Window AsWindow();
}

