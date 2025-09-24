using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

namespace AGI.Kapster.Desktop.Overlays;

public enum AnnotationTool
{
    None,
    Text,
    Rectangle,
    Ellipse,
    Arrow
}

public sealed class AnnotationOverlay : Canvas
{
    private Rect _selectionRect;
    public Rect SelectionRect
    {
        get => _selectionRect;
        set
        {
            _selectionRect = value;
            UpdateHitTestVisibility();
        }
    }

    private AnnotationTool _currentTool = AnnotationTool.None;
    public AnnotationTool CurrentTool
    {
        get => _currentTool;
        set
        {
            _currentTool = value;
            Cursor = _currentTool == AnnotationTool.None
                ? new Cursor(StandardCursorType.Arrow)
                : new Cursor(StandardCursorType.Cross);
        }
    }
    public IBrush StrokeBrush { get; set; } = Brushes.Red;
    public double StrokeThickness { get; set; } = 3;
    public IBrush FillBrush { get; set; } = Brushes.Transparent;
    public double TextFontSize { get; set; } = 18;

    private Shape? _activeShape;
    private Canvas? _activeArrow; // contains Line + Polygon
    private Point _startPoint;
    private TextBox? _editingBox;

    public event Action<Rect>? ConfirmRequested;

    public AnnotationOverlay()
    {
        Background = Brushes.Transparent;
        // Disabled by default; enabled when selection exists and tool chosen
        IsHitTestVisible = false;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var p = e.GetPosition(this);

        // If selection exists and user double-clicks outside selection -> confirm save
        var hasSelection = SelectionRect.Width >= 2 && SelectionRect.Height >= 2;
        if (e.ClickCount == 2 && hasSelection && !SelectionRect.Contains(p))
        {
            ConfirmRequested?.Invoke(SelectionRect);
            e.Handled = true;
            return;
        }

        if (CurrentTool == AnnotationTool.None) return;
        if (!SelectionRect.Contains(p)) return;

        e.Handled = true;
        Focus();
        e.Pointer.Capture(this);
        _startPoint = ClampToSelection(p);

        switch (CurrentTool)
        {
            case AnnotationTool.Rectangle:
                {
                    var rect = new Rectangle
                    {
                        Stroke = StrokeBrush,
                        StrokeThickness = StrokeThickness,
                        Fill = Brushes.Transparent
                    };
                    Children.Add(rect);
                    _activeShape = rect;
                    UpdateRect(rect, _startPoint, _startPoint);
                    break;
                }
            case AnnotationTool.Ellipse:
                {
                    var el = new Ellipse
                    {
                        Stroke = StrokeBrush,
                        StrokeThickness = StrokeThickness,
                        Fill = Brushes.Transparent
                    };
                    Children.Add(el);
                    _activeShape = el;
                    UpdateRect(el, _startPoint, _startPoint);
                    break;
                }
            case AnnotationTool.Arrow:
                {
                    var container = CreateArrow(_startPoint, _startPoint);
                    Children.Add(container);
                    _activeArrow = container;
                    break;
                }
            case AnnotationTool.Text:
                {
                    // Start a text box for input
                    _editingBox = new TextBox
                    {
                        FontSize = TextFontSize,
                        Foreground = StrokeBrush,
                        Background = Brushes.Transparent,
                        BorderBrush = Brushes.Transparent,
                        Width = 200
                    };
                    SetLeft(_editingBox, _startPoint.X);
                    SetTop(_editingBox, _startPoint.Y);
                    Children.Add(_editingBox);
                    _editingBox.Focus();
                    _editingBox.LostFocus += CommitText;
                    _editingBox.KeyDown += (s, ev) =>
                    {
                        if (ev.Key == Key.Enter)
                        {
                            CommitText(s!, EventArgs.Empty);
                            ev.Handled = true;
                        }
                    };
                    break;
                }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);
        Cursor = (CurrentTool != AnnotationTool.None && SelectionRect.Contains(pos))
            ? new Cursor(StandardCursorType.Cross)
            : new Cursor(StandardCursorType.Arrow);

        if (_activeShape is null && _activeArrow is null) return;
        var p = ClampToSelection(pos);
        e.Handled = true;

        if (_activeShape is Rectangle r)
        {
            UpdateRect(r, _startPoint, p);
        }
        else if (_activeShape is Ellipse el)
        {
            UpdateRect(el, _startPoint, p);
        }
        else if (_activeArrow is not null)
        {
            UpdateArrow(_activeArrow, _startPoint, p);
        }
    }

    private void UpdateHitTestVisibility()
    {
        var enable = _currentTool != AnnotationTool.None && _selectionRect.Width >= 2 && _selectionRect.Height >= 2;
        IsHitTestVisible = enable;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }
        _activeShape = null;
        _activeArrow = null;
        e.Handled = true;
    }

    private void CommitText(object? sender, EventArgs e)
    {
        if (_editingBox is null) return;
        var text = _editingBox.Text;
        var left = GetLeft(_editingBox);
        var top = GetTop(_editingBox);
        Children.Remove(_editingBox);
        _editingBox = null;
        if (!string.IsNullOrWhiteSpace(text))
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = TextFontSize,
                Foreground = StrokeBrush
            };
            SetLeft(tb, left);
            SetTop(tb, top);
            Children.Add(tb);
        }
    }

    private static void UpdateRect(Shape shape, Point a, Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        var w = Math.Abs(a.X - b.X);
        var h = Math.Abs(a.Y - b.Y);
        SetLeft(shape, x);
        SetTop(shape, y);
        shape.Width = w;
        shape.Height = h;
    }

    private static Canvas CreateArrow(Point a, Point b)
    {
        var container = new Canvas();
        var shadow = new Path { IsHitTestVisible = false }; // drop shadow under arrow
        var body = new Path(); // combined shaft + head
        container.Children.Add(shadow); // index 0
        container.Children.Add(body);   // index 1
        return container;
    }

    private void UpdateArrow(Canvas container, Point a, Point b)
    {
        if (container.Children.Count < 2) return;
        var shadow = (Path)container.Children[0];
        var body = (Path)container.Children[1];

        var dx = b.X - a.X; var dy = b.Y - a.Y;
        var len = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
        if (len < 5) return; // too short to draw

        var ux = dx / len; var uy = dy / len; // unit vector along arrow
        var px = -uy; var py = ux; // perpendicular vector

        // Refined arrow dimensions - wider base, shorter head
        var headLen = Math.Min(len * 0.2, Math.Max(10, StrokeThickness * 4.5));
        var headWidth = Math.Max(14, StrokeThickness * 7.0);
        var tailWidth = Math.Max(StrokeThickness * 3.5, 12.0);
        var baseWidth = Math.Max(StrokeThickness * 0.8, 2.5);

        // Arrow curve configuration - adjustable parameters
        const double CurveIntensityFactor = 0.12; // Curve strength as percentage of arrow length
        const double MaxCurveAmount = 25; // Maximum curve offset in pixels
        const double StraightAngleThreshold = 15; // Degrees within which arrows remain straight

        // Dynamic curve control - curve towards screen edges
        var midPoint = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);

        // Check if arrow is mostly horizontal or vertical
        var angle = Math.Atan2(Math.Abs(dy), Math.Abs(dx)) * 180 / Math.PI;
        var isHorizontal = angle < StraightAngleThreshold; // mostly horizontal
        var isVertical = angle > (90 - StraightAngleThreshold); // mostly vertical

        Point curveControl;
        if (isHorizontal || isVertical)
        {
            // No curve for horizontal/vertical arrows
            curveControl = midPoint;
        }
        else
        {
            var curveAmount = Math.Min(len * CurveIntensityFactor, MaxCurveAmount);

            // Determine which screen edge to curve towards
            // For diagonal arrows, curve towards the "outside" edge
            double curveOffsetX = 0, curveOffsetY = 0;

            if (dx > 0 && dy < 0) // Right-up direction
            {
                // Curve towards left-up (negative X, negative Y)
                curveOffsetX = -curveAmount;
                curveOffsetY = -curveAmount;
            }
            else if (dx > 0 && dy > 0) // Right-down direction  
            {
                // Curve towards left-down (negative X, positive Y)
                curveOffsetX = -curveAmount;
                curveOffsetY = curveAmount;
            }
            else if (dx < 0 && dy < 0) // Left-up direction
            {
                // Curve towards right-up (positive X, negative Y)
                curveOffsetX = curveAmount;
                curveOffsetY = -curveAmount;
            }
            else if (dx < 0 && dy > 0) // Left-down direction
            {
                // Curve towards right-down (positive X, positive Y)
                curveOffsetX = curveAmount;
                curveOffsetY = curveAmount;
            }

            curveControl = new Point(midPoint.X + curveOffsetX, midPoint.Y + curveOffsetY);
        }

        // Generate smooth curved shaft using quadratic bezier sampling
        int steps = Math.Max(16, (int)(len / 8)); // more points for smoother curves
        var leftPoints = new List<Point>(steps + 1);
        var rightPoints = new List<Point>(steps + 1);

        var headBase = new Point(b.X - ux * headLen, b.Y - uy * headLen);

        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            double omt = 1 - t;

            // Quadratic bezier curve for centerline
            var centerX = omt * omt * a.X + 2 * omt * t * curveControl.X + t * t * headBase.X;
            var centerY = omt * omt * a.Y + 2 * omt * t * curveControl.Y + t * t * headBase.Y;

            // Tangent direction for perpendicular calculation
            var tangentX = 2 * omt * (curveControl.X - a.X) + 2 * t * (headBase.X - curveControl.X);
            var tangentY = 2 * omt * (curveControl.Y - a.Y) + 2 * t * (headBase.Y - curveControl.Y);
            var tangentLen = Math.Max(1e-6, Math.Sqrt(tangentX * tangentX + tangentY * tangentY));
            var normalX = -tangentY / tangentLen;
            var normalY = tangentX / tangentLen;

            // Smooth width transition with easing
            var easedT = t * t * (3 - 2 * t); // smooth step function
            var width = tailWidth + (baseWidth - tailWidth) * easedT;
            var halfWidth = width / 2;

            leftPoints.Add(new Point(centerX + normalX * halfWidth, centerY + normalY * halfWidth));
            rightPoints.Add(new Point(centerX - normalX * halfWidth, centerY - normalY * halfWidth));
        }

        // Arrow head points with smooth connection
        var headLeft = new Point(headBase.X + px * headWidth / 2, headBase.Y + py * headWidth / 2);
        var headRight = new Point(headBase.X - px * headWidth / 2, headBase.Y - py * headWidth / 2);

        // Build streamlined path with curves
        var fig = new PathFigure { StartPoint = leftPoints[0], IsClosed = true };
        var segs = new PathSegments();

        // Swallow tail (燕尾) - create a forked tail like a swallow's tail
        var tailNotchDepth = tailWidth * 0.5; // How deep the notch goes into the tail

        // Calculate the center point between left and right tail edges
        var tailCenter = new Point((leftPoints[0].X + rightPoints[0].X) / 2, (leftPoints[0].Y + rightPoints[0].Y) / 2);

        // Create the notch point - this goes inward (toward the arrow head)
        var tailNotchPoint = new Point(
            tailCenter.X + ux * tailNotchDepth, // Move forward (toward arrow head)
            tailCenter.Y + uy * tailNotchDepth
        );

        // Create swallow tail shape - simple V shape
        segs.Add(new LineSegment { Point = tailNotchPoint }); // Left edge to notch (inward)
        segs.Add(new LineSegment { Point = rightPoints[0] }); // Notch to right edge

        // Smooth curved shaft right edge
        for (int i = 1; i < rightPoints.Count; i++)
        {
            if (i == 1 || i == rightPoints.Count - 1)
                segs.Add(new LineSegment { Point = rightPoints[i] });
            else
            {
                // Add subtle curves between points for ultra-smooth edges
                var prev = rightPoints[i - 1];
                var curr = rightPoints[i];
                var next = i < rightPoints.Count - 1 ? rightPoints[i + 1] : curr;
                var ctrl = new Point((prev.X + curr.X) / 2, (prev.Y + curr.Y) / 2);
                segs.Add(new QuadraticBezierSegment { Point1 = ctrl, Point2 = curr });
            }
        }

        // Smooth connection to arrow head
        segs.Add(new QuadraticBezierSegment
        {
            Point1 = new Point((rightPoints.Last().X + headRight.X) / 2, (rightPoints.Last().Y + headRight.Y) / 2),
            Point2 = headRight
        });

        // Arrow head right edge with inward curve
        var rightCurveCtrl = new Point(
            headRight.X + (b.X - headRight.X) * 0.3 + px * headWidth * 0.1,
            headRight.Y + (b.Y - headRight.Y) * 0.3 + py * headWidth * 0.1
        );
        segs.Add(new QuadraticBezierSegment
        {
            Point1 = rightCurveCtrl,
            Point2 = b
        });

        // Arrow head left edge with inward curve
        var leftCurveCtrl = new Point(
            headLeft.X + (b.X - headLeft.X) * 0.3 - px * headWidth * 0.1,
            headLeft.Y + (b.Y - headLeft.Y) * 0.3 - py * headWidth * 0.1
        );
        segs.Add(new QuadraticBezierSegment
        {
            Point1 = leftCurveCtrl,
            Point2 = headLeft
        });

        // Smooth connection back to shaft
        segs.Add(new QuadraticBezierSegment
        {
            Point1 = new Point((headLeft.X + leftPoints.Last().X) / 2, (headLeft.Y + leftPoints.Last().Y) / 2),
            Point2 = leftPoints.Last()
        });

        // Smooth curved shaft left edge (reverse)
        for (int i = leftPoints.Count - 2; i >= 0; i--)
        {
            if (i == 0 || i == leftPoints.Count - 2)
                segs.Add(new LineSegment { Point = leftPoints[i] });
            else
            {
                var prev = leftPoints[i + 1];
                var curr = leftPoints[i];
                var ctrl = new Point((prev.X + curr.X) / 2, (prev.Y + curr.Y) / 2);
                segs.Add(new QuadraticBezierSegment { Point1 = ctrl, Point2 = curr });
            }
        }

        fig.Segments = segs;
        var geom = new PathGeometry { Figures = new PathFigures { fig } };
        body.Data = geom;

        // Enhanced gradient for streamlined effect - 0% to 75% transparency from tail to arrow head base
        var baseColor = (StrokeBrush as ISolidColorBrush)?.Color ?? Colors.Red;
        var transparent = Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B);                    // 100% transparent (0% opacity)
        var veryLight = Color.FromArgb((byte)(baseColor.A * 0.15), baseColor.R, baseColor.G, baseColor.B);  // 85% transparent (15% opacity)
        var light = Color.FromArgb((byte)(baseColor.A * 0.35), baseColor.R, baseColor.G, baseColor.B);      // 65% transparent (35% opacity)
        var medium = Color.FromArgb((byte)(baseColor.A * 0.55), baseColor.R, baseColor.G, baseColor.B);     // 45% transparent (55% opacity)
        var semiOpaque = Color.FromArgb((byte)(baseColor.A * 0.75), baseColor.R, baseColor.G, baseColor.B); // 25% transparent (75% opacity)

        // Calculate arrow head base position for gradient endpoint
        var arrowHeadBase = new Point(b.X - ux * headLen, b.Y - uy * headLen);

        body.Fill = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(a, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(arrowHeadBase, RelativeUnit.Absolute), // End at arrow head base, not tip
            GradientStops = new GradientStops
            {
                new GradientStop(transparent, 0),        // 100% transparent at tail (0% opacity)
                new GradientStop(veryLight, 0.2),        // 85% transparent (15% opacity)
                new GradientStop(light, 0.4),            // 65% transparent (35% opacity)
                new GradientStop(medium, 0.6),           // 45% transparent (55% opacity)
                new GradientStop(semiOpaque, 0.8),       // 25% transparent (75% opacity) at shaft end
                new GradientStop(semiOpaque, 1)          // Keep 75% opacity at arrow head base
            }
        };
        body.Stroke = null;

        // Softer shadow
        shadow.Data = geom;
        shadow.Fill = new SolidColorBrush(Colors.Black, 0.12);
        shadow.RenderTransform = new TranslateTransform(1.5, 1.5);
    }

    private static Color Lighten(Color c, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        byte L(byte v) => (byte)(v + (255 - v) * amount);
        return Color.FromArgb(c.A, L(c.R), L(c.G), L(c.B));
    }

    private static Color Darken(Color c, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        byte D(byte v) => (byte)(v * (1 - amount));
        return Color.FromArgb(c.A, D(c.R), D(c.G), D(c.B));
    }

    private Point ClampToSelection(Point p)
    {
        var x = Math.Clamp(p.X, SelectionRect.X, SelectionRect.Right);
        var y = Math.Clamp(p.Y, SelectionRect.Y, SelectionRect.Bottom);
        return new Point(x, y);
    }
}


