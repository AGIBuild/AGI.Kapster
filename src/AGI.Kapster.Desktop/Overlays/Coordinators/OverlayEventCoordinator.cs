using System;
using System.Threading.Tasks;
using Avalonia;
using Serilog;
using AGI.Kapster.Desktop.Overlays.Events;
using AGI.Kapster.Desktop.Overlays.Layers;
using AGI.Kapster.Desktop.Overlays.Infrastructure;

namespace AGI.Kapster.Desktop.Overlays.Coordinators;

/// <summary>
/// Coordinates high-level overlay events between layers
/// Decouples event handling logic from OverlayWindow UI class
/// </summary>
public class OverlayEventCoordinator : IDisposable
{
    private readonly IOverlayEventBus _eventBus;
    private readonly IOverlayLayerManager _layerManager;
    private readonly IOverlayActionHandler _actionHandler;
    private IOverlayOrchestrator? _orchestrator;
    
    // Events for external consumers (backward compatibility)
    public event EventHandler<RegionSelectedEventArgs>? RegionSelected;
    
    public OverlayEventCoordinator(
        IOverlayEventBus eventBus,
        IOverlayLayerManager layerManager,
        IOverlayActionHandler actionHandler)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _layerManager = layerManager ?? throw new ArgumentNullException(nameof(layerManager));
        _actionHandler = actionHandler ?? throw new ArgumentNullException(nameof(actionHandler));
        
        SubscribeToEvents();
    }

    /// <summary>
    /// Set orchestrator reference for IME control (called by Orchestrator after construction)
    /// </summary>
    public void SetOrchestrator(IOverlayOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }
    
    private void SubscribeToEvents()
    {
        _eventBus.Subscribe<SelectionFinishedEvent>(OnSelectionFinished);
        _eventBus.Subscribe<SelectionConfirmedEvent>(OnSelectionConfirmed); // Phase 3: Fix for double-click outside
        _eventBus.Subscribe<ExportRequestedEvent>(OnExportRequested);
        _eventBus.Subscribe<ColorPickerRequestedEvent>(OnColorPickerRequested);
        _eventBus.Subscribe<ConfirmRequestedEvent>(OnConfirmRequested);
        _eventBus.Subscribe<CancelRequestedEvent>(OnCancelRequested);
        _eventBus.Subscribe<ToolChangeRequestedEvent>(OnToolChangeRequested);
        _eventBus.Subscribe<UndoRequestedEvent>(_ => OnUndoRequested());
        _eventBus.Subscribe<RedoRequestedEvent>(_ => OnRedoRequested());
        _eventBus.Subscribe<DeleteRequestedEvent>(_ => OnDeleteRequested());
        _eventBus.Subscribe<ClearAnnotationsRequestedEvent>(_ => OnClearRequested());
        _eventBus.Subscribe<SelectAllRequestedEvent>(_ => OnSelectAllRequested());
        _eventBus.Subscribe<NudgeRequestedEvent>(OnNudgeRequested);
        _eventBus.Subscribe<CopyRequestedEvent>(_ => OnCopyRequested());
        _eventBus.Subscribe<PasteRequestedEvent>(_ => OnPasteRequested());
        _eventBus.Subscribe<DuplicateRequestedEvent>(_ => OnDuplicateRequested());
        _eventBus.Subscribe<ImeChangeRequestedEvent>(OnImeChangeRequested);
        
        Log.Debug("OverlayEventCoordinator subscribed to events");
    }
    
    /// <summary>
    /// Handle selection finished - coordinate mode switching
    /// P0 Fix: Ensure state sync before mode switch to prevent validation failure
    /// </summary>
    private void OnSelectionFinished(SelectionFinishedEvent e)
    {
        Log.Debug("🎯 Coordinator.OnSelectionFinished: Received event - Selection={Selection}, isEditable={IsEditable}", 
            e.Selection, e.IsEditableSelection);
        
        // P0 Fix Step 1: Forcefully sync selection to LayerManager BEFORE mode switch
        // This ensures HasValidSelection returns true when SwitchMode validates
        _layerManager.SetSelection(e.Selection);
        Log.Debug("🎯 Coordinator.OnSelectionFinished: Selection synced to LayerManager: {Selection}", e.Selection);
        
        // P0 Fix Step 2: Now switch mode (validation will pass)
        Log.Debug("🎯 Coordinator.OnSelectionFinished: About to switch to Annotation mode");
        _layerManager.SwitchMode(OverlayMode.Annotation);
        Log.Debug("🎯 Coordinator.OnSelectionFinished: Mode switch completed");
        
        // Raise public event for backward compatibility
        RegionSelected?.Invoke(this, new RegionSelectedEventArgs(
            e.Selection, false, null, e.IsEditableSelection));
        
        Log.Debug("🎯 Coordinator.OnSelectionFinished: COMPLETED");
    }
    
    /// <summary>
    /// Phase 3: Handle selection confirmed (double-click outside) - copy to clipboard and close
    /// </summary>
    private async void OnSelectionConfirmed(SelectionConfirmedEvent e)
    {
        Log.Debug("Coordinator: Selection confirmed for region {Region}", e.Selection);
        
        try
        {
            await _actionHandler.HandleConfirmAsync(e.Selection);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Coordinator: Selection confirmation failed");
        }
    }
    
    /// <summary>
    /// Handle export request - delegate to action handler
    /// </summary>
    private async void OnExportRequested(ExportRequestedEvent e)
    {
        Log.Debug("Coordinator: Export requested for region {Region}", e.Region);
        
        try
        {
            await _actionHandler.HandleExportAsync(e.Region);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Coordinator: Export failed");
        }
    }
    
    /// <summary>
    /// Handle color picker request - delegate to toolbar layer
    /// </summary>
    private void OnColorPickerRequested(ColorPickerRequestedEvent e)
    {
        Log.Debug("Coordinator: Color picker requested");
        
        var toolbarLayer = _layerManager.GetLayer(LayerIds.Toolbar) as IToolbarLayer;
        toolbarLayer?.ShowColorPicker();
    }
    
    /// <summary>
    /// Handle confirm request - delegate to action handler
    /// </summary>
    private async void OnConfirmRequested(ConfirmRequestedEvent e)
    {
        Log.Debug("Coordinator: Confirm requested for region {Region}", e.Region);
        
        try
        {
            await _actionHandler.HandleConfirmAsync(e.Region);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Coordinator: Confirm failed");
        }
    }

    private void OnCancelRequested(CancelRequestedEvent e)
    {
        Log.Debug("Coordinator: Cancel requested - {Reason}", e.Reason);
        _actionHandler.HandleCancel(e.Reason);
    }

    private void OnToolChangeRequested(ToolChangeRequestedEvent e)
    {
        Log.Debug("Coordinator: Tool change requested to {Tool}", e.Tool);
        var annotationLayer = _layerManager.GetLayer(LayerIds.Annotation) as IAnnotationLayer;
        annotationLayer?.SetTool(e.Tool);
    }

    private void OnUndoRequested()
    {
        var annotationLayer = _layerManager.GetLayer(LayerIds.Annotation) as IAnnotationLayer;
        if (annotationLayer?.Undo() == true)
        {
            Log.Debug("Coordinator: Undo applied");
        }
    }

    private void OnRedoRequested()
    {
        var annotationLayer = _layerManager.GetLayer(LayerIds.Annotation) as IAnnotationLayer;
        if (annotationLayer?.Redo() == true)
        {
            Log.Debug("Coordinator: Redo applied");
        }
    }

    private void OnDeleteRequested()
    {
        var annotationLayer = _layerManager.GetLayer(LayerIds.Annotation) as IAnnotationLayer;
        if (annotationLayer != null)
        {
            if (annotationLayer.DeleteSelected())
            {
                Log.Debug("Coordinator: Delete selected");
            }
            else
            {
                Log.Debug("Coordinator: Delete selected no-op (nothing selected)");
            }
        }
    }

    private void OnClearRequested()
    {
        var annotationLayer = _layerManager.GetLayer(LayerIds.Annotation) as IAnnotationLayer;
        annotationLayer?.ClearAnnotations();
        Log.Debug("Coordinator: Cleared all annotations");
    }
    
    private void OnSelectAllRequested()
    {
        var annotationLayer = _layerManager.GetLayer(LayerIds.Annotation) as IAnnotationLayer;
        annotationLayer?.SelectAll();
        Log.Debug("Coordinator: Select all");
    }
    
    private void OnNudgeRequested(NudgeRequestedEvent e)
    {
        var annotationLayer = _layerManager.GetLayer(LayerIds.Annotation) as IAnnotationLayer;
        annotationLayer?.NudgeSelected(e.Delta);
        Log.Debug("Coordinator: Nudge {Delta}", e.Delta);
    }
    
    private void OnCopyRequested()
    {
        var annotationLayer = _layerManager.GetLayer(LayerIds.Annotation) as IAnnotationLayer;
        annotationLayer?.CopySelected();
        Log.Debug("Coordinator: Copy selected");
    }
    
    private void OnPasteRequested()
    {
        var annotationLayer = _layerManager.GetLayer(LayerIds.Annotation) as IAnnotationLayer;
        annotationLayer?.Paste();
        Log.Debug("Coordinator: Paste");
    }
    
    private void OnDuplicateRequested()
    {
        var annotationLayer = _layerManager.GetLayer(LayerIds.Annotation) as IAnnotationLayer;
        annotationLayer?.DuplicateSelected();
        Log.Debug("Coordinator: Duplicate selected");
    }

    private void OnImeChangeRequested(ImeChangeRequestedEvent e)
    {
        if (_orchestrator == null)
        {
            Log.Warning("Coordinator: IME change requested but orchestrator not set");
            return;
        }

        if (e.Enabled)
        {
            _orchestrator.EnableImeForTextEditing();
        }
        else
        {
            _orchestrator.DisableImeAfterTextEditing();
        }
        
        Log.Debug("Coordinator: IME change requested - Enabled={Enabled}", e.Enabled);
    }
    
    public void Dispose()
    {
        // EventBus subscriptions are automatically cleaned up
        // This method is for future cleanup needs
    }
}

