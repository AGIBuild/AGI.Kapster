using AGI.Captor.App.Models;
using AGI.Captor.App.Rendering;
using AGI.Captor.App.Services;
using AGI.Captor.App.Commands;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AGI.Captor.App.Overlays;

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
    private readonly IExportService _exportService;
    private IAnnotationItem? _creatingItem;
    private Point _startPoint;
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
    public Rect SelectionRect
    {
        get => _selectionRect;
        set
        {
            _selectionRect = value;
            UpdateHitTestVisibility();
            // Update cursor when selection changes
            UpdateCursor();
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

    public NewAnnotationOverlay(ISettingsService? settingsService)
    {
        _annotationService = new AnnotationService(settingsService);
        _renderer = new AnnotationRenderer();
        _commandManager = new CommandManager();
        _exportService = new ExportService();
        
        Background = Brushes.Transparent;
        IsHitTestVisible = false; // Start as non-interactive
        
        // Subscribe to annotation events
        _annotationService.Manager.ItemChanged += OnItemChanged;
        _annotationService.Manager.SelectionChanged += OnSelectionChanged;
        _annotationService.ToolChanged += OnToolChanged;
        
        UpdateCursor();
        
        // Enable keyboard focus for shortcuts
        Focusable = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        try
        {
            switch (e.Key)
            {
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

                case Key.A when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                    // Select all annotations
                    _annotationService.Manager.SelectAll();
                    e.Handled = true;
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
            
            var point = e.GetPosition(this);
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
                // Handle double-click outside selection for confirm (legacy behavior)
                if (e.ClickCount == 2 && hasSelection && !pointInSelection)
                {
                    ConfirmRequested?.Invoke(_selectionRect);
                    e.Handled = true;
                    return;
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
                    // Check for double-click on text annotation to enter editing mode
                    if (e.ClickCount == 2)
                    {
                        var hitItem = _annotationService.HitTest(point);
                        if (hitItem is TextAnnotation textItem)
                        {
                            // Ensure text is selected
                            if (!_annotationService.Manager.SelectedItems.Contains(textItem))
                            {
                                _annotationService.Manager.ClearSelection();
                                _annotationService.Manager.SelectItem(textItem);
                            }
                            
                            StartTextEditing(textItem);
                            e.Handled = true;
                            return;
                        }
                    }
                    
                    // Selection mode - for selecting/editing existing annotations
                    HandleSelectionPress(point, e.KeyModifiers.HasFlag(KeyModifiers.Control));
                }
                else
                {
                    // Creation mode - for creating new annotations
                    HandleCreationPress(point);
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
        
        var point = e.GetPosition(this);
        
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
            // Drawing tool selected and inside selection - FORCE cross cursor
            Cursor = new Cursor(StandardCursorType.Cross);
            Log.Information("OnPointerMoved: FORCING Cross cursor - CurrentTool={CurrentTool}, InSelection={InSelection}", CurrentTool, pointInSelection);
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
            _annotationService.UpdateCreate(point, _creatingItem);
            RefreshRender();
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
        
        if (_isCreating && _creatingItem != null)
        {
            _annotationService.CancelCreate(_creatingItem);
            _creatingItem = null;
            _isCreating = false;
            RefreshRender();
        }
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
                var textItem = _annotationService.StartCreate(point) as TextAnnotation;
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
                var emojiItem = _annotationService.StartCreate(point) as EmojiAnnotation;
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
            _startPoint = point;
            _creatingItem = _annotationService.StartCreate(point);
            
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
        const double maxDistance = handleSize/2 + tolerance;
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

        var delta = currentPoint - _dragStartPoint;
        var selectedItems = _annotationService.Manager.SelectedItems;

        foreach (var item in selectedItems)
        {
            item.Move(delta);
        }

        _dragStartPoint = currentPoint;
        RefreshRender();
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
        var delta = currentPoint - _dragStartPoint;
        var newBounds = CalculateNewBounds(_dragStartBounds, _activeResizeHandle, delta);

        // Apply transformation based on annotation type
        ApplyTransformation(item, _dragStartBounds, newBounds);
        RefreshRender();
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
            UpdateCursor();
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
            UpdateCursor();
            
            // Force cursor update when tool changes
            Log.Information("OnToolChanged: {OldTool} -> {NewTool}, forcing cursor update", e.OldTool, e.NewTool);
            
            // Simple force: if we have a drawing tool and selection, always set cross cursor
            var hasSelection = _selectionRect.Width >= 2 && _selectionRect.Height >= 2;
            
            if (hasSelection && CurrentTool != AnnotationToolType.None)
            {
                Cursor = new Cursor(StandardCursorType.Cross);
                Log.Information("OnToolChanged: FORCED Cross cursor for tool {Tool}", CurrentTool);
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
            
            // Note: ESC key handling is done at OverlayWindow level
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnToolChanged from {OldTool} to {NewTool}", e.OldTool, e.NewTool);
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
    /// 清除所有标注
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
    /// 删除选中的标注
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
    /// 获取标注服务（用于外部访问）
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
    /// 开始编辑文本标注
    /// </summary>
    private void StartTextEditing(TextAnnotation textItem)
    {
        try
        {
            // 如果已经在编辑其他文本，先结束编辑
            EndTextEditing();

            _editingTextItem = textItem;
            
            // CRITICAL FIX: Force remove existing text render before entering edit mode
            // This prevents ghosting during text editing
            _renderer.RemoveRender(this, textItem);
            Log.Information("Removed existing render for text annotation {Id} before entering edit mode", textItem.Id);
            
            textItem.StartEditing();

            // CRITICAL FIX: Immediately refresh render to remove original text display
            // This prevents ghosting during text editing
            RefreshRender();

            // 获取文本选中状态的边界大小（与选中状态显示一致）
            var bounds = textItem.Bounds; // 这会返回选中状态的编辑边界
            var actualWidth = bounds.Width;
            var actualHeight = bounds.Height;
            
            // 创建文本编辑框 - 精确匹配TextBlock的渲染位置和尺寸
            _editingTextBox = new TextBox
            {
                Text = textItem.Text,
                FontFamily = new Avalonia.Media.FontFamily(textItem.Style.FontFamily),
                FontSize = textItem.Style.FontSize,
                FontWeight = (Avalonia.Media.FontWeight)textItem.Style.FontWeight,
                FontStyle = (Avalonia.Media.FontStyle)textItem.Style.FontStyle,
                Foreground = new SolidColorBrush(textItem.Style.StrokeColor),
                Background = new SolidColorBrush(Colors.White, 0.9), // 半透明背景便于编辑
                BorderBrush = new SolidColorBrush(textItem.Style.StrokeColor),
                BorderThickness = new Avalonia.Thickness(1),
                // CRITICAL: Remove padding and margin to match TextBlock rendering exactly
                Padding = new Avalonia.Thickness(0),
                Margin = new Avalonia.Thickness(0),
                // 使用文本的实际尺寸，而不是选区尺寸
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

            // 设置位置 - 考虑边框偏移量以精确对齐
            // TextBox有1px边框，需要向左上角偏移1px以对齐TextBlock
            Canvas.SetLeft(_editingTextBox, textItem.Position.X - 1);
            Canvas.SetTop(_editingTextBox, textItem.Position.Y - 1);

            // 添加到画布
            Children.Add(_editingTextBox);

            // 处理编辑完成事件
            _editingTextBox.LostFocus += OnTextEditingLostFocus;
            _editingTextBox.KeyDown += OnTextEditingKeyDown;
            _editingTextBox.TextChanged += OnTextEditingTextChanged;

            // 聚焦并选中所有文本
            _editingTextBox.Focus();
            _editingTextBox.SelectAll();
            
            Log.Information("Started text editing for annotation {Id}", textItem.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start text editing for annotation {Id}", textItem.Id);
            
            // 清理状态
            _editingTextBox = null;
            _editingTextItem = null;
            textItem.EndEditing();
        }
    }

    /// <summary>
    /// 文本编辑框内容变化事件 - 实现自动扩展
    /// </summary>
    private void OnTextEditingTextChanged(object? sender, TextChangedEventArgs e)
    {
        try
        {
            if (_editingTextBox == null || _editingTextItem == null) return;

            // 测量文本尺寸
            var formattedText = new Avalonia.Media.FormattedText(
                _editingTextBox.Text ?? "",
                System.Globalization.CultureInfo.CurrentCulture,
                Avalonia.Media.FlowDirection.LeftToRight,
                new Avalonia.Media.Typeface(_editingTextItem.Style.FontFamily, 
                                            (Avalonia.Media.FontStyle)_editingTextItem.Style.FontStyle,
                                            (Avalonia.Media.FontWeight)_editingTextItem.Style.FontWeight),
                _editingTextItem.Style.FontSize,
                Brushes.Black);

            // 计算新的尺寸（添加一些编辑空间）
            var newWidth = Math.Max(formattedText.Width + 20, 100);
            var newHeight = Math.Max(formattedText.Height + 8, _editingTextItem.Style.FontSize + 8);

            // 更新编辑框尺寸
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
    /// 结束文本编辑
    /// </summary>
    private void EndTextEditing()
    {
        if (_editingTextBox == null || _editingTextItem == null) return;

        try
        {
            // 更新文本内容
            var finalText = _editingTextBox.Text ?? string.Empty;
            _editingTextItem.Text = finalText;
            
            // 移除编辑框
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
            // 确保清理状态
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
    /// 文本编辑框失去焦点事件
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
    /// 文本编辑框按键事件
    /// </summary>
    private void OnTextEditingKeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            if (e.Key == Key.Escape)
            {
                // ESC键取消编辑，恢复原文本
                if (_editingTextItem != null && _editingTextBox != null)
                {
                    _editingTextBox.Text = _editingTextItem.Text;
                }
                EndTextEditing();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                // Enter键结束编辑（Shift+Enter换行）
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
