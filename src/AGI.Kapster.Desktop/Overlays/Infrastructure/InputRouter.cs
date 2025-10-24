using System;
using Avalonia.Input;
using AGI.Kapster.Desktop.Overlays.Layers;
using Serilog;

namespace AGI.Kapster.Desktop.Overlays.Infrastructure;

/// <summary>
/// Routes input events (Key/Pointer) through layer manager and global handlers
/// Decouples OverlayWindow from direct input processing logic
/// </summary>
public class InputRouter
{
    private readonly IOverlayLayerManager _layerManager;

    public InputRouter(IOverlayLayerManager layerManager)
    {
        _layerManager = layerManager ?? throw new ArgumentNullException(nameof(layerManager));
        Log.Debug("InputRouter created");
    }

    /// <summary>
    /// Route keyboard event through layer manager
    /// </summary>
    public bool RouteKeyEvent(KeyEventArgs e)
    {
        return _layerManager.RouteKeyEvent(e);
    }

    /// <summary>
    /// Route pointer event through layer manager
    /// </summary>
    public bool RoutePointerEvent(PointerEventArgs e)
    {
        return _layerManager.RoutePointerEvent(e);
    }
}

