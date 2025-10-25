using System;
using System.Collections.Generic;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Overlays.Events;
using AGI.Kapster.Desktop.Services.Annotation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Serilog;

namespace AGI.Kapster.Desktop.Overlays.Layers;

/// <summary>
/// AnnotationLayer implementation - wraps NewAnnotationOverlay
/// Plan A: Now self-owns NewAnnotationOverlay visual
/// </summary>
public class AnnotationLayer : IAnnotationLayer, IOverlayVisual
{
    private readonly NewAnnotationOverlay _overlay;
    private readonly IOverlayEventBus _eventBus;
    private readonly IOverlayLayerManager _layerManager;
    
    private ILayerHost? _host;
    private IOverlayContext? _context;
    
    public string LayerId => LayerIds.Annotation;
    public int ZIndex { get; set; } = 20;
    public bool IsVisible 
    { 
        get => _overlay.IsVisible; 
        set => _overlay.IsVisible = value; 
    }
    
    public bool IsInteractive 
    { 
        get => _overlay.IsHitTestVisible; 
        set => _overlay.IsHitTestVisible = value; 
    }
    
    /// <summary>
    /// Check if text editing is currently active
    /// </summary>
    public bool IsTextEditing => _overlay.IsTextEditing;
    
    public AnnotationLayer(Services.Settings.ISettingsService settingsService, IOverlayEventBus eventBus, IOverlayLayerManager layerManager)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _layerManager = layerManager ?? throw new ArgumentNullException(nameof(layerManager));
        
        // Create own NewAnnotationOverlay visual
        _overlay = new NewAnnotationOverlay(settingsService)
        {
            Name = "Annotator",
            EventBus = eventBus,  // Set EventBus for IME events
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };
        
        // Subscribe to overlay events and forward to event bus
        _overlay.StyleChanged += OnStyleChanged;
        _overlay.ExportRequested += OnExportRequested;
        _overlay.ColorPickerRequested += OnColorPickerRequested;
        _overlay.ConfirmRequested += OnConfirmRequested;
        
        // Subscribe to LayerManager selection changes
        _layerManager.SelectionChanged += OnSelectionChanged;
        
        Log.Debug("AnnotationLayer created");
    }
    
    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (_layerManager.HasValidSelection)
        {
            var selection = _layerManager.CurrentSelection;
            SetSelectionRect(selection);
            Log.Debug("AnnotationLayer: Selection rect updated: {Selection}", selection);
        }
    }
    
    /// <summary>
    /// Internal accessor for overlay - used by ToolbarLayer for now
    /// TODO: Remove this once toolbar is fully event-driven
    /// </summary>
    internal NewAnnotationOverlay GetOverlay() => _overlay;
    
    public void OnActivate()
    {
        IsVisible = true;

        if (_layerManager.HasValidSelection)
        {
            var selection = _layerManager.CurrentSelection;
            SetSelectionRect(selection);
            IsInteractive = true;
            _overlay.Focus();
            Log.Debug("AnnotationLayer activated with selection: {Selection}", selection);
        }
        else
        {
            IsInteractive = false;
            Log.Debug("AnnotationLayer activated but no valid selection");
        }
    }
    
    public void OnDeactivate()
    {
        IsInteractive = false;
        Log.Debug("AnnotationLayer deactivated");
    }
    
    public bool HandlePointerEvent(PointerEventArgs e)
    {
        // Avalonia routes events directly to the overlay control
        // No need to manually route events here
        return false;
    }
    
    public bool HandleKeyEvent(KeyEventArgs e)
    {
        // Overlay handles keyboard shortcuts internally
        // No need to manually route events here
        return false;
    }
    
    public bool CanHandle(OverlayMode mode)
    {
        return mode == OverlayMode.Annotation;
    }
    
    // === IAnnotationLayer Implementation ===
    
    public void SetSelectionRect(Rect rect)
    {
        _overlay.SelectionRect = rect;
        IsInteractive = SelectionValidator.IsValid(rect);
        Log.Debug("AnnotationLayer selection rect set: {Rect}, IsInteractive={IsInteractive}", rect, IsInteractive);
    }
    
    public IEnumerable<IAnnotationItem> GetAnnotations()
    {
        return _overlay.GetAnnotations();
    }
    
    public void ClearAnnotations()
    {
        _overlay.ClearAnnotations();
        Log.Debug("AnnotationLayer cleared all annotations");
    }
    
    public bool DeleteSelected()
    {
        try
        {
            _overlay.DeleteSelected();
            _overlay.InvalidateVisual();
            Log.Debug("AnnotationLayer deleted selected annotations");
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "AnnotationLayer.DeleteSelected failed (no selection or not supported)");
            return false;
        }
    }
    
    public void SetTool(AnnotationToolType tool)
    {
        var oldTool = _overlay.CurrentTool;
        _overlay.CurrentTool = tool;
        
        // Publish tool change event
        _eventBus.Publish(new ToolChangedEvent(oldTool, tool));
        
        Log.Debug("AnnotationLayer tool changed: {OldTool} -> {NewTool}", oldTool, tool);
    }
    
    public void SetStyle(IAnnotationStyle style)
    {
        _overlay.CurrentStyle = style ?? throw new ArgumentNullException(nameof(style));
        Log.Debug("AnnotationLayer style set");
    }
    
    public void EndTextEditing()
    {
        _overlay.EndTextEditing();
        Log.Debug("AnnotationLayer text editing ended");
    }
    
    public bool Undo()
    {
        var commandManager = _overlay.CommandManager;
        if (commandManager.CanUndo)
        {
            commandManager.Undo();
            _overlay.InvalidateVisual(); // Trigger re-render
            Log.Debug("AnnotationLayer undo: {Description}", commandManager.UndoDescription);
            return true;
        }
        return false;
    }
    
    public bool Redo()
    {
        var commandManager = _overlay.CommandManager;
        if (commandManager.CanRedo)
        {
            commandManager.Redo();
            _overlay.InvalidateVisual(); // Trigger re-render
            Log.Debug("AnnotationLayer redo: {Description}", commandManager.RedoDescription);
            return true;
        }
        return false;
    }
    
    // === Event Forwarding ===
    
    private void OnStyleChanged(object? sender, StyleChangedEventArgs e)
    {
        _eventBus.Publish(new StyleChangedEvent(e.NewStyle));
        Log.Debug("AnnotationLayer style changed event published");
    }
    
    // === High-level operations for Coordinator ===
    public void SelectAll()
    {
        _overlay.SelectAllAnnotations();
        Log.Debug("AnnotationLayer select all requested");
    }
    
    public void NudgeSelected(Vector delta)
    {
        if (_overlay.NudgeSelected(delta))
        {
            Log.Debug("AnnotationLayer nudge by {Delta}", delta);
        }
    }
    
    public async void CopySelected()
    {
        // Copy JSON of selected annotations to platform clipboard (text)
        var json = _overlay.SerializeSelectedToJson();
        if (string.IsNullOrEmpty(json))
        {
            Log.Debug("AnnotationLayer copy requested but nothing selected");
            return;
        }
        try
        {
            var topLevel = TopLevel.GetTopLevel(_overlay);
            var clipboard = topLevel?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(json);
                Log.Debug("AnnotationLayer copied selected annotations to clipboard (text/json)");
            }
            else
            {
                // Fallback to internal copy
                _overlay.CopySelectedInternal();
                Log.Debug("AnnotationLayer copied to internal clipboard (no system clipboard)");
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "AnnotationLayer.CopySelected platform clipboard failed, using internal clipboard");
            _overlay.CopySelectedInternal();
        }
    }
    
    public async void Paste()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(_overlay);
            var clipboard = topLevel?.Clipboard;
            if (clipboard != null)
            {
                var text = await clipboard.GetTextAsync();
                if (!string.IsNullOrWhiteSpace(text) && _overlay.PasteFromJson(text))
                {
                    Log.Debug("AnnotationLayer pasted from system clipboard (text/json)");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "AnnotationLayer.Paste system clipboard failed, trying internal");
        }
        
        if (_overlay.PasteFromInternalClipboard())
        {
            Log.Debug("AnnotationLayer pasted from internal clipboard");
        }
    }
    
    public void DuplicateSelected()
    {
        if (_overlay.DuplicateSelectedInternal())
        {
            Log.Debug("AnnotationLayer duplicate requested");
        }
    }
    
    private void OnExportRequested()
    {
        _eventBus.Publish(new ExportRequestedEvent(_overlay.SelectionRect));
        Log.Debug("AnnotationLayer export requested event published");
    }
    
    private void OnColorPickerRequested()
    {
        _eventBus.Publish(new ColorPickerRequestedEvent());
        Log.Debug("AnnotationLayer color picker requested event published");
    }
    
    private void OnConfirmRequested(Rect region)
    {
        _eventBus.Publish(new ConfirmRequestedEvent(region));
        Log.Debug("AnnotationLayer confirm requested event published: {Region}", region);
    }
    
    // === IOverlayVisual Implementation (Plan A) ===
    
    public void AttachTo(ILayerHost host, IOverlayContext context)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        
        // Attach visual to host
        host.Attach(_overlay, this.ZIndex);
        
        // Set focus to enable keyboard shortcuts
        _overlay.Focus();
        
        Log.Debug("AnnotationLayer attached to host");
    }
    
    public void Detach()
    {
        if (_host != null)
        {
            _host.Detach(_overlay);
            _host = null;
            _context = null;
            Log.Debug("AnnotationLayer detached from host");
        }
    }
}

/// <summary>
/// Layer ID constants
/// </summary>
public static class LayerIds
{
    public const string Mask = "mask";
    public const string Selection = "selection";
    public const string Annotation = "annotation";
    public const string Toolbar = "toolbar";
}

