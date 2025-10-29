using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Rendering;
using AGI.Kapster.Desktop.Services;
using AGI.Kapster.Desktop.Services.Annotation;
using AGI.Kapster.Desktop.Services.Export;
using AGI.Kapster.Desktop.Services.Settings;
using AGI.Kapster.Desktop.Commands;
using AGI.Kapster.Desktop.Overlays.Handlers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AGI.Kapster.Desktop.Overlays;

/// <summary>
/// Resize handle positions for annotation transformation
/// </summary>
public enum ResizeHandle
{
    None,
    TopLeft,
    TopCenter,
    TopRight,
    MiddleRight,
    BottomRight,
    BottomCenter,
    BottomLeft,
    MiddleLeft
}

/// <summary>
/// New annotation overlay based on data model
/// </summary>
public sealed class NewAnnotationOverlay : Canvas
{
    private readonly IAnnotationService _annotationService;
    private readonly IAnnotationRenderer _renderer;
    private readonly CommandManager _commandManager;
    private IAnnotationItem? _creatingItem;

    // Handler instances
    private AnnotationInputHandler? _inputHandler;
    private AnnotationTransformHandler? _transformHandler;
    private AnnotationEditingHandler? _editingHandler;
    private AnnotationRenderingHandler? _renderingHandler;

    /// <summary>
    /// Get all annotation items (public for use by OverlayWindow)
    /// </summary>
    public IEnumerable<IAnnotationItem> GetAnnotations()
    {
        return _annotationService?.Manager?.Items ?? Enumerable.Empty<IAnnotationItem>();
    }
    private bool _isCreating;

    // Text editing
    private TextBox? _editingTextBox;
    private TextAnnotation? _editingTextItem;

    // Selection and transformation
    private bool _isDragging;
    private bool _isResizing;
    private Point _dragStartPoint;
    private Rect _dragStartBounds;
    private ResizeHandle _activeResizeHandle = ResizeHandle.None;

    // Selection rect for hit testing
    private Rect _selectionRect;

    // Event for double-click confirm
    public event Action<Rect>? ConfirmRequested;

    // Events for export functionality
    public event Action? ExportRequested;
    
    // Event for color picker functionality
    public event Action? ColorPickerRequested;

    // Event for style changes (for toolbar updates)
    public event EventHandler<StyleChangedEventArgs>? StyleChanged;
    public Rect SelectionRect
    {
        get => _selectionRect;
        set
        {
            _selectionRect = value;
            UpdateHitTestVisibility();
        }
    }

    // Current tool
    public AnnotationToolType CurrentTool
    {
        get => _annotationService.CurrentTool;
        set => _annotationService.CurrentTool = value;
    }

    // Current style
    public IAnnotationStyle CurrentStyle
    {
        get => _annotationService.CurrentStyle;
        set => _annotationService.CurrentStyle = value;
    }

    public CommandManager CommandManager => _commandManager;

    // Properties for handlers
    public IAnnotationItem? CreatingItem => _creatingItem;
    public bool IsCreating => _isCreating;

    // Methods for handlers
    public void SetCreatingItem(IAnnotationItem? item) => _creatingItem = item;
    public void SetIsCreating(bool isCreating) => _isCreating = isCreating;
    public void RefreshRender() 
    {
        try
        {
            // Re-render all completed annotations
            foreach (var item in _annotationService.Manager.Items)
            {
                if (item.IsVisible)
                {
                    _renderer.Render(this, item);
                }
            }

            // Re-render the currently creating annotation
            if (_isCreating && _creatingItem != null && _creatingItem.IsVisible)
            {
                _renderer.Render(this, _creatingItem);
            }

            // Force immediate UI update
            InvalidateVisual();
            Dispatcher.UIThread.Post(() => 
            {
                InvalidateVisual();
            }, DispatcherPriority.Render);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in RefreshRender");
        }
    }

    public NewAnnotationOverlay() : this(null)
    {
    }

    public NewAnnotationOverlay(ISettingsService? settingsService)
    {
        _annotationService = new AnnotationService(settingsService);
        _renderer = new AnnotationRenderer();
        _commandManager = new CommandManager();

        Background = Brushes.Transparent;
        IsHitTestVisible = false; // Start as non-interactive

        // Subscribe to annotation events
        _annotationService.Manager.ItemChanged += OnItemChanged;
        _annotationService.Manager.SelectionChanged += OnSelectionChanged;
        _annotationService.ToolChanged += OnToolChanged;
        _annotationService.StyleChanged += OnStyleChanged;

        // Enable keyboard focus for shortcuts
        Focusable = true;

        // Initialize handlers
        InitializeHandlers();
    }

    /// <summary>
    /// Initialize handler instances
    /// </summary>
    private void InitializeHandlers()
    {
        _renderingHandler = new AnnotationRenderingHandler(this, _annotationService, _renderer);
        _editingHandler = new AnnotationEditingHandler(this, _annotationService, _commandManager);
        _transformHandler = new AnnotationTransformHandler(this, _annotationService, _commandManager);
        _inputHandler = new AnnotationInputHandler(this, _annotationService, _transformHandler, _editingHandler, _renderingHandler);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Note: Keyboard events are now handled by OverlayWindow.OnPreviewKeyDown
        // which calls HandleKeyDown() to avoid duplicate processing
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        try
        {
            base.OnPointerPressed(e);
            _inputHandler?.HandlePointerPressed(e);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnPointerPressed at {Point}", e.GetPosition(this));
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        try
        {
            base.OnPointerMoved(e);
            _inputHandler?.HandlePointerMoved(e);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnPointerMoved at {Point}", e.GetCurrentPoint(this).Position);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        try
        {
            base.OnPointerReleased(e);
            _inputHandler?.HandlePointerReleased(e);
            }
            catch (Exception ex)
            {
            Log.Error(ex, "Error in OnPointerReleased at {Point}", e.GetPosition(this));
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            try
            {
            base.OnPointerCaptureLost(e);
            _inputHandler?.HandlePointerCaptureLost(e);
            }
            catch (Exception ex)
            {
            Log.Error(ex, "Error in OnPointerCaptureLost");
            }
        }

    protected override void OnPointerExited(PointerEventArgs e)
        {
            try
            {
            base.OnPointerExited(e);
            }
            catch (Exception ex)
            {
            Log.Error(ex, "Error in OnPointerExited");
        }
    }

    /// <summary>
    /// Update hit test visibility
    /// </summary>
    private void UpdateHitTestVisibility()
    {
        // Always hit test visible to handle double-click outside selection
        // but only process events inside selection area or for specific cases
        IsHitTestVisible = true;
    }

    /// <summary>
    /// Handle annotation item change events
    /// </summary>
    private void OnItemChanged(object? sender, AnnotationChangedEventArgs e)
    {
        try
        {
            _renderingHandler?.UpdateRender(e.Item);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnItemChanged");
        }
    }

    /// <summary>
    /// Handle selection change events
    /// </summary>
    private void OnSelectionChanged(object? sender, Models.SelectionChangedEventArgs e)
    {
        try
        {
            // Handle selection changes for all items
            foreach (var item in e.OldSelection.Except(e.NewSelection))
            {
                _renderingHandler?.HandleSelectionChanged(item, false);
            }
            foreach (var item in e.NewSelection.Except(e.OldSelection))
            {
                _renderingHandler?.HandleSelectionChanged(item, true);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnSelectionChanged");
        }
    }

    /// <summary>
    /// Handle tool change events
    /// </summary>
    private void OnToolChanged(object? sender, ToolChangedEventArgs e)
    {
        try
        {
            // Cancel any ongoing creation when tool changes
            if (_isCreating && _creatingItem != null)
            {
                _annotationService.CancelCreate(_creatingItem);
                _creatingItem = null;
                _isCreating = false;
                RefreshRender();
            }

            // Clear annotation selection when entering drawing mode
            var hasSelection = _selectionRect.Width >= 2 && _selectionRect.Height >= 2;
            if (hasSelection && CurrentTool != AnnotationToolType.None)
            {
                _annotationService.Manager.ClearSelection();
                IsHitTestVisible = true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnToolChanged from {OldTool} to {NewTool}", e.OldTool, e.NewTool);
        }
    }

    /// <summary>
    /// Handle style change events - notify toolbar to update UI
    /// </summary>
    private void OnStyleChanged(object? sender, StyleChangedEventArgs e)
    {
        try
        {
            // Notify any listeners (like toolbar) that style has changed
            StyleChanged?.Invoke(this, e);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnStyleChanged");
        }
    }


    /// <summary>
    /// Set style properties (for compatibility with existing toolbar)
    /// </summary>
    public void SetStrokeColor(Color color)
    {
        var newStyle = CurrentStyle.Clone();
        newStyle.StrokeColor = color;
        if (newStyle.FillMode != FillMode.None)
            newStyle.FillColor = color;
        CurrentStyle = newStyle;
    }

    public void SetStrokeWidth(double width)
    {
        var newStyle = CurrentStyle.Clone();
        newStyle.StrokeWidth = width;
        CurrentStyle = newStyle;
    }

    public void SetFontSize(double size)
    {
        var newStyle = CurrentStyle.Clone();
        newStyle.FontSize = size;
        CurrentStyle = newStyle;
    }


    /// <summary>
    /// Clear all annotations
    /// </summary>
    public void Clear()
    {
        var allItems = _annotationService.Manager.Items.ToList();
        if (allItems.Count == 0) return;

        // Create a composite command to remove all annotations
        var removeCommands = allItems.Select(item =>
            new RemoveAnnotationCommand(_annotationService.Manager, _renderer, item, this)).ToList();

        var clearCommand = new CompositeCommand("Clear All Annotations", removeCommands);
        _commandManager.ExecuteCommand(clearCommand);

        RefreshRender();
        Log.Information("Cleared {Count} annotations using command pattern", allItems.Count);
    }

    /// <summary>
    /// Delete selected annotations
    /// </summary>
    public void DeleteSelected()
    {
        var selectedItems = _annotationService.Manager.SelectedItems.ToList();
        if (selectedItems.Count == 0) return;

        // Create commands for each selected item
        var removeCommands = selectedItems.Select(item =>
            new RemoveAnnotationCommand(_annotationService.Manager, _renderer, item, this)).ToList();

        if (removeCommands.Count == 1)
        {
            // Single item - execute single command
            _commandManager.ExecuteCommand(removeCommands[0]);
        }
        else
        {
            // Multiple items - use composite command
            var deleteCommand = new CompositeCommand("Delete Selected Annotations", removeCommands);
            _commandManager.ExecuteCommand(deleteCommand);
        }

        RefreshRender();
        Log.Information("Deleted {Count} selected annotations using command pattern", selectedItems.Count);
    }

    /// <summary>
    /// Get annotation service (for external access)
    /// </summary>
    public IAnnotationService GetAnnotationService() => _annotationService;

    /// <summary>
    /// Request export to file
    /// </summary>
    public void RequestExport()
    {
        ExportRequested?.Invoke();
    }

    /// <summary>
    /// Show color picker
    /// </summary>
    public void ShowColorPicker()
    {
        ColorPickerRequested?.Invoke();
    }


    /// <summary>
    /// End text editing (public for use by OverlayWindow)
    /// </summary>
    public void EndTextEditing()
    {
        _editingHandler?.EndTextEditing();
    }

    /// <summary>
    /// Handle keyboard events (public for use by OverlayWindow)
    /// </summary>
    public void HandleKeyDown(KeyEventArgs e)
    {
        _inputHandler?.HandleKeyDown(e);
    }
}
