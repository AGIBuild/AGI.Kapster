using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Serilog;
using AGI.Kapster.Desktop.Services.Overlay;
using AGI.Kapster.Desktop.Services.Overlay.State;

namespace AGI.Kapster.Desktop.Overlays;

public sealed class SelectionOverlay : Canvas
{
    private Rect _selectionRect;
    private Point _dragStart;
    private Point _moveStart;
    private bool _isDraggingCreate;
    private bool _isDraggingMove;
    private HandleKind _activeHandle = HandleKind.None;
    private readonly Border _rectBorder;
    private readonly SelectionInfoOverlay _infoOverlay;
    private bool _pendingCreate;
    private const double DragThreshold = 0.5; // Reduced from 2.0 to 0.5 pixels for better responsiveness

    private const double HandleSize = 8;
    private const double HandleHit = 10;

    private enum HandleKind { None, N, S, E, W, NE, NW, SE, SW }

    public Rect SelectionRect
    {
        get => _selectionRect;
        private set { _selectionRect = value; }
    }

    public event Action<Rect>? SelectionFinished;
    public event Action<Rect>? ConfirmRequested;
    public event Action<Rect>? SelectionChanged;

    public SelectionOverlay()
    {
        Cursor = new Cursor(StandardCursorType.Cross);
        Focusable = true;
        Background = Brushes.Transparent;
        _rectBorder = new Border
        {
            BorderBrush = Brushes.DeepSkyBlue,
            BorderThickness = new Thickness(2),
            IsHitTestVisible = false
        };
        Children.Add(_rectBorder);

        // Create info overlay
        _infoOverlay = new SelectionInfoOverlay();
        Children.Add(_infoOverlay);

        // Subscribe to session selection state changes (will be wired up when session is set)
    }
    
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        
        // Wire up to session when attached to window
        var parentWindow = this.FindAncestorOfType<OverlayWindow>();
        var session = parentWindow?.GetSession();
        if (session != null)
        {
            session.SelectionStateChanged += OnSessionSelectionStateChanged;
            Log.Debug("SelectionOverlay attached to session");
        }
    }
    
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        
        // Unsubscribe from session when detached
        var parentWindow = this.FindAncestorOfType<OverlayWindow>();
        var session = parentWindow?.GetSession();
        if (session != null)
        {
            session.SelectionStateChanged -= OnSessionSelectionStateChanged;
            Log.Debug("SelectionOverlay detached from session");
        }
    }

    private void OnSessionSelectionStateChanged(bool hasSelection)
    {
        var parentWindow = this.FindAncestorOfType<OverlayWindow>();
        var session = parentWindow?.GetSession();
        var isActiveWindow = session?.ActiveSelectionWindow == parentWindow;

        // Update cursor based on selection state
        if (hasSelection && !isActiveWindow)
        {
            Cursor = new Cursor(StandardCursorType.No); // Show "not allowed" cursor
            Log.Information("SelectionOverlay: Disabled cursor on inactive window");
        }
        else
        {
            Cursor = new Cursor(StandardCursorType.Cross); // Normal selection cursor
            Log.Information("SelectionOverlay: Enabled cursor on active/available window");
        }
    }

    // Rely on default hit testing; the control fills the Grid

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var p = e.GetPosition(this);
        Log.Information("SelectionOverlay PointerPressed at {X},{Y}", p.X, p.Y);
        Focus();

        // Check if another overlay window already has a selection
        var parentWindow = this.FindAncestorOfType<OverlayWindow>();
        var session = parentWindow?.GetSession();
        if (parentWindow != null && session != null && !session.CanStartSelection(parentWindow))
        {
            Log.Information("SelectionOverlay: Cannot start selection, another window has active selection");
            e.Handled = true;
            return;
        }
        // Design: Only when a selection exists and cursor is in 'select' mode (outside selection), double-click confirms.
        var hasSelection = SelectionRect.Width >= 2 && SelectionRect.Height >= 2;
        var clickOutsideSelection = hasSelection && !SelectionRect.Contains(p);
        if (e.ClickCount == 2 && hasSelection && clickOutsideSelection)
        {
            ConfirmRequested?.Invoke(SelectionRect);
            _pendingCreate = false;
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }
        if (SelectionRect.Contains(p))
        {
            var handle = HitTestHandle(p);
            if (handle != HandleKind.None)
            {
                _activeHandle = handle;
                _dragStart = p;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }
            _isDraggingMove = true;
            _moveStart = p;
            Log.Information("SelectionOverlay MoveStart at {X},{Y}", p.X, p.Y);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }
        _pendingCreate = true;
        _dragStart = p;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);

        // Handle different interaction modes
        if (HandleDragCreation(p))
        {
            // Drag creation handled
        }
        else if (HandleDragMove(p))
        {
            // Drag move handled
        }
        else if (HandleResize(p))
        {
            // Resize handled
        }
        else
        {
            // Handle cursor updates and info overlay for hover state
            HandleHoverState(p);
        }

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var p = e.GetPosition(this);

        // Check if this was a click (not a drag) for fullscreen selection
        // If _pendingCreate is true but _isDraggingCreate is false, it means the pointer
        // never moved enough to trigger drag creation - this is a click
        var hasExistingSelection = SelectionRect.Width >= 2 && SelectionRect.Height >= 2;
        if (_pendingCreate && !_isDraggingCreate && !hasExistingSelection)
        {
            Log.Information("SelectionOverlay: Click detected, creating fullscreen selection");
            
            // Set selection to entire canvas (fullscreen)
            SelectionRect = new Rect(0, 0, Bounds.Width, Bounds.Height);
            UpdateVisuals();

            // Set selection state in session
            var parentWindow = this.FindAncestorOfType<OverlayWindow>();
            var session = parentWindow?.GetSession();
            if (parentWindow != null && session != null)
            {
                session.SetSelection(parentWindow);
            }

            // Show info overlay
            _infoOverlay.Show();
            UpdateInfoOverlay(p);

            // Reset state
            _isDraggingCreate = false;
            _isDraggingMove = false;
            _activeHandle = HandleKind.None;
            _pendingCreate = false;
            e.Pointer.Capture(null);
            e.Handled = true;

            // Fire selection finished event
            SelectionFinished?.Invoke(SelectionRect);
            return; // Early return to avoid duplicate processing
        }

        _isDraggingCreate = false;
        _isDraggingMove = false;
        _activeHandle = HandleKind.None;
        _pendingCreate = false;
        e.Pointer.Capture(null);
        e.Handled = true;

        if (SelectionRect.Width >= 2 && SelectionRect.Height >= 2)
        {
            // Keep showing both size and color info after selection is finished
            // Ensure info overlay is visible and update with current mouse position
            _infoOverlay.Show();
            UpdateInfoOverlay(e.GetPosition(this));

            SelectionFinished?.Invoke(SelectionRect);
        }
        else
        {
            // Hide info overlay if selection is too small
            _infoOverlay.Hide();
        }
    }

    private void UpdateVisuals()
    {
        // Directly update visual elements without throttling
        if (SelectionRect.Width > 0 && SelectionRect.Height > 0)
        {
            _rectBorder.Width = SelectionRect.Width;
            _rectBorder.Height = SelectionRect.Height;
            Canvas.SetLeft(_rectBorder, SelectionRect.X);
            Canvas.SetTop(_rectBorder, SelectionRect.Y);
            _rectBorder.IsVisible = true;
        }
        else
        {
            _rectBorder.IsVisible = false;
        }
        SelectionChanged?.Invoke(SelectionRect);
    }

    private static Rect Normalize(Rect r)
    {
        var x = Math.Min(r.X, r.Right);
        var y = Math.Min(r.Y, r.Bottom);
        var w = Math.Abs(r.Width);
        var h = Math.Abs(r.Height);
        return new Rect(x, y, w, h);
    }

    private HandleKind HitTestHandle(Point p)
    {
        if (SelectionRect.Width <= 0 || SelectionRect.Height <= 0) return HandleKind.None;
        var (x, y, r, b, cx, cy) = (SelectionRect.X, SelectionRect.Y, SelectionRect.Right, SelectionRect.Bottom, SelectionRect.Center.X, SelectionRect.Center.Y);
        if (Distance(p, new Point(x, y)) <= HandleHit) return HandleKind.NW;
        if (Distance(p, new Point(r, y)) <= HandleHit) return HandleKind.NE;
        if (Distance(p, new Point(x, b)) <= HandleHit) return HandleKind.SW;
        if (Distance(p, new Point(r, b)) <= HandleHit) return HandleKind.SE;
        if (Math.Abs(p.X - x) <= HandleHit && Math.Abs(p.Y - cy) <= HandleHit) return HandleKind.W;
        if (Math.Abs(p.X - r) <= HandleHit && Math.Abs(p.Y - cy) <= HandleHit) return HandleKind.E;
        if (Math.Abs(p.Y - y) <= HandleHit && Math.Abs(p.X - cx) <= HandleHit) return HandleKind.N;
        if (Math.Abs(p.Y - b) <= HandleHit && Math.Abs(p.X - cx) <= HandleHit) return HandleKind.S;
        return HandleKind.None;
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X; var dy = a.Y - b.Y; return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Sets the selection rectangle programmatically (used by element picker)
    /// </summary>
    /// <param name="rect">The selection rectangle to set</param>
    public void SetSelection(Rect rect)
    {
        SelectionRect = rect;
        UpdateVisuals(); // Force update for programmatic changes

        // Set selection state in session
        var parentWindow = this.FindAncestorOfType<OverlayWindow>();
        var session = parentWindow?.GetSession();
        if (parentWindow != null && session != null)
        {
            session.SetSelection(parentWindow);
        }

        // Fire events
        SelectionFinished?.Invoke(SelectionRect);
        Log.Information("SelectionOverlay: Selection set programmatically to {Rect}", rect);
    }

    private static Point[] HandlePoints(Rect r)
    {
        return new[]
        {
            new Point(r.X, r.Y), // NW
            new Point(r.Right, r.Y), // NE
            new Point(r.X, r.Bottom), // SW
            new Point(r.Right, r.Bottom), // SE
            new Point(r.X, r.Center.Y), // W
            new Point(r.Right, r.Center.Y), // E
            new Point(r.Center.X, r.Y), // N
            new Point(r.Center.X, r.Bottom) // S
        };
    }

    /// <summary>
    /// Update the info overlay with current selection and mouse position
    /// </summary>
    private void UpdateInfoOverlay(Point mousePosition)
    {
        // Always update info overlay when there's a selection
        if (SelectionRect.Width > 0 && SelectionRect.Height > 0)
        {
            _infoOverlay.UpdateInfo(SelectionRect, mousePosition);
        }
    }

    #region Pointer Movement Handlers

    /// <summary>
    /// Handle drag creation logic
    /// </summary>
    /// <param name="p">Current pointer position</param>
    /// <returns>True if drag creation was handled</returns>
    private bool HandleDragCreation(Point p)
    {
        // Check if we should start drag creation
        if (_pendingCreate && !_isDraggingCreate)
        {
            var delta = p - _dragStart;
            if (Math.Abs(delta.X) >= DragThreshold || Math.Abs(delta.Y) >= DragThreshold)
            {
                StartDragCreation();
                return true;
            }
            return false;
        }

        // Handle ongoing drag creation
        if (_isDraggingCreate)
        {
            SelectionRect = Normalize(new Rect(_dragStart, p));
            UpdateVisualsAndInfo(p, showInfo: true);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handle drag move logic
    /// </summary>
    /// <param name="p">Current pointer position</param>
    /// <returns>True if drag move was handled</returns>
    private bool HandleDragMove(Point p)
    {
        if (_isDraggingMove)
        {
            var delta = p - _moveStart;
            _moveStart = p;
            SelectionRect = new Rect(SelectionRect.Position + delta, SelectionRect.Size);
            UpdateVisualsAndInfo(p);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Handle resize logic
    /// </summary>
    /// <param name="p">Current pointer position</param>
    /// <returns>True if resize was handled</returns>
    private bool HandleResize(Point p)
    {
        if (_activeHandle != HandleKind.None)
        {
            var newRect = CalculateResizedRect(p);
            SelectionRect = newRect;
            UpdateVisualsAndInfo(p);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Handle hover state (cursor updates and info overlay)
    /// </summary>
    /// <param name="p">Current pointer position</param>
    private void HandleHoverState(Point p)
    {
        UpdateCursor(p);
        UpdateInfoOverlayIfNeeded(p);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Start drag creation and set global selection state
    /// </summary>
    private void StartDragCreation()
    {
        _isDraggingCreate = true;
        SelectionRect = new Rect(_dragStart, _dragStart);

        // Set selection state in session when drag starts
        var parentWindow = this.FindAncestorOfType<OverlayWindow>();
        var session = parentWindow?.GetSession();
        if (parentWindow != null && session != null)
        {
            session.SetSelection(parentWindow);
            Log.Information("SelectionOverlay: Started selection on window");
        }
    }

    /// <summary>
    /// Update visuals and info overlay
    /// </summary>
    /// <param name="p">Current pointer position</param>
    /// <param name="showInfo">Whether to show info overlay</param>
    private void UpdateVisualsAndInfo(Point p, bool showInfo = false)
    {
        UpdateVisuals();
        if (showInfo)
        {
            _infoOverlay.Show();
        }
        UpdateInfoOverlay(p);
    }

    /// <summary>
    /// Calculate resized rectangle based on active handle and pointer position
    /// </summary>
    /// <param name="p">Current pointer position</param>
    /// <returns>New rectangle after resize</returns>
    private Rect CalculateResizedRect(Point p)
    {
        var r = SelectionRect;
        return _activeHandle switch
        {
            HandleKind.N => new Rect(r.X, Math.Min(p.Y, r.Bottom - 1), r.Width, Math.Max(1, r.Bottom - Math.Min(p.Y, r.Bottom - 1))),
            HandleKind.S => new Rect(r.X, r.Y, r.Width, Math.Max(1, p.Y - r.Y)),
            HandleKind.W => new Rect(Math.Min(p.X, r.Right - 1), r.Y, Math.Max(1, r.Right - Math.Min(p.X, r.Right - 1)), r.Height),
            HandleKind.E => new Rect(r.X, r.Y, Math.Max(1, p.X - r.X), r.Height),
            HandleKind.NW => Normalize(new Rect(p, r.BottomRight)),
            HandleKind.NE => Normalize(new Rect(new Point(r.X, p.Y), new Point(p.X, r.Bottom))),
            HandleKind.SW => Normalize(new Rect(new Point(p.X, r.Y), new Point(r.Right, p.Y))),
            HandleKind.SE => Normalize(new Rect(r.Position, p)),
            _ => r
        };
    }

    /// <summary>
    /// Update cursor based on pointer position and selection state
    /// </summary>
    /// <param name="p">Current pointer position</param>
    private void UpdateCursor(Point p)
    {
        var handle = HitTestHandle(p);
        var hasSel = SelectionRect.Width >= 2 && SelectionRect.Height >= 2;

        if (handle != HandleKind.None)
        {
            Cursor = handle switch
            {
                HandleKind.N or HandleKind.S => new Cursor(StandardCursorType.SizeNorthSouth),
                HandleKind.E or HandleKind.W => new Cursor(StandardCursorType.SizeWestEast),
                _ => new Cursor(StandardCursorType.SizeAll)
            };
        }
        else if (hasSel && !SelectionRect.Contains(p))
        {
            // Selection exists and pointer is outside -> selection mode
            Cursor = new Cursor(StandardCursorType.Arrow);
        }
        else if (hasSel && SelectionRect.Contains(p))
        {
            Cursor = new Cursor(StandardCursorType.SizeAll);
        }
        else
        {
            // No selection -> draw mode
            Cursor = new Cursor(StandardCursorType.Cross);
        }
    }

    /// <summary>
    /// Update info overlay if selection exists
    /// </summary>
    /// <param name="p">Current pointer position</param>
    private void UpdateInfoOverlayIfNeeded(Point p)
    {
        var hasSel = SelectionRect.Width >= 2 && SelectionRect.Height >= 2;
        if (hasSel)
        {
            UpdateInfoOverlay(p);
        }
    }

    #endregion
}


