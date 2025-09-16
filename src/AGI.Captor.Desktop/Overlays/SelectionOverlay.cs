using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Serilog;
using AGI.Captor.Desktop.Services;

namespace AGI.Captor.Desktop.Overlays;

public sealed class SelectionOverlay : Canvas
{
    private Rect _selectionRect;
    private Point _dragStart;
    private Point _moveStart;
    private bool _isDraggingCreate;
    private bool _isDraggingMove;
    private HandleKind _activeHandle = HandleKind.None;
    private readonly Border _rectBorder;
    private bool _pendingCreate;
    private const double DragThreshold = 0.5; // 从2.0降低到0.5像素，提高响应速度
    
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

        // Subscribe to global selection state changes
        GlobalSelectionState.SelectionStateChanged += OnGlobalSelectionStateChanged;
    }

    private void OnGlobalSelectionStateChanged(bool hasSelection)
    {
        var parentWindow = this.FindAncestorOfType<OverlayWindow>();
        var isActiveWindow = GlobalSelectionState.ActiveSelectionWindow == parentWindow;
        
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
        if (parentWindow != null && !GlobalSelectionState.CanStartSelection(parentWindow))
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
        if (_pendingCreate && !_isDraggingCreate)
        {
            var delta = p - _dragStart;
            if (Math.Abs(delta.X) >= DragThreshold || Math.Abs(delta.Y) >= DragThreshold)
            {
                _isDraggingCreate = true;
                SelectionRect = new Rect(_dragStart, _dragStart);
                
                // Set global selection state when drag starts
                var parentWindow = this.FindAncestorOfType<OverlayWindow>();
                if (parentWindow != null)
                {
                    GlobalSelectionState.SetSelection(parentWindow);
                    Log.Information("SelectionOverlay: Started selection on window");
                }
            }
        }

        if (_isDraggingCreate)
        {
            // Free draw from press point to current pointer
            SelectionRect = Normalize(new Rect(_dragStart, p));
            UpdateVisuals();
        }
        else if (_isDraggingMove)
        {
            var delta = p - _moveStart;
            _moveStart = p;
            SelectionRect = new Rect(SelectionRect.Position + delta, SelectionRect.Size);
            UpdateVisuals();
        }
        else if (_activeHandle != HandleKind.None)
        {
            var r = SelectionRect;
            switch (_activeHandle)
            {
                case HandleKind.N: r = new Rect(r.X, Math.Min(p.Y, r.Bottom - 1), r.Width, Math.Max(1, r.Bottom - Math.Min(p.Y, r.Bottom - 1))); break;
                case HandleKind.S: r = new Rect(r.X, r.Y, r.Width, Math.Max(1, p.Y - r.Y)); break;
                case HandleKind.W: r = new Rect(Math.Min(p.X, r.Right - 1), r.Y, Math.Max(1, r.Right - Math.Min(p.X, r.Right - 1)), r.Height); break;
                case HandleKind.E: r = new Rect(r.X, r.Y, Math.Max(1, p.X - r.X), r.Height); break;
                case HandleKind.NW: r = Normalize(new Rect(p, r.BottomRight)); break;
                case HandleKind.NE: r = Normalize(new Rect(new Point(r.X, p.Y), new Point(p.X, r.Bottom))); break;
                case HandleKind.SW: r = Normalize(new Rect(new Point(p.X, r.Y), new Point(r.Right, p.Y))); break;
                case HandleKind.SE: r = Normalize(new Rect(r.Position, p)); break;
            }
            SelectionRect = r;
            UpdateVisuals();
        }
        else
        {
            // update cursor on hover
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
                // selection exists and pointer is outside -> selection mode
                Cursor = new Cursor(StandardCursorType.Arrow);
            }
            else if (hasSel && SelectionRect.Contains(p))
            {
                Cursor = new Cursor(StandardCursorType.SizeAll);
            }
            else
            {
                // no selection -> draw mode
                Cursor = new Cursor(StandardCursorType.Cross);
            }
        }
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isDraggingCreate = false;
        _isDraggingMove = false;
        _activeHandle = HandleKind.None;
        _pendingCreate = false;
        e.Pointer.Capture(null);
        e.Handled = true;

        if (SelectionRect.Width >= 2 && SelectionRect.Height >= 2)
        {
            SelectionFinished?.Invoke(SelectionRect);
        }
    }

    private void UpdateVisuals()
    {
        // 直接更新视觉元素，无节流
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
        
        // Set global selection state
        var parentWindow = this.FindAncestorOfType<OverlayWindow>();
        if (parentWindow != null)
        {
            GlobalSelectionState.SetSelection(parentWindow);
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
}


