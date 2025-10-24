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
    /// Initialize the orchestrator with window, host, and session references
    /// Session is needed for element highlight coordination and selection mode sync
    /// </summary>
    void Initialize(TopLevel window, ILayerHost host, Size maskSize, IOverlaySession session, IReadOnlyList<Screen>? screens = null);

    /// <summary>
    /// Build and register all overlay layers (Mask, Selection, Annotation, Toolbar)
    /// Must be called by Session after Initialize()
    /// </summary>
    void BuildLayers();

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
    /// Callback invoked when region is selected (replaces event to avoid reverse dependency on Session)
    /// Session sets this callback to receive notifications from Orchestrator
    /// </summary>
    Action<object?, RegionSelectedEventArgs>? OnRegionSelected { get; set; }
    
    /// <summary>
    /// Callback invoked when overlay is cancelled (replaces event to avoid reverse dependency on Session)
    /// Session sets this callback to receive cancel notifications from Orchestrator
    /// </summary>
    Action<string>? OnCancelled { get; set; }
}

