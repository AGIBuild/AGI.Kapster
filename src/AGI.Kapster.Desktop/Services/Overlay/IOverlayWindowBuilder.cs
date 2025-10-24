using System;
using System.Collections.Generic;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.Overlay.State;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace AGI.Kapster.Desktop.Services.Overlay;

/// <summary>
/// Builder interface for configuring IOverlayWindow instances
/// Provides fluent API for window setup
/// </summary>
public interface IOverlayWindowBuilder
{
    /// <summary>
    /// Set window bounds (position and size)
    /// </summary>
    IOverlayWindowBuilder WithBounds(Rect bounds);
    
    /// <summary>
    /// Set screens for this window
    /// </summary>
    IOverlayWindowBuilder WithScreens(IReadOnlyList<Screen> screens);
    
    /// <summary>
    /// Enable element detection mode (optional)
    /// </summary>
    IOverlayWindowBuilder EnableElementDetection(bool enable = true);
    
    /// <summary>
    /// Build and return configured window
    /// Automatically adds window to session and wires events
    /// </summary>
    IOverlayWindow Build();
}

