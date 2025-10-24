using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Rendering;
using AGI.Kapster.Desktop.Services;
using AGI.Kapster.Desktop.Services.Annotation;
using AGI.Kapster.Desktop.Services.Export;
using AGI.Kapster.Desktop.Services.Settings;
using AGI.Kapster.Desktop.Commands;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

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
    
    /// <summary>
    /// EventBus for publishing overlay events (set by AnnotationLayer)
    /// </summary>
    internal AGI.Kapster.Desktop.Overlays.Events.IOverlayEventBus? EventBus { get; set; }

    /// <summary>
    /// Get all annotation items (public for use by OverlayWindow)
    /// </summary>
    public IEnumerable<IAnnotationItem> GetAnnotations()
    {
        return _annotationService?.Manager?.Items ?? Enumerable.Empty<IAnnotationItem>();
    }
    
    /// <summary>
    /// Clear all annotations (public for use by AnnotationLayer)
    /// </summary>
    public void ClearAnnotations()
    {
        _annotationService?.Manager?.Clear();
        _commandManager?.Clear();
        InvalidateVisual();
        Log.Debug("All annotations cleared");
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

    public NewAnnotationOverlay() : this(null)
    {
    }

    // === Annotation operations for Layer/Coordinator ===
    private static List<Dictionary<string, object>>? _internalAnnotationClipboard;

    public void SelectAllAnnotations()
    {
        _annotationService.Manager.SelectAll();
        InvalidateVisual();
        Log.Debug("SelectAllAnnotations executed");
    }

    public bool NudgeSelected(Vector delta)
    {
        if (!_annotationService.Manager.HasSelection)
            return false;

        var selected = _annotationService.Manager.SelectedItems;
        var prevUnion = ComputeUnionBounds(selected.Select(i => i.Bounds));
        foreach (var item in selected)
        {
            item.Move(delta);
        }
        var newUnion = ComputeUnionBounds(selected.Select(i => i.Bounds));
        var dirty = Union(prevUnion, newUnion).Inflate(DirtyPadding);
        _renderer.RenderChanged(this, _annotationService.Manager.Items, dirty);
        Log.Debug("NudgeSelected by {Delta}", delta);
        return true;
    }

    public bool CopySelectedInternal()
    {
        if (!_annotationService.Manager.HasSelection)
            return false;
        _internalAnnotationClipboard = _annotationService.Manager.SelectedItems
            .Select(item => item.Serialize())
            .ToList();
        Log.Debug("CopySelectedInternal: {Count} items", _internalAnnotationClipboard.Count);
        return _internalAnnotationClipboard.Count > 0;
    }

    public bool PasteFromInternalClipboard()
    {
        if (_internalAnnotationClipboard == null || _internalAnnotationClipboard.Count == 0)
            return false;

        // Create items from clipboard data with offset
        const double offset = 10;
        var created = new List<IAnnotationItem>();
        foreach (var data in _internalAnnotationClipboard)
        {
            var item = AnnotationFactory.CreateFromData(data);
            if (item != null)
            {
                item.Move(new Vector(offset, offset));
                _annotationService.Manager.AddItem(item);
                created.Add(item);
            }
        }
        if (created.Count == 0) return false;

        _annotationService.Manager.SelectItems(created);
        InvalidateVisual();
        Log.Debug("PasteFromInternalClipboard: {Count} items", created.Count);
        return true;
    }

    public bool DuplicateSelectedInternal()
    {
        if (!_annotationService.Manager.HasSelection)
            return false;
        var clones = _annotationService.Manager.CloneSelected();
        if (clones.Count == 0) return false;
        const double offset = 10;
        foreach (var item in clones)
        {
            item.Move(new Vector(offset, offset));
            _annotationService.Manager.AddItem(item);
        }
        _annotationService.Manager.SelectItems(clones);
        InvalidateVisual();
        Log.Debug("DuplicateSelectedInternal: {Count} items", clones.Count);
        return true;
    }

    public string? SerializeSelectedToJson()
    {
        if (!_annotationService.Manager.HasSelection) return null;
        var data = _annotationService.Manager.SelectedItems
            .Select(item => item.Serialize())
            .ToList();
        if (data.Count == 0) return null;
        var json = JsonSerializer.Serialize(data);
        return json;
    }

    public bool PasteFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var data = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);
            if (data == null || data.Count == 0) return false;
            const double offset = 10;
            var created = new List<IAnnotationItem>();
            foreach (var itemData in data)
            {
                var item = AnnotationFactory.CreateFromData(itemData);
                if (item != null)
                {
                    item.Move(new Vector(offset, offset));
                    _annotationService.Manager.AddItem(item);
                    created.Add(item);
                }
            }
            if (created.Count == 0) return false;
            _annotationService.Manager.SelectItems(created);
            InvalidateVisual();
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "PasteFromJson failed");
            return false;
        }
    }

    // Track if we've already sized to avoid repeated updates
    private bool _isSized = false;

    public NewAnnotationOverlay(ISettingsService? settingsService)
    {
        _annotationService = new AnnotationService(settingsService);
        _renderer = new AnnotationRenderer();
        _commandManager = new CommandManager();

        // CRITICAL: Use semi-transparent background (1% opacity) to enable hit-testing
        // Pure Transparent background does NOT trigger hit-test in Avalonia
        Background = new SolidColorBrush(Colors.Transparent, 0.01);
        IsHitTestVisible = false; // Start as non-interactive

        // Subscribe to annotation events
        _annotationService.Manager.ItemChanged += OnItemChanged;
        _annotationService.Manager.SelectionChanged += OnSelectionChanged;
        _annotationService.ToolChanged += OnToolChanged;
        _annotationService.StyleChanged += OnStyleChanged;

        // Enable keyboard focus for shortcuts
        Focusable = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        
        // Subscribe to LayoutUpdated to ensure sizing after layout is complete
        // This is critical for Canvas to receive mouse events - it needs explicit Width/Height
        _isSized = false;
        this.LayoutUpdated += OnLayoutUpdated;
    }
    
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        
        // Unsubscribe from layout updates
        this.LayoutUpdated -= OnLayoutUpdated;
        _isSized = false;
    }
    
    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        // Only run once after layout is complete
        if (_isSized)
            return;
        
        // Canvas children don't auto-fill even with Stretch alignment
        // We must explicitly set Width/Height after parent layout is complete
        var parent = this.Parent as Canvas;
        if (parent != null && parent.Bounds.Width > 0 && parent.Bounds.Height > 0)
        {
            this.Width = parent.Bounds.Width;
            this.Height = parent.Bounds.Height;
            Canvas.SetLeft(this, 0);
            Canvas.SetTop(this, 0);
            
            _isSized = true;
            
            // Unsubscribe after sizing to avoid repeated updates
            this.LayoutUpdated -= OnLayoutUpdated;
            
            Log.Debug("NewAnnotationOverlay sized to parent: {Width}x{Height}", this.Width, this.Height);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        Log.Debug("OnKeyDown called with key: {Key}, modifiers: {Modifiers}", e.Key, e.KeyModifiers);

        try
        {
            // If currently editing text, don't handle tool hotkeys
            if (_editingTextBox != null)
            {
                Log.Debug("Text editing active, skipping tool hotkey handling for key: {Key}", e.Key);
                return;
            }
            switch (e.Key)
            {
                case Key.Left:
                case Key.Right:
                case Key.Up:
                case Key.Down:
                    if (_annotationService.Manager.HasSelection)
                    {
                        var delta = e.Key switch
                        {
                            Key.Left => new Vector(-1, 0),
                            Key.Right => new Vector(1, 0),
                            Key.Up => new Vector(0, -1),
                            Key.Down => new Vector(0, 1),
                            _ => new Vector(0, 0)
                        };
                        var selected = _annotationService.Manager.SelectedItems;
                        var prevUnion = ComputeUnionBounds(selected.Select(i => i.Bounds));
                        foreach (var item in selected)
                        {
                            item.Move(delta);
                        }
                        var newUnion = ComputeUnionBounds(selected.Select(i => i.Bounds));
                        var dirty = Union(prevUnion, newUnion).Inflate(DirtyPadding);
                        _renderer.RenderChanged(this, _annotationService.Manager.Items, dirty);
                        e.Handled = true;
                    }
                    break;
                case Key.Delete:
                    if (_annotationService.Manager.HasSelection)
                    {
                        var selectedItems = _annotationService.Manager.SelectedItems.ToList();
                        foreach (var item in selectedItems)
                        {
                            var removeCommand = new RemoveAnnotationCommand(_annotationService.Manager, _renderer, item, this);
                            _commandManager.ExecuteCommand(removeCommand);
                        }
                        RefreshRender();
                        e.Handled = true;
                    }
                    break;

                // Size (Stroke Width) shortcuts: Ctrl + '-' decreases, Ctrl + '+' increases
                case Key.OemMinus when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                {
                    var current = (int)Math.Round(CurrentStyle.StrokeWidth);
                    var next = Math.Clamp(current - 1, 1, 20);
                    if (next != current)
                    {
                        SetStrokeWidth(next);
                        Log.Information("Stroke width decreased via Ctrl+-: {Old} -> {New}", current, next);
                    }
                    e.Handled = true;
                    break;
                }
                case Key.OemPlus when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                {
                    var current = (int)Math.Round(CurrentStyle.StrokeWidth);
                    var next = Math.Clamp(current + 1, 1, 20);
                    if (next != current)
                    {
                        SetStrokeWidth(next);
                        Log.Information("Stroke width increased via Ctrl++: {Old} -> {New}", current, next);
                    }
                    e.Handled = true;
                    break;
                }

                case Key.A when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                    // Select all annotations
                    _annotationService.Manager.SelectAll();
                    e.Handled = true;
                    break;

                case Key.Enter:
                    // Enter key confirms selection and exits selection state
                    if (_selectionRect.Width > 0 && _selectionRect.Height > 0)
                    {
                        Log.Debug("Enter key pressed - confirming selection and exiting selection state");
                        var currentSelection = _selectionRect;

                        // Clear selection state before confirming to prevent further drawing
                        _selectionRect = new Rect();
                        IsHitTestVisible = false;

                        // Trigger confirm event
                        ConfirmRequested?.Invoke(currentSelection);
                        e.Handled = true;
                    }
                    break;

                case Key.Escape:
                    // ESC key will be handled by OverlayWindow to exit screenshot mode
                    // Don't handle it here to let it bubble up
                    Log.Information("ESC key pressed in NewAnnotationOverlay - letting OverlayWindow handle it");
                    break;

                case Key.Z when e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    // Undo
                    if (_commandManager.CanUndo)
                    {
                        _commandManager.Undo();
                        RefreshRender();
                        Log.Information("Undo: {Description}", _commandManager.UndoDescription);
                        e.Handled = true;
                    }
                    break;

                case Key.Y when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                case Key.Z when e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    // Redo (Ctrl+Y or Ctrl+Shift+Z)
                    if (_commandManager.CanRedo)
                    {
                        _commandManager.Redo();
                        RefreshRender();
                        Log.Information("Redo: {Description}", _commandManager.RedoDescription);
                        e.Handled = true;
                    }
                    break;

                // Tool hotkeys (only when no modifiers are pressed)
                case Key.S when !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    Log.Debug("Tool hotkey S pressed - switching to Select tool");
                    _annotationService.CurrentTool = AnnotationToolType.None; // None is used for selection tool
                    e.Handled = true;
                    break;
                case Key.A when !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    Log.Debug("Tool hotkey A pressed - switching to Arrow tool");
                    _annotationService.CurrentTool = AnnotationToolType.Arrow;
                    e.Handled = true;
                    break;
                case Key.R when !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    Log.Debug("Tool hotkey R pressed - switching to Rectangle tool");
                    _annotationService.CurrentTool = AnnotationToolType.Rectangle;
                    e.Handled = true;
                    break;
                case Key.E when !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    Log.Debug("Tool hotkey E pressed - switching to Ellipse tool");
                    _annotationService.CurrentTool = AnnotationToolType.Ellipse;
                    e.Handled = true;
                    break;
                case Key.T when !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    Log.Debug("Tool hotkey T pressed - switching to Text tool");
                    _annotationService.CurrentTool = AnnotationToolType.Text;
                    e.Handled = true;
                    break;
                case Key.F when !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    Log.Debug("Tool hotkey F pressed - switching to Freehand tool");
                    _annotationService.CurrentTool = AnnotationToolType.Freehand;
                    e.Handled = true;
                    break;
                case Key.M when !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    Log.Debug("Tool hotkey M pressed - switching to Mosaic tool");
                    _annotationService.CurrentTool = AnnotationToolType.Mosaic;
                    e.Handled = true;
                    break;
                case Key.J when !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    Log.Debug("Tool hotkey J pressed - switching to Emoji tool");
                    _annotationService.CurrentTool = AnnotationToolType.Emoji;
                    e.Handled = true;
                    break;
                case Key.C when !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    Log.Debug("Color picker hotkey C pressed - triggering color picker event");
                    ColorPickerRequested?.Invoke();
                    e.Handled = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnKeyDown for key {Key}", e.Key);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        try
        {
            base.OnPointerPressed(e);

            var point = e.GetCurrentPoint(this).Position;
            var properties = e.GetCurrentPoint(this).Properties;
            var hasSelection = _selectionRect.Width >= 2 && _selectionRect.Height >= 2;
            var pointInSelection = hasSelection && _selectionRect.Contains(point);

            // Handle right-click during text editing for quick completion
            if (properties.IsRightButtonPressed && _editingTextBox != null)
            {
                // Check if click is outside the text editing area
                var textBoxBounds = new Rect(
                    Canvas.GetLeft(_editingTextBox),
                    Canvas.GetTop(_editingTextBox),
                    _editingTextBox.Bounds.Width,
                    _editingTextBox.Bounds.Height);

                if (!textBoxBounds.Contains(point))
                {
                    // Right-click outside text box - complete editing and stay in text tool mode
                    Log.Information("Right-click outside text box - completing text editing and keeping text tool active");
                    EndTextEditing();
                    // Keep the text tool active for quick successive text creation
                    // Don't change CurrentTool - user can continue creating text annotations
                    e.Handled = true;
                    return;
                }
            }

            if (properties.IsLeftButtonPressed)
            {
                // Handle double-click for various actions
                if (e.ClickCount == 2)
                {
                    // Double-click should open text editing if hitting text annotation
                    var hitItem = _annotationService.HitTest(point);
                    if (hitItem is TextAnnotation textItem)
                    {
                        if (!_annotationService.Manager.SelectedItems.Contains(textItem))
                        {
                            _annotationService.Manager.ClearSelection();
                            _annotationService.Manager.SelectItem(textItem);
                        }

                        StartTextEditing(textItem);
                        e.Handled = true;
                        return;
                    }

                    // Clear annotation selection if any (anchor points)
                    if (_annotationService.Manager.HasSelection)
                    {
                        Log.Debug("Double-click detected with selected annotations - clearing selection");
                        _annotationService.Manager.ClearSelection();
                        RefreshRender();
                    }

                    // Handle save to clipboard operation if there's a selection rect
                    if (hasSelection && !pointInSelection)
                    {
                        // Save current selection rect before clearing
                        var currentSelection = _selectionRect;

                        // Clear selection state before confirming to prevent further drawing
                        _selectionRect = new Rect();
                        IsHitTestVisible = false;
                        ConfirmRequested?.Invoke(currentSelection);
                        e.Handled = true;
                        return;
                    }
                }

                // If no selection exists, let SelectionOverlay handle it
                if (!hasSelection)
                {
                    e.Handled = false;
                    return;
                }

                // If point is outside selection area, let SelectionOverlay handle it
                if (!pointInSelection)
                {
                    e.Handled = false;
                    return;
                }

                // Point is inside selection area - handle annotation logic
                if (CurrentTool == AnnotationToolType.None)
                {
                    // Selection mode - for selecting/editing existing annotations
                    HandleSelectionPress(point, e.KeyModifiers.HasFlag(KeyModifiers.Control));
                }
                else
                {
                    // Creation mode - for creating new annotations
                    HandleCreationPress(point);
                    // Ensure we receive all move events accurately during creation
                    if (_isCreating && _creatingItem != null)
                    {
                        e.Pointer.Capture(this);
                    }
                }

                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnPointerPressed at {Point}", e.GetPosition(this));
            e.Handled = true; // Prevent event propagation
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var point = e.GetCurrentPoint(this).Position;

        // Handle drag and resize operations first
        if (_isDragging)
        {
            ProcessDrag(point);
            return;
        }

        if (_isResizing)
        {
            ProcessResize(point);
            return;
        }

        // Cursor logic based on tool and position
        var hasSelection = _selectionRect.Width >= 2 && _selectionRect.Height >= 2;
        var pointInSelection = hasSelection && _selectionRect.Contains(point);

        if (!hasSelection || !pointInSelection)
        {
            // Outside selection or no selection - arrow cursor
            Cursor = new Cursor(StandardCursorType.Arrow);
        }
        else if (CurrentTool != AnnotationToolType.None)
        {
            // Drawing tool selected and inside selection - cross cursor
            Cursor = new Cursor(StandardCursorType.Cross);
        }
        else
        {
            // Selection mode (CurrentTool == None) inside selection area
            var resizeHandle = HitTestResizeHandle(point);
            if (resizeHandle != ResizeHandle.None)
            {
                UpdateCursorForResize(resizeHandle);
            }
            else
            {
                // Check for hover over selectable items
                var hitItem = _annotationService.HitTest(point);
                if (hitItem != null)
                {
                    Cursor = new Cursor(StandardCursorType.Hand);
                }
                else
                {
                    // Inside selection but no specific interaction - arrow cursor
                    Cursor = new Cursor(StandardCursorType.Arrow);
                }
            }
        }

        if (_isCreating && _creatingItem != null)
        {
            var oldBounds = _creatingItem.Bounds;
            _annotationService.UpdateAnnotation(point, _creatingItem);
            var newBounds = _creatingItem.Bounds;
            var dirty = Union(oldBounds, newBounds).Inflate(DirtyPadding);
            // Ensure creating item participates in rendering during drag
            var itemsWithCreating = new System.Collections.Generic.List<IAnnotationItem>(_annotationService.Manager.Items)
            {
                _creatingItem
            };
            _renderer.RenderChanged(this, itemsWithCreating, dirty);
            // Force immediate UI update to reduce visual latency
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Render);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        var point = e.GetPosition(this);
        var hasSelection = _selectionRect.Width >= 2 && _selectionRect.Height >= 2;
        var pointInSelection = hasSelection && _selectionRect.Contains(point);

        // End drag or resize operations
        if (_isDragging || _isResizing)
        {
            // Commit resize/drag to command stack for undo
            if (_isResizing)
            {
                var selectedItems = _annotationService.Manager.SelectedItems;
                if (selectedItems.Count == 1)
                {
                    var item = selectedItems[0];
                    if (item is ArrowAnnotation)
                    {
                        var (s, ept) = GetCurrentEndpoints(item);
                        if (_resizeStartSnapshot.item is ArrowAnnotation && s.HasValue && ept.HasValue
                            && _resizeStartSnapshot.start.HasValue && _resizeStartSnapshot.end.HasValue)
                        {
                            var cmd = new SetArrowEndpointsCommand(
                                _renderer,
                                _annotationService.Manager,
                                (ArrowAnnotation)item,
                                _resizeStartSnapshot.start.Value,
                                _resizeStartSnapshot.end.Value,
                                s.Value,
                                ept.Value,
                                this);
                            _commandManager.ExecuteCommand(cmd);
                        }
                    }
                }
            }

            EndTransformation();
            e.Handled = true;
            return;
        }

        // If no selection or point outside selection, let SelectionOverlay handle it
        if (!hasSelection || !pointInSelection)
        {
            e.Handled = false;
            return;
        }

        if (_isCreating && _creatingItem != null)
        {
            // Use command pattern for creating annotations
            try
            {
                // Validate the annotation before creating command
                var isValid = ValidateAnnotationForFinish(_creatingItem);
                if (isValid)
                {
                    var addCommand = new AddAnnotationCommand(_annotationService.Manager, _renderer, _creatingItem, this);
                    _commandManager.ExecuteCommand(addCommand);
                    RefreshRender();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create annotation");
            }

            _creatingItem = null;
            _isCreating = false;
        }

        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        // End any ongoing transformation
        EndTransformation();
        // Ensure capture released
        e.Pointer.Capture(null);
        e.Pointer.Capture(null);

        if (_isCreating && _creatingItem != null)
        {
            _annotationService.CancelCreate(_creatingItem);
            _creatingItem = null;
            _isCreating = false;
            RefreshRender();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
    }

    /// <summary>
    /// Handle pointer press in selection mode
    /// </summary>
    private void HandleSelectionPress(Point point, bool addToSelection)
    {
        // Check if clicking on a resize handle first
        var resizeHandle = HitTestResizeHandle(point);
        if (resizeHandle != ResizeHandle.None)
        {
            StartResize(point, resizeHandle);
            return;
        }

        var hitItem = _annotationService.HitTest(point);

        if (hitItem != null)
        {
            if (addToSelection)
            {
                _annotationService.Manager.ToggleSelection(hitItem);
            }
            else
            {
                _annotationService.Manager.SelectItem(hitItem);
                StartDrag(point);
            }
        }
        else if (!addToSelection)
        {
            _annotationService.Manager.ClearSelection();
        }
    }

    /// <summary>
    /// Handle pointer press in creation mode
    /// </summary>
    private void HandleCreationPress(Point point)
    {
        // If currently editing text, end editing first
        if (_editingTextBox != null)
        {
            // Check if click is outside the text editing area for text tool
            if (CurrentTool == AnnotationToolType.Text)
            {
                var textBoxBounds = new Rect(
                    Canvas.GetLeft(_editingTextBox),
                    Canvas.GetTop(_editingTextBox),
                    _editingTextBox.Bounds.Width,
                    _editingTextBox.Bounds.Height);

                if (!textBoxBounds.Contains(point))
                {
                    // Click outside text box - complete current editing
                    Log.Information("Left-click outside text box - completing text editing and creating new text at new location");
                    EndTextEditing();
                    // Continue to create new text annotation at the new position
                }
                else
                {
                    // Click inside text box - let the text box handle it
                    return;
                }
            }
            else
            {
                // For non-text tools, always end text editing
                EndTextEditing();
            }
        }

        // Special handling for text tool: create directly and enter editing state
        if (CurrentTool == AnnotationToolType.Text)
        {
            try
            {
                var textItem = _annotationService.StartAnnotation(point) as TextAnnotation;
                if (textItem != null)
                {
                    // CRITICAL FIX: Remember the original selection rectangle
                    // This prevents selection bounds mismatch that causes text ghosting
                    textItem.SetOriginalSelectionRect(_selectionRect);
                    Log.Information("Text annotation created with original selection rect: {Rect}", _selectionRect);

                    // For text tool, defer command execution until editing is completed
                    // This makes text creation + editing a single atomic operation for undo/redo
                    if (ValidateAnnotationForFinish(textItem))
                    {
                        // Enter editing state immediately - command will be executed when editing ends
                        StartTextEditing(textItem);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create text annotation at {Point}", point);
            }
            return;
        }

        // Special handling for emoji tool: create directly with selected emoji
        if (CurrentTool == AnnotationToolType.Emoji)
        {
            try
            {
                var emojiItem = _annotationService.StartAnnotation(point) as EmojiAnnotation;
                if (emojiItem != null)
                {
                    // Get current selected emoji from toolbar
                    if (this.GetVisualRoot() is Window parentWindow &&
                        parentWindow.FindControl<NewAnnotationToolbar>("Toolbar") is { } toolbar &&
                        toolbar.FindControl<TextBlock>("CurrentEmojiText") is { } emojiText)
                    {
                        emojiItem.Emoji = emojiText.Text ?? "😀";
                    }

                    // Complete creation immediately using command pattern
                    if (ValidateAnnotationForFinish(emojiItem))
                    {
                        var addCommand = new AddAnnotationCommand(_annotationService.Manager, _renderer, emojiItem, this);
                        _commandManager.ExecuteCommand(addCommand);
                        RefreshRender();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create emoji annotation at {Point}", point);
            }
            return;
        }

        // Normal creation flow for other tools
        try
        {
            _creatingItem = _annotationService.StartAnnotation(point);

            if (_creatingItem != null)
            {
                _isCreating = true;
                RefreshRender();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start creating annotation with tool {Tool} at {Point}", CurrentTool, point);
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
    /// Update mouse cursor - now handled dynamically in OnPointerMoved
    /// </summary>
    private void UpdateCursor()
    {
        // No longer set cursor here - let OnPointerMoved handle it based on position
        // This avoids conflicts between static tool-based cursor and dynamic position-based cursor
        Log.Debug("UpdateCursor called: CurrentTool={CurrentTool} (cursor will be set by OnPointerMoved)", CurrentTool);
    }

    #region Selection and Transformation

    /// <summary>
    /// Hit test resize handles for selected annotations
    /// </summary>
    private ResizeHandle HitTestResizeHandle(Point point)
    {
        var selectedItems = _annotationService.Manager.SelectedItems;
        if (selectedItems.Count != 1) return ResizeHandle.None;

        var item = selectedItems[0];
        // For text annotations, use actual text rendering bounds for precise handle hit testing
        // This must match the bounds used in AnnotationRenderer.RenderSelectionHandles
        var bounds = item is TextAnnotation textAnnotation ?
            textAnnotation.GetTextRenderBounds() :
            item.Bounds;
        const double handleSize = 8;
        const double tolerance = 6; // Increased tolerance for better precision

        // Define handle center points for distance-based hit testing
        var handlePositions = new[]
        {
            (ResizeHandle.TopLeft, new Point(bounds.Left, bounds.Top)),
            (ResizeHandle.TopCenter, new Point(bounds.Center.X, bounds.Top)),
            (ResizeHandle.TopRight, new Point(bounds.Right, bounds.Top)),
            (ResizeHandle.MiddleRight, new Point(bounds.Right, bounds.Center.Y)),
            (ResizeHandle.BottomRight, new Point(bounds.Right, bounds.Bottom)),
            (ResizeHandle.BottomCenter, new Point(bounds.Center.X, bounds.Bottom)),
            (ResizeHandle.BottomLeft, new Point(bounds.Left, bounds.Bottom)),
            (ResizeHandle.MiddleLeft, new Point(bounds.Left, bounds.Center.Y))
        };

        // Use distance-based hit testing for more precise control
        const double maxDistance = handleSize / 2 + tolerance;
        foreach (var (handle, handlePos) in handlePositions)
        {
            var distance = Math.Sqrt(Math.Pow(point.X - handlePos.X, 2) + Math.Pow(point.Y - handlePos.Y, 2));
            if (distance <= maxDistance)
                return handle;
        }

        return ResizeHandle.None;
    }

    /// <summary>
    /// Start dragging selected annotations
    /// </summary>
    private void StartDrag(Point point)
    {
        var selectedItems = _annotationService.Manager.SelectedItems;
        if (selectedItems.Count == 0) return;

        _isDragging = true;
        _dragStartPoint = point;
        UpdateCursorForDrag();
    }

    /// <summary>
    /// Start resizing annotation
    /// </summary>
    private void StartResize(Point point, ResizeHandle handle)
    {
        var selectedItems = _annotationService.Manager.SelectedItems;
        if (selectedItems.Count != 1) return;

        _isResizing = true;
        _activeResizeHandle = handle;
        _dragStartPoint = point;
        _dragStartBounds = selectedItems[0].Bounds;
        // Snapshot start state for undo
        _resizeStartSnapshot = CreateSnapshot(selectedItems[0]);
        // Arrow endpoint lock-in
        if (selectedItems[0] is ArrowAnnotation arrow)
        {
            var handleCenter = GetHandleCenter(_dragStartBounds, handle);
            var distStart = Math.Sqrt(Math.Pow(handleCenter.X - arrow.StartPoint.X, 2) + Math.Pow(handleCenter.Y - arrow.StartPoint.Y, 2));
            var distEnd = Math.Sqrt(Math.Pow(handleCenter.X - arrow.EndPoint.X, 2) + Math.Pow(handleCenter.Y - arrow.EndPoint.Y, 2));
            _resizeMoveStart = distStart <= distEnd;
            _resizeFixedEndpoint = _resizeMoveStart ? arrow.EndPoint : arrow.StartPoint;
        }
        UpdateCursorForResize(handle);
    }

    /// <summary>
    /// Update mouse cursor for drag operation
    /// </summary>
    private void UpdateCursorForDrag()
    {
        Cursor = new Cursor(StandardCursorType.SizeAll);
    }

    /// <summary>
    /// Update mouse cursor for resize operation
    /// </summary>
    private void UpdateCursorForResize(ResizeHandle handle)
    {
        Cursor = handle switch
        {
            ResizeHandle.TopLeft or ResizeHandle.BottomRight => new Cursor(StandardCursorType.TopLeftCorner),
            ResizeHandle.TopRight or ResizeHandle.BottomLeft => new Cursor(StandardCursorType.TopRightCorner),
            ResizeHandle.TopCenter or ResizeHandle.BottomCenter => new Cursor(StandardCursorType.SizeNorthSouth),
            ResizeHandle.MiddleLeft or ResizeHandle.MiddleRight => new Cursor(StandardCursorType.SizeWestEast),
            _ => new Cursor(StandardCursorType.Arrow)
        };
    }

    /// <summary>
    /// Process drag operation
    /// </summary>
    private void ProcessDrag(Point currentPoint)
    {
        if (!_isDragging) return;

        var selectedItems = _annotationService.Manager.SelectedItems;
        if (selectedItems.Count == 0) return;

        // Compute union of previous bounds
        var prevUnion = ComputeUnionBounds(selectedItems.Select(i => i.Bounds));

        // Apply movement
        var delta = currentPoint - _dragStartPoint;
        foreach (var item in selectedItems)
        {
            item.Move(delta);
        }

        // Compute union of new bounds
        var newUnion = ComputeUnionBounds(selectedItems.Select(i => i.Bounds));
        _dragStartPoint = currentPoint;

        // Dirty rect = union(prev, new) with padding (shadow/AA)
        var dirty = Union(prevUnion, newUnion).Inflate(DirtyPadding);
        _renderer.RenderChanged(this, _annotationService.Manager.Items, dirty);
    }

    /// <summary>
    /// Process resize operation
    /// </summary>
    private void ProcessResize(Point currentPoint)
    {
        if (!_isResizing) return;

        var selectedItems = _annotationService.Manager.SelectedItems;
        if (selectedItems.Count != 1) return;

        var item = selectedItems[0];
        var prevBounds = item.Bounds;
        var delta = currentPoint - _dragStartPoint;

        var newBounds = CalculateNewBounds(_dragStartBounds, _activeResizeHandle, delta);
        if (item is ArrowAnnotation arrowItem)
        {
            ApplyArrowResizeFollowHandle(arrowItem, newBounds, _activeResizeHandle);
        }
        else
        {
            // Apply transformation based on item type
            ApplyTransformation(item, _dragStartBounds, newBounds);
        }

        var dirty = Union(prevBounds, item.Bounds).Inflate(DirtyPadding);
        _renderer.RenderChanged(this, _annotationService.Manager.Items, dirty);
    }

    private (IAnnotationItem item, Point? start, Point? end) CreateSnapshot(IAnnotationItem item)
    {
        return item is ArrowAnnotation a
            ? (item, a.StartPoint, a.EndPoint)
            : (item, null, null);
    }

    private (Point? start, Point? end) GetCurrentEndpoints(IAnnotationItem item)
    {
        if (item is ArrowAnnotation a)
            return (a.StartPoint, a.EndPoint);
        return (null, null);
    }

    private (IAnnotationItem item, Point? start, Point? end) _resizeStartSnapshot;

    private void ApplyArrowResizeFollowHandle(ArrowAnnotation arrow, Rect newBounds, ResizeHandle handle)
    {
        if (arrow.IsLocked) return;
        // Follow the dragged handle directly; keep the opposite endpoint fixed for the whole gesture
        var target = GetHandleCenter(newBounds, handle);
        var guard = (_selectionRect.Width > 0 && _selectionRect.Height > 0) ? _selectionRect : newBounds;
        target = ClampPoint(target, guard);

        const double minLen = 2.0;
        if (_resizeMoveStart)
        {
            var newStart = target;
            if (Vector.Distance(newStart, _resizeFixedEndpoint) < minLen)
            {
                var dir = _resizeFixedEndpoint - newStart;
                var len = Math.Max(1e-6, Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y));
                var ux = dir.X / len; var uy = dir.Y / len;
                newStart = new Point(_resizeFixedEndpoint.X - ux * minLen, _resizeFixedEndpoint.Y - uy * minLen);
            }
            arrow.StartPoint = newStart;
            arrow.EndPoint = _resizeFixedEndpoint;
        }
        else
        {
            var newEnd = target;
            if (Vector.Distance(_resizeFixedEndpoint, newEnd) < minLen)
            {
                var dir = newEnd - _resizeFixedEndpoint;
                var len = Math.Max(1e-6, Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y));
                var ux = dir.X / len; var uy = dir.Y / len;
                newEnd = new Point(_resizeFixedEndpoint.X + ux * minLen, _resizeFixedEndpoint.Y + uy * minLen);
            }
            arrow.StartPoint = _resizeFixedEndpoint;
            arrow.EndPoint = newEnd;
        }
    }

    private static Point ClampPoint(Point p, Rect rect)
    {
        var x = Math.Max(rect.Left, Math.Min(rect.Right, p.X));
        var y = Math.Max(rect.Top, Math.Min(rect.Bottom, p.Y));
        return new Point(x, y);
    }

    private static ResizeHandle GetOppositeHandle(ResizeHandle handle)
    {
        return handle switch
        {
            ResizeHandle.TopLeft => ResizeHandle.BottomRight,
            ResizeHandle.TopCenter => ResizeHandle.BottomCenter,
            ResizeHandle.TopRight => ResizeHandle.BottomRight,
            ResizeHandle.MiddleRight => ResizeHandle.MiddleLeft,
            ResizeHandle.BottomRight => ResizeHandle.TopLeft,
            ResizeHandle.BottomCenter => ResizeHandle.TopCenter,
            ResizeHandle.BottomLeft => ResizeHandle.TopRight,
            ResizeHandle.MiddleLeft => ResizeHandle.MiddleRight,
            _ => ResizeHandle.BottomRight
        };
    }

    // Arrow resize state
    private bool _resizeMoveStart;
    private Point _resizeFixedEndpoint;

    private static Point GetHandleCenter(Rect bounds, ResizeHandle handle)
    {
        return handle switch
        {
            ResizeHandle.TopLeft => new Point(bounds.Left, bounds.Top),
            ResizeHandle.TopCenter => new Point(bounds.Center.X, bounds.Top),
            ResizeHandle.TopRight => new Point(bounds.Right, bounds.Top),
            ResizeHandle.MiddleRight => new Point(bounds.Right, bounds.Center.Y),
            ResizeHandle.BottomRight => new Point(bounds.Right, bounds.Bottom),
            ResizeHandle.BottomCenter => new Point(bounds.Center.X, bounds.Bottom),
            ResizeHandle.BottomLeft => new Point(bounds.Left, bounds.Bottom),
            ResizeHandle.MiddleLeft => new Point(bounds.Left, bounds.Center.Y),
            _ => bounds.Center
        };
    }

    private static Rect ComputeUnionBounds(IEnumerable<Rect> rects)
    {
        var enumerated = rects.ToList();
        if (enumerated.Count == 0) return new Rect();
        var union = enumerated[0];
        for (int i = 1; i < enumerated.Count; i++)
        {
            union = Union(union, enumerated[i]);
        }
        return union;
    }

    private static Rect Union(Rect a, Rect b)
    {
        if (a.Width <= 0 || a.Height <= 0) return b;
        if (b.Width <= 0 || b.Height <= 0) return a;
        var left = Math.Min(a.Left, b.Left);
        var top = Math.Min(a.Top, b.Top);
        var right = Math.Max(a.Right, b.Right);
        var bottom = Math.Max(a.Bottom, b.Bottom);
        return new Rect(left, top, right - left, bottom - top);
    }

    private const double DirtyPadding = 3.0; // compensate AA/shadow
    private System.Collections.Generic.List<Rect> _frameDirtyRects = new System.Collections.Generic.List<Rect>(8);

    private void FlushFrameDirtyIfNeeded()
    {
        if (_frameDirtyRects == null || _frameDirtyRects.Count == 0) return;
        // Defer to UI thread low priority to coalesce same-frame updates
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_frameDirtyRects == null || _frameDirtyRects.Count == 0) return;
            var batch = _frameDirtyRects.ToArray();
            _frameDirtyRects.Clear();
            _renderer.RenderChanged(this, _annotationService.Manager.Items, batch);
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// Calculate new bounds based on resize handle and delta
    /// </summary>
    private Rect CalculateNewBounds(Rect originalBounds, ResizeHandle handle, Vector delta)
    {
        var left = originalBounds.Left;
        var top = originalBounds.Top;
        var right = originalBounds.Right;
        var bottom = originalBounds.Bottom;

        switch (handle)
        {
            case ResizeHandle.TopLeft:
                left += delta.X;
                top += delta.Y;
                break;
            case ResizeHandle.TopCenter:
                top += delta.Y;
                break;
            case ResizeHandle.TopRight:
                right += delta.X;
                top += delta.Y;
                break;
            case ResizeHandle.MiddleRight:
                right += delta.X;
                break;
            case ResizeHandle.BottomRight:
                right += delta.X;
                bottom += delta.Y;
                break;
            case ResizeHandle.BottomCenter:
                bottom += delta.Y;
                break;
            case ResizeHandle.BottomLeft:
                left += delta.X;
                bottom += delta.Y;
                break;
            case ResizeHandle.MiddleLeft:
                left += delta.X;
                break;
        }

        // Ensure minimum size
        const double minSize = 10;
        if (right - left < minSize) right = left + minSize;
        if (bottom - top < minSize) bottom = top + minSize;

        return new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Apply transformation to annotation item
    /// </summary>
    private void ApplyTransformation(IAnnotationItem item, Rect originalBounds, Rect newBounds)
    {
        // Calculate scale factors
        var scaleX = newBounds.Width / originalBounds.Width;
        var scaleY = newBounds.Height / originalBounds.Height;

        // Calculate translation
        var translation = newBounds.TopLeft - originalBounds.TopLeft;

        // Apply transformation based on item type
        switch (item)
        {
            case RectangleAnnotation rect:
                rect.TopLeft = newBounds.TopLeft;
                rect.BottomRight = newBounds.BottomRight;
                break;
            case EllipseAnnotation ellipse:
                ellipse.BoundingRect = newBounds;
                break;
            case ArrowAnnotation arrow:
                // For arrows, scale relative to center
                var center = originalBounds.Center;
                var startVector = arrow.StartPoint - center;
                var endVector = arrow.EndPoint - center;
                var newStartPoint = center + new Vector(startVector.X * scaleX, startVector.Y * scaleY) + translation;
                var newEndPoint = center + new Vector(endVector.X * scaleX, endVector.Y * scaleY) + translation;
                arrow.StartPoint = newStartPoint;
                arrow.EndPoint = newEndPoint;
                break;
            case TextAnnotation text:
                // For text annotations, scale strictly according to drag distance
                ApplyTextTransformation(text, originalBounds, newBounds);
                break;
        }

        item.ModifiedAt = DateTime.Now;
    }

    /// <summary>
    /// Apply transformation specifically for text annotations to strictly follow drag bounds
    /// </summary>
    private void ApplyTextTransformation(TextAnnotation text, Rect originalBounds, Rect newBounds)
    {
        // Update position to match new bounds
        text.Position = newBounds.TopLeft;

        // Use the new SetTargetSize method for precise size control
        text.SetTargetSize(new Size(newBounds.Width, newBounds.Height));

        Log.Debug("Applied text transformation: Position={Position}, NewBounds={NewBounds}",
                 newBounds.TopLeft, newBounds);
    }

    /// <summary>
    /// End drag or resize operation
    /// </summary>
    private void EndTransformation()
    {
        if (_isDragging || _isResizing)
        {
            _isDragging = false;
            _isResizing = false;
            _activeResizeHandle = ResizeHandle.None;
        }
    }

    #endregion

    // Performance optimization: throttle rendering
    private DateTime _lastRenderTime = DateTime.MinValue;
    private const double MinRenderInterval = 16.0; // ~60 FPS (milliseconds)
    private bool _renderPending = false;

    /// <summary>
    /// Refresh rendering
    /// </summary>
    private void RefreshRender()
    {
        RefreshRenderThrottled(false);
    }

    /// <summary>
    /// Refresh rendering with throttling for performance
    /// </summary>
    private void RefreshRenderThrottled(bool force = false)
    {
        try
        {
            var now = DateTime.UtcNow;
            var timeSinceLastRender = (now - _lastRenderTime).TotalMilliseconds;

            // Check if we should skip this update for performance
            if (!force && timeSinceLastRender < MinRenderInterval)
            {
                // Mark that a render is pending and schedule it
                if (!_renderPending)
                {
                    _renderPending = true;
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        _renderPending = false;
                        RefreshRenderThrottled(true);
                    }, Avalonia.Threading.DispatcherPriority.Background);
                }
                return;
            }

            _lastRenderTime = now;

            var items = _annotationService.Manager.Items;

            // Add creating item temporarily for rendering
            if (_creatingItem != null)
            {
                var itemsWithCreating = new System.Collections.Generic.List<IAnnotationItem>(items) { _creatingItem };
                _renderer.RenderAll(this, itemsWithCreating);
            }
            else
            {
                _renderer.RenderAll(this, items);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error refreshing render");
        }
    }

    /// <summary>
    /// Handle annotation item change events
    /// </summary>
    private void OnItemChanged(object? sender, AnnotationChangedEventArgs e)
    {
        try
        {
            RefreshRender();
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
            RefreshRender();
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
            // Force cursor update when tool changes
            Log.Information("OnToolChanged: {OldTool} -> {NewTool}, forcing cursor update", e.OldTool, e.NewTool);

            // If we are in drawing mode and have a valid screenshot selection,
            // immediately clear any annotation selection (resize anchors) and set cross cursor
            var hasSelection = _selectionRect.Width >= 2 && _selectionRect.Height >= 2;
            if (hasSelection && CurrentTool != AnnotationToolType.None)
            {
                _annotationService.Manager.ClearSelection();
                IsHitTestVisible = true;
                Cursor = new Cursor(StandardCursorType.Cross);
                Log.Information("OnToolChanged: cleared annotation selection and set Cross cursor for {Tool}", CurrentTool);
            }
            else if (hasSelection && CurrentTool == AnnotationToolType.None)
            {
                Cursor = new Cursor(StandardCursorType.Arrow);
                Log.Information("OnToolChanged: FORCED Arrow cursor for None tool");
            }

            // Cancel any ongoing creation when tool changes
            if (_isCreating && _creatingItem != null)
            {
                _annotationService.CancelCreate(_creatingItem);
                _creatingItem = null;
                _isCreating = false;
                RefreshRender();
            }

            // (moved) selection clearing handled above when entering drawing mode

            // Note: ESC key handling is done at OverlayWindow level
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
            Log.Debug("OnStyleChanged: StrokeWidth={Width}, StrokeColor={Color}", e.NewStyle.StrokeWidth, e.NewStyle.StrokeColor);
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


    #region Text Editing

    /// <summary>
    /// Start editing text annotation
    /// </summary>
    private void StartTextEditing(TextAnnotation textItem)
    {
        try
        {
            // If already editing other text, end editing first
            EndTextEditing();

            // Enable IME for text input
            EventBus?.Publish(new AGI.Kapster.Desktop.Overlays.Events.ImeChangeRequestedEvent(true));

            _editingTextItem = textItem;

            // CRITICAL FIX: Force remove existing text render before entering edit mode
            // This prevents ghosting during text editing
            _renderer.RemoveRender(this, textItem);
            Log.Information("Removed existing render for text annotation {Id} before entering edit mode", textItem.Id);

            textItem.StartEditing();

            // CRITICAL FIX: Immediately refresh render to remove original text display
            // This prevents ghosting during text editing
            RefreshRender();

            // Get text selection state boundary size (consistent with selection state display)
            var bounds = textItem.Bounds; // This returns the editing boundary of the selection state
            var actualWidth = bounds.Width;
            var actualHeight = bounds.Height;

            // Create text editing box - precisely match TextBlock rendering position and size
            _editingTextBox = new TextBox
            {
                Text = textItem.Text,
                FontFamily = new Avalonia.Media.FontFamily(textItem.Style.FontFamily),
                FontSize = textItem.Style.FontSize,
                FontWeight = (Avalonia.Media.FontWeight)textItem.Style.FontWeight,
                FontStyle = (Avalonia.Media.FontStyle)textItem.Style.FontStyle,
                Foreground = new SolidColorBrush(textItem.Style.StrokeColor),
                Background = new SolidColorBrush(Colors.White, 0.9), // Semi-transparent background for easy editing
                BorderBrush = new SolidColorBrush(textItem.Style.StrokeColor),
                BorderThickness = new Avalonia.Thickness(1),
                // CRITICAL: Remove padding and margin to match TextBlock rendering exactly
                Padding = new Avalonia.Thickness(0),
                Margin = new Avalonia.Thickness(0),
                // Use actual text size, not selection size
                Width = actualWidth,
                Height = actualHeight,
                MinWidth = 100,
                MinHeight = textItem.Style.FontSize + 8,
                AcceptsReturn = true,
                TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
                // Ensure consistent text alignment
                TextAlignment = Avalonia.Media.TextAlignment.Left,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Top
            };

            // Set position - consider border offset for precise alignment
            // TextBox has 1px border, need to offset 1px to top-left to align with TextBlock
            Canvas.SetLeft(_editingTextBox, textItem.Position.X - 1);
            Canvas.SetTop(_editingTextBox, textItem.Position.Y - 1);

            // Add to canvas
            Children.Add(_editingTextBox);

            // Handle editing completion events
            _editingTextBox.LostFocus += OnTextEditingLostFocus;
            _editingTextBox.KeyDown += OnTextEditingKeyDown;
            _editingTextBox.TextChanged += OnTextEditingTextChanged;

            // Focus and select all text
            _editingTextBox.Focus();
            _editingTextBox.SelectAll();

            Log.Information("Started text editing for annotation {Id}", textItem.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start text editing for annotation {Id}", textItem.Id);

            // Clean up state
            _editingTextBox = null;
            _editingTextItem = null;
            textItem.EndEditing();
        }
    }

    /// <summary>
    /// Text editing box content change event - implement auto-expansion
    /// </summary>
    private void OnTextEditingTextChanged(object? sender, TextChangedEventArgs e)
    {
        try
        {
            if (_editingTextBox == null || _editingTextItem == null) return;

            // Measure text size
            var formattedText = new Avalonia.Media.FormattedText(
                _editingTextBox.Text ?? "",
                System.Globalization.CultureInfo.CurrentCulture,
                Avalonia.Media.FlowDirection.LeftToRight,
                new Avalonia.Media.Typeface(_editingTextItem.Style.FontFamily,
                                            (Avalonia.Media.FontStyle)_editingTextItem.Style.FontStyle,
                                            (Avalonia.Media.FontWeight)_editingTextItem.Style.FontWeight),
                _editingTextItem.Style.FontSize,
                Brushes.Black);

            // Calculate new size (add some editing space)
            var newWidth = Math.Max(formattedText.Width + 20, 100);
            var newHeight = Math.Max(formattedText.Height + 8, _editingTextItem.Style.FontSize + 8);

            // Update editing box size
            _editingTextBox.Width = newWidth;
            _editingTextBox.Height = newHeight;

            Log.Debug("Auto-expanded text box to {Width}x{Height} for text: {Text}",
                     newWidth, newHeight, _editingTextBox.Text?.Substring(0, Math.Min(20, _editingTextBox.Text?.Length ?? 0)));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnTextEditingTextChanged");
        }
    }

    /// <summary>
    /// End text editing (public for use by OverlayWindow)
    /// </summary>
    public void EndTextEditing()
    {
        if (_editingTextBox == null || _editingTextItem == null) return;

        try
        {
            // Disable IME after text editing
            EventBus?.Publish(new AGI.Kapster.Desktop.Overlays.Events.ImeChangeRequestedEvent(false));

            // Update text content
            var finalText = _editingTextBox.Text ?? string.Empty;
            _editingTextItem.Text = finalText;

            // Remove editing box
            Children.Remove(_editingTextBox);
            _editingTextBox.LostFocus -= OnTextEditingLostFocus;
            _editingTextBox.KeyDown -= OnTextEditingKeyDown;
            _editingTextBox.TextChanged -= OnTextEditingTextChanged;

            // CRITICAL FIX: Clear any existing rendering before changing state
            // This prevents blurring/ghosting effects
            _renderer.RemoveRender(this, _editingTextItem);

            // End editing state AFTER removing old renders
            _editingTextItem.EndEditing();

            // CRITICAL FIX: Execute add command when text editing is completed
            // This ensures the text annotation is properly added to the undo/redo stack
            if (!string.IsNullOrWhiteSpace(finalText))
            {
                var addCommand = new AddAnnotationCommand(_annotationService.Manager, _renderer, _editingTextItem, this);
                _commandManager.ExecuteCommand(addCommand);
                Log.Information("Text annotation added to command stack via EndTextEditing: {Id}, text: '{Text}'", _editingTextItem.Id, finalText);
            }
            else
            {
                // If text is empty, don't add it to the annotation manager
                Log.Information("Empty text annotation discarded: {Id}", _editingTextItem.Id);
            }

            // CRITICAL FIX: Ensure focus returns to overlay for keyboard shortcuts to work
            // This allows immediate undo/redo without clicking on canvas first
            try
            {
                Focus();
                Log.Debug("Focus returned to annotation overlay after text editing");
            }
            catch (Exception focusEx)
            {
                Log.Warning(focusEx, "Failed to return focus to annotation overlay");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error ending text editing for annotation {Id}", _editingTextItem?.Id);
        }
        finally
        {
            // Ensure state cleanup
            _editingTextBox = null;
            _editingTextItem = null;

            // Force a clean render after state changes
            try
            {
                RefreshRender();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error refreshing render after ending text editing");
            }
        }
    }

    /// <summary>
    /// Text editing box lost focus event
    /// </summary>
    private void OnTextEditingLostFocus(object? sender, EventArgs e)
    {
        try
        {
            EndTextEditing();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnTextEditingLostFocus");
        }
    }

    /// <summary>
    /// Text editing box key press event
    /// </summary>
    private void OnTextEditingKeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            if (e.Key == Key.Escape)
            {
                // ESC key cancels editing, restore original text
                if (_editingTextItem != null && _editingTextBox != null)
                {
                    _editingTextBox.Text = _editingTextItem.Text;
                }
                EndTextEditing();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                // Enter key ends editing (Shift+Enter for new line)
                EndTextEditing();
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnTextEditingKeyDown for key {Key}", e.Key);
        }
    }

    #endregion

    /// <summary>
    /// Validate annotation before finishing creation
    /// </summary>
    private bool ValidateAnnotationForFinish(IAnnotationItem item)
    {
        // Validate minimum size for shapes
        if (item is RectangleAnnotation rect)
        {
            return rect.Width >= 2 && rect.Height >= 2;
        }
        else if (item is EllipseAnnotation ellipse)
        {
            return ellipse.RadiusX >= 1 && ellipse.RadiusY >= 1;
        }
        else if (item is ArrowAnnotation arrow)
        {
            var length = Math.Sqrt(Math.Pow(arrow.EndPoint.X - arrow.StartPoint.X, 2) +
                                 Math.Pow(arrow.EndPoint.Y - arrow.StartPoint.Y, 2));
            return length >= 5;
        }
        else if (item is FreehandAnnotation freehand)
        {
            return freehand.Points.Count >= 2;
        }

        // Text and Emoji don't need size validation
        return true;
    }
}
