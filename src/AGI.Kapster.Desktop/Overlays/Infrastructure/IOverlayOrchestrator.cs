using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AGI.Kapster.Desktop.Services.Overlay.State;

namespace AGI.Kapster.Desktop.Overlays.Infrastructure;

/// <summary>
/// Orchestrator facade for OverlayWindow - manages layers, input routing, context updates, and event coordination.
/// Reduces OverlayWindow dependencies to minimum by centralizing overlay subsystems.
/// </summary>
public interface IOverlayOrchestrator : IDisposable
{
    /// <summary>
    /// Initialize the orchestrator with window and host references
    /// </summary>
    void Initialize(TopLevel window, ILayerHost host, Size maskSize);

    /// <summary>
    /// Build and register all overlay layers (Mask, Selection, Annotation, Toolbar)
    /// </summary>
    void BuildLayers();

    /// <summary>
    /// Set the overlay session for coordination state (must be called before BuildLayers)
    /// Enables session-scoped element highlighting and selection mode management
    /// </summary>
    void SetSession(IOverlaySession session);

    /// <summary>
    /// Update overlay context when window size/position/screens change
    /// </summary>
    void PublishContextChanged(Size overlaySize, PixelPoint overlayPosition, IReadOnlyList<Screen>? screens);

    /// <summary>
    /// Route keyboard input through layers and global hotkey handlers
    /// </summary>
    bool RouteKeyEvent(KeyEventArgs e);

    /// <summary>
    /// Route pointer input through layers
    /// </summary>
    bool RoutePointerEvent(PointerEventArgs e);

    /// <summary>
    /// Set precaptured background for annotation rendering
    /// </summary>
    void SetFrozenBackground(Bitmap? background);

    /// <summary>
    /// Set screens for context
    /// </summary>
    void SetScreens(IReadOnlyList<Screen>? screens);

    /// <summary>
    /// Get full screen screenshot for color sampling (delegates to image capture service)
    /// </summary>
    Task<Bitmap?> GetFullScreenScreenshotAsync(Size bounds);

    /// <summary>
    /// Enable IME for text editing (called by annotation layer)
    /// </summary>
    void EnableImeForTextEditing();

    /// <summary>
    /// Disable IME after text editing (called by annotation layer)
    /// </summary>
    void DisableImeAfterTextEditing();

    /// <summary>
    /// Expose RegionSelected event for backward compatibility with window consumers
    /// </summary>
    event EventHandler<RegionSelectedEventArgs>? RegionSelected;
}

