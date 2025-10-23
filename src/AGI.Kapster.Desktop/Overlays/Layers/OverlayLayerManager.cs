using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using Serilog;
using AGI.Kapster.Desktop.Overlays.Events;

namespace AGI.Kapster.Desktop.Overlays.Layers;

/// <summary>
/// Default implementation of layer manager
/// Manages layer lifecycle, visibility, and event routing
/// </summary>
public class OverlayLayerManager : IOverlayLayerManager
{
    private readonly Dictionary<string, IOverlayLayer> _layers = new();
    private readonly IOverlayEventBus _eventBus;
    private string? _activeLayerId;
    private OverlayMode _currentMode = OverlayMode.FreeSelection;

    public OverlayMode CurrentMode => _currentMode;

    public OverlayLayerManager(IOverlayEventBus eventBus)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public void RegisterLayer(string layerId, IOverlayLayer layer)
    {
        if (string.IsNullOrEmpty(layerId))
            throw new ArgumentException("Layer ID cannot be null or empty", nameof(layerId));
        
        if (layer == null)
            throw new ArgumentNullException(nameof(layer));

        if (_layers.ContainsKey(layerId))
        {
            Log.Warning("Layer {LayerId} is already registered, replacing", layerId);
        }

        _layers[layerId] = layer;
        Log.Debug("Layer registered: {LayerId}, ZIndex={ZIndex}", layerId, layer.ZIndex);
    }

    public void UnregisterLayer(string layerId)
    {
        if (_layers.Remove(layerId))
        {
            Log.Debug("Layer unregistered: {LayerId}", layerId);
            
            if (_activeLayerId == layerId)
            {
                _activeLayerId = null;
            }
        }
    }

    public IOverlayLayer? GetLayer(string layerId)
    {
        _layers.TryGetValue(layerId, out var layer);
        return layer;
    }

    public void ShowLayer(string layerId)
    {
        if (_layers.TryGetValue(layerId, out var layer))
        {
            layer.IsVisible = true;
            Log.Debug("Layer shown: {LayerId}", layerId);
        }
    }

    public void HideLayer(string layerId)
    {
        if (_layers.TryGetValue(layerId, out var layer))
        {
            layer.IsVisible = false;
            Log.Debug("Layer hidden: {LayerId}", layerId);
        }
    }

    public void SetLayerZIndex(string layerId, int zIndex)
    {
        if (_layers.TryGetValue(layerId, out var layer))
        {
            layer.ZIndex = zIndex;
            Log.Debug("Layer Z-index updated: {LayerId} = {ZIndex}", layerId, zIndex);
        }
    }

    public void SetActiveLayer(string layerId)
    {
        if (!_layers.ContainsKey(layerId))
        {
            Log.Warning("Cannot set active layer: {LayerId} not found", layerId);
            return;
        }

        if (_activeLayerId == layerId)
            return;

        // Deactivate old layer
        if (_activeLayerId != null && _layers.TryGetValue(_activeLayerId, out var oldLayer))
        {
            oldLayer.OnDeactivate();
            _eventBus.Publish(new LayerDeactivatedEvent(_activeLayerId));
        }

        // Activate new layer
        _activeLayerId = layerId;
        if (_layers.TryGetValue(_activeLayerId, out var newLayer))
        {
            newLayer.OnActivate();
            _eventBus.Publish(new LayerActivatedEvent(_activeLayerId));
        }

        Log.Debug("Active layer changed to: {LayerId}", layerId);
    }

    public IOverlayLayer? GetActiveLayer()
    {
        if (_activeLayerId != null && _layers.TryGetValue(_activeLayerId, out var layer))
        {
            return layer;
        }
        return null;
    }

    public bool RoutePointerEvent(PointerEventArgs e)
    {
        // Get layers sorted by Z-index (descending - top to bottom)
        var sortedLayers = _layers.Values
            .Where(l => l.IsVisible && l.IsInteractive)
            .OrderByDescending(l => l.ZIndex)
            .ToList();

        // Route to active layer first
        if (_activeLayerId != null && _layers.TryGetValue(_activeLayerId, out var activeLayer))
        {
            if (activeLayer.IsVisible && activeLayer.IsInteractive)
            {
                if (activeLayer.HandlePointerEvent(e))
                {
                    return true; // Event handled
                }
            }
        }

        // Then route to other layers from top to bottom
        foreach (var layer in sortedLayers)
        {
            if (layer.LayerId == _activeLayerId)
                continue; // Already processed

            if (layer.HandlePointerEvent(e))
            {
                return true; // Event handled
            }
        }

        return false; // Event not handled
    }

    public bool RouteKeyEvent(KeyEventArgs e)
    {
        // Route to active layer first
        if (_activeLayerId != null && _layers.TryGetValue(_activeLayerId, out var activeLayer))
        {
            if (activeLayer.IsVisible && activeLayer.IsInteractive)
            {
                if (activeLayer.HandleKeyEvent(e))
                {
                    return true; // Event handled
                }
            }
        }

        // Then route to other visible/interactive layers
        var sortedLayers = _layers.Values
            .Where(l => l.IsVisible && l.IsInteractive && l.LayerId != _activeLayerId)
            .OrderByDescending(l => l.ZIndex)
            .ToList();

        foreach (var layer in sortedLayers)
        {
            if (layer.HandleKeyEvent(e))
            {
                return true; // Event handled
            }
        }

        return false; // Event not handled
    }

    public void SwitchMode(OverlayMode mode)
    {
        if (_currentMode == mode)
            return;

        var oldMode = _currentMode;
        _currentMode = mode;

        // Activate/deactivate layers based on mode
        foreach (var layer in _layers.Values)
        {
            if (layer.CanHandle(mode))
            {
                if (!layer.IsVisible)
                {
                    layer.IsVisible = true;
                    layer.OnActivate();
                }
            }
            else
            {
                if (layer.IsVisible)
                {
                    layer.OnDeactivate();
                    layer.IsVisible = false;
                }
            }
        }

        // Publish mode changed event
        _eventBus.Publish(new ModeChangedEvent(oldMode, mode));

        Log.Debug("Overlay mode switched: {OldMode} -> {NewMode}", oldMode, mode);
    }
}

