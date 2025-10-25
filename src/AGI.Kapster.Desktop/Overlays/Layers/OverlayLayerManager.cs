using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using Avalonia;
using AGI.Kapster.Desktop.Overlays.Layers.Selection;
using AGI.Kapster.Desktop.Services.Annotation;
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
    private OverlayMode _currentMode = OverlayMode.None; // Start with None to ensure first SwitchMode() activates layers
    
    // State Management (Phase 1)
    private Rect _currentSelection = default;
    private readonly object _stateLock = new(); // Thread-safe state access
    
    public OverlayMode CurrentMode => _currentMode;
    
    // State Management Events
    public event EventHandler? SelectionChanged;
    public event EventHandler? ModeChanged;
    
    // State Management Properties
    public Rect CurrentSelection
    {
        get
        {
            lock (_stateLock)
            {
                return _currentSelection;
            }
        }
    }
    
    public bool HasValidSelection
    {
        get
        {
            lock (_stateLock)
            {
                // P3 Fix: Use unified validation logic
                return SelectionValidator.IsValid(_currentSelection);
            }
        }
    }

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
        // Only handle global semantics on KeyDown to avoid double firing on KeyUp
        if (e.RoutedEvent == InputElement.KeyDownEvent)
        {
            // ESC: cancel
            if (e.Key == Key.Escape)
            {
                _eventBus.Publish(new CancelRequestedEvent("User pressed ESC"));
                return true;
            }
            // Ctrl+S: export
            if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                if (_layers.TryGetValue(LayerIds.Selection, out var layer) && layer is ISelectionLayer selection)
                {
                    var selectionRect = selection.GetCurrentSelection();
                    if (selectionRect.HasValue)
                    {
                        _eventBus.Publish(new ExportRequestedEvent(selectionRect.Value));
                        return true;
                    }
                }
            }
            // Enter: confirm
            if (e.Key == Key.Enter)
            {
                // Use LayerManager's CurrentSelection instead of SelectionLayer
                // because in Annotation mode, selection is already in LayerManager
                if (HasValidSelection)
                {
                    _eventBus.Publish(new ConfirmRequestedEvent(CurrentSelection));
                    return true;
                }
            }

            // Ctrl+Shift+Delete: clear all annotations
            if (e.Key == Key.Delete && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                _eventBus.Publish(new ClearAnnotationsRequestedEvent());
                return true;
            }

            // CRITICAL: Check if text editing is active before handling tool hotkeys
            // If user is typing text, let the text input go through instead of switching tools
            var annotationLayer = GetLayer(LayerIds.Annotation) as IAnnotationLayer;
            if (annotationLayer?.IsTextEditing == true)
            {
                // Text editing is active, don't intercept hotkeys, let text input go through
                return false;
            }

            // Tool hotkeys (annotation): A/R/E/T/F/M without modifiers
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                !e.KeyModifiers.HasFlag(KeyModifiers.Alt) &&
                !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                AnnotationToolType? tool = e.Key switch
                {
                    Key.A => AnnotationToolType.Arrow,
                    Key.R => AnnotationToolType.Rectangle,
                    Key.E => AnnotationToolType.Ellipse,
                    Key.T => AnnotationToolType.Text,
                    Key.F => AnnotationToolType.Freehand,
                    Key.M => AnnotationToolType.Mosaic,
                    _ => null
                };
                if (tool.HasValue)
                {
                    _eventBus.Publish(new ToolChangeRequestedEvent(tool.Value));
                    return true;
                }
                // C: Color picker
                if (e.Key == Key.C)
                {
                    _eventBus.Publish(new ColorPickerRequestedEvent());
                    return true;
                }
                // Delete key: delete selection
                if (e.Key == Key.Delete)
                {
                    _eventBus.Publish(new DeleteRequestedEvent());
                    return true;
                }
            }

            // Ctrl+Z / Ctrl+Y: undo/redo
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Z)
            {
                _eventBus.Publish(new UndoRequestedEvent());
                return true;
            }
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Y)
            {
                _eventBus.Publish(new RedoRequestedEvent());
                return true;
            }

            // Select All: Ctrl+A
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.A)
            {
                _eventBus.Publish(new SelectAllRequestedEvent());
                return true;
            }
            // Copy/Paste/Duplicate: Ctrl+C / Ctrl+V / Ctrl+D
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.C)
            {
                _eventBus.Publish(new CopyRequestedEvent());
                return true;
            }
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.V)
            {
                _eventBus.Publish(new PasteRequestedEvent());
                return true;
            }
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.D)
            {
                _eventBus.Publish(new DuplicateRequestedEvent());
                return true;
            }

            // Nudge (arrow keys without modifiers)
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                !e.KeyModifiers.HasFlag(KeyModifiers.Alt) &&
                !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                Vector? delta = e.Key switch
                {
                    Key.Left => new Vector(-1, 0),
                    Key.Right => new Vector(1, 0),
                    Key.Up => new Vector(0, -1),
                    Key.Down => new Vector(0, 1),
                    _ => null
                };
                if (delta.HasValue)
                {
                    _eventBus.Publish(new NudgeRequestedEvent(delta.Value));
                    return true;
                }
            }
        }

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
        {
            Log.Debug("LayerManager: Already in mode {Mode}, skipping switch", mode);
            return;
        }

        // P2 Fix: Remove centralized validation - let layers decide if they can activate
        // Each layer validates state in OnActivate() according to single responsibility principle
        // This prevents mode manager from becoming a bottleneck and coupling to layer internals

        var oldMode = _currentMode;
        _currentMode = mode;

        // Activate/deactivate layers based on mode
        
        foreach (var kvp in _layers)
        {
            var layerId = kvp.Key;
            var layer = kvp.Value;
            var canHandle = layer.CanHandle(mode);
            
            if (canHandle)
            {
                // Always call OnActivate() - let the layer handle idempotency
                // Don't rely on IsVisible alone, as it may be set during initialization
                // but OnActivate() logic (e.g., strategy activation) may not have run
                layer.IsVisible = true;
                layer.OnActivate();
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

        // Phase 1: Notify subscribers
        ModeChanged?.Invoke(this, EventArgs.Empty);
        
        // Publish mode changed event to EventBus (backward compatibility)
        _eventBus.Publish(new ModeChangedEvent(oldMode, mode));

        Log.Debug("LayerManager: Mode switched {OldMode} -> {NewMode}", oldMode, mode);
    }
    
    // === Plan A: Unified Layer Visual Management ===
    
    public void RegisterAndAttachLayer(string layerId, IOverlayLayer layer, ILayerHost host, IOverlayContext context)
    {
        if (string.IsNullOrEmpty(layerId))
            throw new ArgumentException("Layer ID cannot be null or empty", nameof(layerId));
        
        if (layer == null)
            throw new ArgumentNullException(nameof(layer));
        
        if (host == null)
            throw new ArgumentNullException(nameof(host));
        
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        
        // Register the layer
        RegisterLayer(layerId, layer);
        
        // Attach visual if layer implements IOverlayVisual
        if (layer is IOverlayVisual visual)
        {
            visual.AttachTo(host, context);
            Log.Debug("Layer {LayerId} attached to host", layerId);
        }
    }
    
    public void UnregisterAndDetachLayer(string layerId)
    {
        if (_layers.TryGetValue(layerId, out var layer))
        {
            // Detach visual if layer implements IOverlayVisual
            if (layer is IOverlayVisual visual)
            {
                visual.Detach();
                Log.Debug("Layer {LayerId} detached from host", layerId);
            }
            
            // Unregister the layer
            UnregisterLayer(layerId);
        }
    }
    
    // === State Management Methods (Phase 1) ===
    
    public void SetSelection(Rect selection)
    {
        bool changed = false;
        Rect oldSelection = default;
        
        lock (_stateLock)
        {
            if (_currentSelection != selection)
            {
                oldSelection = _currentSelection;
                _currentSelection = selection;
                changed = true;
            }
        }
        
        if (changed)
        {
            Log.Debug("LayerManager: Selection changed from {Old} to {New}", oldSelection, selection);
            
            // Notify subscribers (no data - they should pull from CurrentSelection)
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            
            // Also publish to EventBus for backward compatibility (with data)
            // TODO Phase 4: Remove Rect parameter from event
            _eventBus.Publish(new SelectionChangedEvent(selection));
        }
    }
    
    public void ClearSelection()
    {
        SetSelection(default);
    }
}

