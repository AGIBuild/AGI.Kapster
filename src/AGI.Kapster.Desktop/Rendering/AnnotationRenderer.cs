using AGI.Kapster.Desktop.Models;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AGI.Kapster.Desktop.Rendering;

/// <summary>
/// 标注渲染器实现
/// </summary>
public class AnnotationRenderer : IAnnotationRenderer
{
    private readonly AnnotationRenderOptions _options;
    private readonly Dictionary<Guid, List<Control>> _renderCache = new();

    // Geometry cache to avoid rebuilding complex figures repeatedly
    private readonly Dictionary<Guid, (Geometry geometry, Rect bounds, long version)> _geometryCache = new();
    private readonly Dictionary<Guid, (PathGeometry geometry, Rect bounds, long version)> _freehandCache = new();

    // Path instance pool to reduce allocations
    private readonly Stack<Path> _pathPool = new();
    private readonly SolidColorBrush _arrowShadowBrush = new SolidColorBrush(Colors.Black, 0.08);

    // Geometry pools for shapes with reusable local coordinates
    private readonly Dictionary<(int w, int h), RectangleGeometry> _rectGeomPool = new();
    private readonly Dictionary<(int w, int h), EllipseGeometry> _ellipseGeomPool = new();

    public AnnotationRenderer(AnnotationRenderOptions? options = null)
    {
        _options = options ?? new AnnotationRenderOptions();
    }

    public void Render(Canvas canvas, IAnnotationItem item)
    {
        if (!item.IsVisible) return;

        // Remove existing render if any
        RemoveRender(canvas, item);

        var controls = new List<Control>();

        // Render the annotation based on its type
        switch (item)
        {
            case ArrowAnnotation arrow:
                RenderArrow(canvas, arrow, controls);
                break;
            case RectangleAnnotation rectangle:
                RenderRectangle(canvas, rectangle, controls);
                break;
            case EllipseAnnotation ellipse:
                RenderEllipse(canvas, ellipse, controls);
                break;
            case TextAnnotation text:
                RenderText(canvas, text, controls);
                break;
            case FreehandAnnotation freehand:
                RenderFreehand(canvas, freehand, controls);
                break;
            case EmojiAnnotation emoji:
                RenderEmoji(canvas, emoji, controls);
                break;
        }

        // Render selection handles if selected
        if (item.State == AnnotationState.Selected && _options.ShowSelectionHandles)
        {
            RenderSelectionHandles(canvas, item, controls);
        }

        // Render bounds if enabled
        if (_options.ShowBounds)
        {
            RenderBounds(canvas, item, controls);
        }

        // Set Z-index for all controls
        foreach (var control in controls)
        {
            control.ZIndex = item.ZIndex;
        }

        // Cache the rendered controls
        _renderCache[item.Id] = controls;
    }

    public void RenderAll(Canvas canvas, IEnumerable<IAnnotationItem> items)
    {
        Clear(canvas);

        // Render items sorted by Z-index
        foreach (var item in items.OrderBy(i => i.ZIndex))
        {
            Render(canvas, item);
        }
    }

    public void RenderChanged(Canvas canvas, IEnumerable<IAnnotationItem> items, Rect dirtyRect)
    {
        if (IsEmptyRect(dirtyRect))
        {
            // Fallback to full render if dirty rect is not provided
            RenderAll(canvas, items);
            return;
        }

        foreach (var item in items)
        {
            // Use cached bounds when available for looser intersection test; fallback to item.Bounds
            Rect itemBounds = item switch
            {
                TextAnnotation ta => ta.GetTextRenderBounds(),
                _ => item.Bounds
            };
            if (_geometryCache.TryGetValue(item.Id, out var cached))
            {
                if (!IsEmptyRect(cached.bounds))
                {
                    itemBounds = cached.bounds;
                }
            }

            if (!itemBounds.Intersects(dirtyRect))
                continue;

            // Re-render this item only
            Render(canvas, item);
        }
    }

    public void RenderChanged(Canvas canvas, IEnumerable<IAnnotationItem> items, IReadOnlyList<Rect> dirtyRects)
    {
        if (dirtyRects == null || dirtyRects.Count == 0)
        {
            RenderAll(canvas, items);
            return;
        }
        // Merge dirty rects into minimal list
        var merged = MergeRects(dirtyRects);
        foreach (var item in items)
        {
            Rect itemBounds = item is TextAnnotation t ? t.GetTextRenderBounds() : item.Bounds;
            if (_geometryCache.TryGetValue(item.Id, out var cached) && !IsEmptyRect(cached.bounds))
                itemBounds = cached.bounds;
            bool intersects = false;
            foreach (var dr in merged)
            {
                if (itemBounds.Intersects(dr)) { intersects = true; break; }
            }
            if (!intersects) continue;
            Render(canvas, item);
        }
    }

    private static List<Rect> MergeRects(IReadOnlyList<Rect> rects)
    {
        var list = rects.Where(r => r.Width > 0 && r.Height > 0).ToList();
        if (list.Count <= 1) return list;
        // Simple O(n^2) merge sufficient for small frame batches
        bool merged;
        do
        {
            merged = false;
            for (int i = 0; i < list.Count && !merged; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    var a = list[i]; var b = list[j];
                    if (a.Intersects(b) || Touching(a, b))
                    {
                        var u = new Rect(Math.Min(a.Left, b.Left), Math.Min(a.Top, b.Top),
                                         Math.Max(a.Right, b.Right) - Math.Min(a.Left, b.Left),
                                         Math.Max(a.Bottom, b.Bottom) - Math.Min(a.Top, b.Top));
                        list.RemoveAt(j); list[i] = u; merged = true; break;
                    }
                }
            }
        } while (merged);
        return list;
    }

    private static bool Touching(Rect a, Rect b)
    {
        return !(a.Right < b.Left || b.Right < a.Left || a.Bottom < b.Top || b.Bottom < a.Top);
    }

    private static bool IsEmptyRect(Rect rect) => rect.Width <= 0 || rect.Height <= 0;

    public void Clear(Canvas canvas)
    {
        canvas.Children.Clear();
        _renderCache.Clear();
        _geometryCache.Clear();
        _freehandCache.Clear();
    }

    public void RemoveRender(Canvas canvas, IAnnotationItem item)
    {
        if (_renderCache.TryGetValue(item.Id, out var controls))
        {
            foreach (var control in controls)
            {
                canvas.Children.Remove(control);
                if (control is Path p)
                {
                    // Reset path before pooling
                    p.Data = null;
                    p.Stroke = null;
                    p.Fill = null;
                    p.StrokeThickness = 0;
                    _pathPool.Push(p);
                }
            }
            _renderCache.Remove(item.Id);
        }

        // Drop geometry cache for this item to force rebuild next time
        _geometryCache.Remove(item.Id);
        _freehandCache.Remove(item.Id);

        // Additional safety check: remove any controls with matching name pattern
        // This helps prevent ghosting if the cache misses anything
        if (item is TextAnnotation)
        {
            var targetName = $"TextAnnotation_{item.Id}";
            var toRemove = canvas.Children
                .OfType<TextBlock>()
                .Where(tb => tb.Name == targetName)
                .ToList();

            foreach (var textBlock in toRemove)
            {
                canvas.Children.Remove(textBlock);
            }

            if (toRemove.Count > 0)
            {
                Log.Warning("Removed {Count} additional TextBlock controls for annotation {Id} to prevent ghosting",
                    toRemove.Count, item.Id);
            }
        }
    }

    /// <summary>
    /// 渲染箭头
    /// </summary>
    private void RenderArrow(Canvas canvas, ArrowAnnotation arrow, List<Control> controls)
    {
        var arrowCanvas = new Canvas();

        // Create shadow and body paths
        var shadowPath = _pathPool.Count > 0 ? _pathPool.Pop() : new Path();
        shadowPath.Fill = _arrowShadowBrush;
        shadowPath.Stroke = null;
        shadowPath.RenderTransform = new TranslateTransform(1.0, 1.0);
        var bodyPath = _pathPool.Count > 0 ? _pathPool.Pop() : new Path();
        bodyPath.Stroke = null;

        arrowCanvas.Children.Add(shadowPath);
        arrowCanvas.Children.Add(bodyPath);

        // Build or reuse geometry
        var version = ComputeArrowVersion(arrow);
        Geometry geom;
        if (_geometryCache.TryGetValue(arrow.Id, out var cached) && cached.version == version && cached.geometry is not null)
        {
            geom = cached.geometry;
        }
        else
        {
            geom = BuildArrowGeometry(arrow, out var bounds);
            _geometryCache[arrow.Id] = (geom, bounds, version);
        }
        
        bodyPath.Data = geom;
        shadowPath.Data = geom;
        ApplyArrowFillAndShadow(bodyPath, shadowPath, arrow);

        canvas.Children.Add(arrowCanvas);
        controls.Add(arrowCanvas);
    }

    /// <summary>
    /// 渲染矩形
    /// </summary>
    private void RenderRectangle(Canvas canvas, RectangleAnnotation rectangle, List<Control> controls)
    {
        // Simple cache key via version
        var version = ComputeRectVersion(rectangle);
        if (!_geometryCache.TryGetValue(rectangle.Id, out var cached) || cached.version != version)
        {
            var key = ((int)Math.Round(rectangle.Width), (int)Math.Round(rectangle.Height));
            if (!_rectGeomPool.TryGetValue(key, out var rg))
            {
                rg = new RectangleGeometry(new Rect(0, 0, key.Item1, key.Item2));
                _rectGeomPool[key] = rg;
            }
            _geometryCache[rectangle.Id] = (rg, rectangle.Bounds, version);
            cached = (rg, rectangle.Bounds, version);
        }

        var path = _pathPool.Count > 0 ? _pathPool.Pop() : new Path();
        path.Data = cached.geometry;
        path.Stroke = CreateBrush(rectangle.Style.StrokeColor);
        path.StrokeThickness = rectangle.Style.StrokeWidth;
        path.Fill = rectangle.Style.FillMode != FillMode.None ? CreateBrush(rectangle.Style.FillColor) : Brushes.Transparent;
        path.Opacity = rectangle.Style.Opacity;
        Canvas.SetLeft(path, rectangle.Rectangle.X);
        Canvas.SetTop(path, rectangle.Rectangle.Y);
        canvas.Children.Add(path);
        controls.Add(path);
    }

    /// <summary>
    /// 渲染椭圆
    /// </summary>
    private void RenderEllipse(Canvas canvas, EllipseAnnotation ellipse, List<Control> controls)
    {
        var version = ComputeEllipseVersion(ellipse);
        if (!_geometryCache.TryGetValue(ellipse.Id, out var cached) || cached.version != version)
        {
            var key = ((int)Math.Round(ellipse.BoundingRect.Width), (int)Math.Round(ellipse.BoundingRect.Height));
            if (!_ellipseGeomPool.TryGetValue(key, out var eg))
            {
                eg = new EllipseGeometry(new Rect(0, 0, key.Item1, key.Item2));
                _ellipseGeomPool[key] = eg;
            }
            _geometryCache[ellipse.Id] = (eg, ellipse.Bounds, version);
            cached = (eg, ellipse.Bounds, version);
        }

        var path = _pathPool.Count > 0 ? _pathPool.Pop() : new Path();
        path.Data = cached.geometry;
        path.Stroke = CreateBrush(ellipse.Style.StrokeColor);
        path.StrokeThickness = ellipse.Style.StrokeWidth;
        path.Fill = ellipse.Style.FillMode != FillMode.None ? CreateBrush(ellipse.Style.FillColor) : Brushes.Transparent;
        path.Opacity = ellipse.Style.Opacity;
        Canvas.SetLeft(path, ellipse.BoundingRect.X);
        Canvas.SetTop(path, ellipse.BoundingRect.Y);
        canvas.Children.Add(path);
        controls.Add(path);
    }

    private static long ComputeRectVersion(RectangleAnnotation r)
    {
        unchecked
        {
            long v = 1469598103934665603L;
            void Mix(double d) { var b = BitConverter.DoubleToInt64Bits(d); v ^= b; v *= 1099511628211L; }
            void MixColor(Color c) { v ^= ((long)c.A << 24) | ((long)c.R << 16) | ((long)c.G << 8) | c.B; v *= 1099511628211L; }
            var rect = r.Rectangle;
            Mix(rect.X); Mix(rect.Y); Mix(rect.Width); Mix(rect.Height);
            Mix(r.Style.StrokeWidth); Mix(r.Style.Opacity); MixColor(r.Style.StrokeColor); MixColor(r.Style.FillColor);
            return v;
        }
    }

    private static long ComputeEllipseVersion(EllipseAnnotation e)
    {
        unchecked
        {
            long v = 1469598103934665603L;
            void Mix(double d) { var b = BitConverter.DoubleToInt64Bits(d); v ^= b; v *= 1099511628211L; }
            void MixColor(Color c) { v ^= ((long)c.A << 24) | ((long)c.R << 16) | ((long)c.G << 8) | c.B; v *= 1099511628211L; }
            var rect = e.BoundingRect;
            Mix(rect.X); Mix(rect.Y); Mix(rect.Width); Mix(rect.Height);
            Mix(e.Style.StrokeWidth); Mix(e.Style.Opacity); MixColor(e.Style.StrokeColor); MixColor(e.Style.FillColor);
            return v;
        }
    }

    /// <summary>
    /// 渲染文本
    /// </summary>
    private void RenderText(Canvas canvas, TextAnnotation text, List<Control> controls)
    {
        // 不渲染正在编辑的文本（由TextBox替代）
        if (text.IsEditing)
        {
            Log.Debug("Skipping render for text annotation {Id} in editing state", text.Id);
            return;
        }

        if (string.IsNullOrEmpty(text.Text)) return;

        // Ensure we don't have duplicate text renders for the same annotation
        // by using the annotation ID as a unique identifier
        var textBlock = new TextBlock
        {
            Text = text.Text,
            FontFamily = new FontFamily(text.Style.FontFamily),
            FontSize = text.Style.FontSize,
            FontWeight = text.Style.FontWeight,
            FontStyle = text.Style.FontStyle,
            Foreground = CreateBrush(text.Style.StrokeColor),
            Opacity = text.Style.Opacity,
            // Add unique name to help identify and prevent duplicates
            Name = $"TextAnnotation_{text.Id}"
        };

        Canvas.SetLeft(textBlock, text.Position.X);
        Canvas.SetTop(textBlock, text.Position.Y);

        canvas.Children.Add(textBlock);
        controls.Add(textBlock);

        // CRITICAL FIX: Measure actual TextBlock size and sync with annotation bounds
        // This prevents selection bounds mismatch that causes text ghosting
        textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var actualSize = textBlock.DesiredSize;

        // Sync the measured size back to the text annotation to ensure bounds consistency
        text.SyncActualSize(actualSize);

        Log.Debug("TextBlock measured size: {Width}x{Height} for annotation {Id}",
            actualSize.Width, actualSize.Height, text.Id);
    }

    /// <summary>
    /// 渲染选择手柄
    /// </summary>
    private void RenderSelectionHandles(Canvas canvas, IAnnotationItem item, List<Control> controls)
    {
        // For text annotations, use actual text rendering bounds for precise handle positioning
        var bounds = item is TextAnnotation textAnnotation ?
            textAnnotation.GetTextRenderBounds() :
            item.Bounds;

        var handleSize = _options.HandleSize;
        var halfHandle = handleSize / 2;

        // Create 8 resize handles around the bounds
        var handlePositions = new[]
        {
            new Point(bounds.Left - halfHandle, bounds.Top - halfHandle),           // Top-left
            new Point(bounds.Center.X - halfHandle, bounds.Top - halfHandle),      // Top-center
            new Point(bounds.Right - halfHandle, bounds.Top - halfHandle),         // Top-right
            new Point(bounds.Right - halfHandle, bounds.Center.Y - halfHandle),    // Middle-right
            new Point(bounds.Right - halfHandle, bounds.Bottom - halfHandle),      // Bottom-right
            new Point(bounds.Center.X - halfHandle, bounds.Bottom - halfHandle),   // Bottom-center
            new Point(bounds.Left - halfHandle, bounds.Bottom - halfHandle),       // Bottom-left
            new Point(bounds.Left - halfHandle, bounds.Center.Y - halfHandle)      // Middle-left
        };

        foreach (var pos in handlePositions)
        {
            var handle = new Rectangle
            {
                Width = handleSize,
                Height = handleSize,
                Fill = CreateBrush(_options.SelectionColor),
                Stroke = Brushes.White,
                StrokeThickness = 1
            };

            Canvas.SetLeft(handle, pos.X);
            Canvas.SetTop(handle, pos.Y);

            canvas.Children.Add(handle);
            controls.Add(handle);
        }

        // Selection outline
        var outline = new Rectangle
        {
            Width = bounds.Width,
            Height = bounds.Height,
            Fill = Brushes.Transparent,
            Stroke = CreateBrush(_options.SelectionColor),
            StrokeThickness = 1,
            StrokeDashArray = new AvaloniaList<double> { 5, 5 }
        };

        Canvas.SetLeft(outline, bounds.X);
        Canvas.SetTop(outline, bounds.Y);

        canvas.Children.Add(outline);
        controls.Add(outline);
    }

    /// <summary>
    /// 渲染边界框
    /// </summary>
    private void RenderBounds(Canvas canvas, IAnnotationItem item, List<Control> controls)
    {
        var bounds = item.Bounds;
        var outline = new Rectangle
        {
            Width = bounds.Width,
            Height = bounds.Height,
            Fill = Brushes.Transparent,
            Stroke = CreateBrush(_options.BoundsColor),
            StrokeThickness = 1,
            StrokeDashArray = new AvaloniaList<double> { 2, 2 }
        };

        Canvas.SetLeft(outline, bounds.X);
        Canvas.SetTop(outline, bounds.Y);

        canvas.Children.Add(outline);
        controls.Add(outline);
    }

    /// <summary>
    /// 更新箭头几何形状
    /// </summary>
    private void UpdateArrowGeometry(Path bodyPath, Path shadowPath, ArrowAnnotation arrow)
    {
        var a = arrow.StartPoint;
        var b = arrow.EndPoint;

        var dx = b.X - a.X; var dy = b.Y - a.Y;
        var len = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
        if (len < 5) return;

        var ux = dx / len; var uy = dy / len;
        var px = -uy; var py = ux;

        // Arrow dimensions (from the perfected arrow implementation)
        var headLen = Math.Min(len * 0.2, Math.Max(10, arrow.Style.StrokeWidth * 4.5));
        var headWidth = Math.Max(14, arrow.Style.StrokeWidth * 7.0);
        var tailWidth = Math.Max(arrow.Style.StrokeWidth * 3.5, 12.0);
        var baseWidth = Math.Max(arrow.Style.StrokeWidth * 0.8, 2.5);

        // Arrow curve configuration
        const double CurveIntensityFactor = 0.12;
        const double MaxCurveAmount = 25;
        const double StraightAngleThreshold = 15;

        var midPoint = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        var angle = Math.Atan2(Math.Abs(dy), Math.Abs(dx)) * 180 / Math.PI;
        var isHorizontal = angle < StraightAngleThreshold;
        var isVertical = angle > (90 - StraightAngleThreshold);

        Point curveControl;
        if (isHorizontal || isVertical)
        {
            curveControl = midPoint;
        }
        else
        {
            var curveAmount = Math.Min(len * CurveIntensityFactor, MaxCurveAmount);
            double curveOffsetX = 0, curveOffsetY = 0;

            if (dx > 0 && dy < 0)
            {
                curveOffsetX = -curveAmount;
                curveOffsetY = -curveAmount;
            }
            else if (dx > 0 && dy > 0)
            {
                curveOffsetX = -curveAmount;
                curveOffsetY = curveAmount;
            }
            else if (dx < 0 && dy < 0)
            {
                curveOffsetX = curveAmount;
                curveOffsetY = -curveAmount;
            }
            else if (dx < 0 && dy > 0)
            {
                curveOffsetX = curveAmount;
                curveOffsetY = curveAmount;
            }

            curveControl = new Point(midPoint.X + curveOffsetX, midPoint.Y + curveOffsetY);
        }

        // Generate smooth curved shaft using quadratic bezier sampling
        // Increase sampling density to smooth edges and reduce stair-stepping
        int steps = Math.Max(24, (int)(len / 4));
        var leftPoints = new List<Point>(steps + 1);
        var rightPoints = new List<Point>(steps + 1);

        var headBase = new Point(b.X - ux * headLen, b.Y - uy * headLen);

        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            double omt = 1 - t;

            var centerX = omt * omt * a.X + 2 * omt * t * curveControl.X + t * t * headBase.X;
            var centerY = omt * omt * a.Y + 2 * omt * t * curveControl.Y + t * t * headBase.Y;

            var tangentX = 2 * omt * (curveControl.X - a.X) + 2 * t * (headBase.X - curveControl.X);
            var tangentY = 2 * omt * (curveControl.Y - a.Y) + 2 * t * (headBase.Y - curveControl.Y);
            var tangentLen = Math.Max(1e-6, Math.Sqrt(tangentX * tangentX + tangentY * tangentY));
            var normalX = -tangentY / tangentLen;
            var normalY = tangentX / tangentLen;

            // Taper becomes more aggressive with larger sizes
            var sizeFactor = Math.Sqrt(arrow.Style.StrokeWidth);
            var taper = 1.0 + sizeFactor * 0.1;
            var easedT = Math.Pow(t, taper) * (3 - 2*t);
            
            var width = tailWidth * (1 - easedT) + baseWidth * easedT;
            var halfWidth = width / 2;

            leftPoints.Add(new Point(centerX + normalX * halfWidth, centerY + normalY * halfWidth));
            rightPoints.Add(new Point(centerX - normalX * halfWidth, centerY - normalY * halfWidth));
        }

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
        bodyPath.Data = geom;
        shadowPath.Data = geom;

        // Gradient re-tuned to reduce banding and jagged transparency edges
        var baseColor = arrow.Style.StrokeColor;
        var tailSoft = Color.FromArgb((byte)(baseColor.A * 0.12), baseColor.R, baseColor.G, baseColor.B);   // 12%
        var tailMid = Color.FromArgb((byte)(baseColor.A * 0.28), baseColor.R, baseColor.G, baseColor.B);   // 28%
        var shaftMid = Color.FromArgb((byte)(baseColor.A * 0.48), baseColor.R, baseColor.G, baseColor.B);   // 48%
        var shaftEnd = Color.FromArgb((byte)(baseColor.A * 0.72), baseColor.R, baseColor.G, baseColor.B);   // 72%

        // Calculate arrow head base position for gradient endpoint
        var arrowHeadBase = new Point(b.X - ux * headLen, b.Y - uy * headLen);

        bodyPath.Fill = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(a, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(arrowHeadBase, RelativeUnit.Absolute), // End at arrow head base, not tip
            GradientStops = new GradientStops
            {
                // Avoid fully transparent start to reduce visible banding on AA edges
                new GradientStop(tailSoft, 0.00),  // 12%
                new GradientStop(tailMid,  0.20),  // 28%
                new GradientStop(shaftMid, 0.45),  // 48%
                new GradientStop(shaftEnd, 0.80),  // 72%
                new GradientStop(shaftEnd, 1.00)   // maintain opacity near head base
            }
        };
        // No stroke outline per requirement
        bodyPath.Stroke = null;

        // Softer shadow
        shadowPath.Fill = new SolidColorBrush(Colors.Black, 0.08);
        shadowPath.Stroke = null;
        shadowPath.RenderTransform = new TranslateTransform(1.0, 1.0);
    }

    // Build arrow geometry only (no fill/stroke assignment). Returns geometry and bounds.
    private Geometry BuildArrowGeometry(ArrowAnnotation arrow, out Rect bounds)
    {
        var trail = arrow.Trail != null && arrow.Trail.Count > 1
            ? arrow.Trail
            : new List<Point> { arrow.StartPoint, arrow.EndPoint };

        var smoother = PathSmoother.Generate(trail);
        var request = new TacticalArrowRequest(arrow.StartPoint, arrow.EndPoint, trail, arrow.Style.StrokeWidth, arrow.Style.StrokeColor, smoother.SignedBend);
        var result = TacticalArrowBuilder.Build(request);

        bounds = result.Geometry.Bounds;
        return result.Geometry;
    }

    private static long ComputeArrowVersion(ArrowAnnotation arrow)
    {
        // Combine key properties into a simple version hash surrogate
        unchecked
        {
            long v = 1469598103934665603L; // FNV offset basis
            void Mix(double d)
            {
                var bits = BitConverter.DoubleToInt64Bits(d);
                v ^= bits;
                v *= 1099511628211L;
            }
            void MixColor(Color c)
            {
                v ^= ((long)c.A << 24) | ((long)c.R << 16) | ((long)c.G << 8) | c.B;
                v *= 1099511628211L;
            }
            Mix(arrow.StartPoint.X); Mix(arrow.StartPoint.Y);
            Mix(arrow.EndPoint.X); Mix(arrow.EndPoint.Y);
            Mix(arrow.Style.StrokeWidth);
            Mix(arrow.Style.Opacity);
            MixColor(arrow.Style.StrokeColor);

            if (arrow.Trail != null)
            {
                foreach (var p in arrow.Trail)
                {
                    Mix(p.X);
                    Mix(p.Y);
                }
            }
            
            return v;
        }
    }

    private void ApplyArrowFillAndShadow(Path bodyPath, Path shadowPath, ArrowAnnotation arrow)
    {
        // The actual geometry is built and cached in BuildArrowGeometry.
        // Here we just apply the fill/shadow, which might depend on live state not captured in the cache key.
        
        var trail = arrow.Trail != null && arrow.Trail.Count > 1
            ? arrow.Trail
            : new List<Point> { arrow.StartPoint, arrow.EndPoint };

        // We need to re-run the builder to get the brushes and transforms, but we don't use the geometry from it.
        var smoother = PathSmoother.Generate(trail);
        var request = new TacticalArrowRequest(arrow.StartPoint, arrow.EndPoint, trail, arrow.Style.StrokeWidth, arrow.Style.StrokeColor, smoother.SignedBend);
        var result = TacticalArrowBuilder.Build(request);
        
        bodyPath.Fill = result.Fill;
        bodyPath.Stroke = null;
        shadowPath.Fill = result.ShadowFill;
        shadowPath.Stroke = null;
        shadowPath.RenderTransform = result.ShadowTransform;
    }

    /// <summary>
    /// 渲染自由画笔标注
    /// </summary>
    private void RenderFreehand(Canvas canvas, FreehandAnnotation freehand, List<Control> controls)
    {
        if (!freehand.IsVisible || freehand.Points.Count < 2) return;

        // Use smoothed points if available, otherwise use original points
        var points = (freehand.SmoothedPoints?.Count > 0 ? freehand.SmoothedPoints : freehand.Points) ?? new List<Point>();

        if (points.Count < 2) return;

        // Cache key and version
        long version = ComputeFreehandVersion(freehand);
        if (!_freehandCache.TryGetValue(freehand.Id, out var cached) || cached.version != version)
        {
            // Rebuild
            var pathGeometry = new PathGeometry();
            var pathFigure = new PathFigure { StartPoint = points[0], IsClosed = false };
            if (points.Count == 2)
            {
                pathFigure.Segments!.Add(new LineSegment { Point = points[1] });
            }
            else
            {
                for (int i = 1; i < points.Count; i++)
                {
                    if (i == 1)
                    {
                        pathFigure.Segments!.Add(new LineSegment { Point = points[1] });
                    }
                    else if (i == points.Count - 1)
                    {
                        pathFigure.Segments!.Add(new LineSegment { Point = points[i] });
                    }
                    else
                    {
                        var controlPoint = points[i - 1];
                        var endPoint = new Point(
                            (points[i - 1].X + points[i].X) / 2,
                            (points[i - 1].Y + points[i].Y) / 2);
                        pathFigure.Segments!.Add(new QuadraticBezierSegment
                        {
                            Point1 = controlPoint,
                            Point2 = endPoint
                        });
                    }
                }
            }
            pathGeometry.Figures!.Add(pathFigure);
            var bounds = pathGeometry.Bounds.Inflate(freehand.Style.StrokeWidth / 2);
            _freehandCache[freehand.Id] = (pathGeometry, bounds, version);
            cached = (pathGeometry, bounds, version);
        }

        // Create path control
        var path = _pathPool.Count > 0 ? _pathPool.Pop() : new Path();
        path.Data = cached.geometry;
        path.Stroke = CreateBrush(freehand.Style.StrokeColor);
        path.StrokeThickness = freehand.Style.StrokeWidth;
        path.Opacity = freehand.Style.Opacity;
        path.StrokeLineCap = PenLineCap.Round;

        canvas.Children.Add(path);
        controls.Add(path);
    }

    private static long ComputeFreehandVersion(FreehandAnnotation freehand)
    {
        unchecked
        {
            long v = 1469598103934665603L;
            void Mix(double d) { var b = BitConverter.DoubleToInt64Bits(d); v ^= b; v *= 1099511628211L; }
            void MixColor(Color c) { v ^= ((long)c.A << 24) | ((long)c.R << 16) | ((long)c.G << 8) | c.B; v *= 1099511628211L; }
            foreach (var p in freehand.Points)
            { Mix(p.X); Mix(p.Y); }
            Mix(freehand.Style.StrokeWidth); Mix(freehand.Style.Opacity); MixColor(freehand.Style.StrokeColor);
            return v;
        }
    }

    /// <summary>
    /// 渲染Emoji标注
    /// </summary>
    private void RenderEmoji(Canvas canvas, EmojiAnnotation emoji, List<Control> controls)
    {
        if (!emoji.IsVisible) return;

        var textBlock = new TextBlock
        {
            Text = emoji.Emoji,
            FontSize = emoji.ActualSize,
            FontFamily = new FontFamily("Segoe UI Emoji"),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            Opacity = emoji.Style.Opacity,
            Name = $"EmojiAnnotation_{emoji.Id}",
            // Ensure text is not clipped
            ClipToBounds = false
        };

        // Measure the text to get actual size
        textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var textSize = textBlock.DesiredSize;

        // Position centered at the emoji position
        var centerX = emoji.Position.X;
        var centerY = emoji.Position.Y;

        // Calculate position to center the text
        var left = centerX - textSize.Width / 2;
        var top = centerY - textSize.Height / 2;

        Canvas.SetLeft(textBlock, left);
        Canvas.SetTop(textBlock, top);

        // Set explicit size to ensure proper rendering
        textBlock.Width = textSize.Width;
        textBlock.Height = textSize.Height;

        canvas.Children.Add(textBlock);
        controls.Add(textBlock);
    }

    /// <summary>
    /// 创建画刷
    /// </summary>
    // Simple brush pool to reduce allocations
    private readonly Dictionary<uint, SolidColorBrush> _brushPool = new();
    private IBrush CreateBrush(Color color)
    {
        uint key = ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        if (_brushPool.TryGetValue(key, out var b)) return b;
        var brush = new SolidColorBrush(color);
        _brushPool[key] = brush;
        return brush;
    }

    private (double headLen, double headWidth, double tailWidth, double baseWidth) CalculateArrowDimensions(ArrowAnnotation arrow, double len)
    {
        var size = Math.Max(1.0, arrow.Style.StrokeWidth);
        // Use Log for faster growth at larger sizes
        var sizeFactor = Math.Log(size + 1);
 
        // --- Tail Width ---
        var tailWidth = 6.0 + sizeFactor * 4.5;
        if (size > 10)
        {
            // Double tail width for large sizes
            tailWidth *= 1.5 + (size - 10) * 0.05; // Gently scale up from 1.5x to 2x
        }
        tailWidth = Math.Clamp(tailWidth, 5.0, Math.Max(5.0, len * 0.5));
 
        // --- Head Width ---
        var headWidth = tailWidth * 0.6 + sizeFactor * 1.0;
        var minHeadWidth = tailWidth * 0.4;
        var maxHeadWidth = Math.Max(minHeadWidth + 0.01, len * 0.3);
        headWidth = Math.Clamp(headWidth, minHeadWidth, maxHeadWidth);
         
        // --- Head Length ---
        var headLen = tailWidth * 0.7 + sizeFactor * 1.5;
        var minHeadLen = tailWidth * 0.45;
        var maxHeadLen = Math.Max(minHeadLen + 0.01, len * 0.22);
        headLen = Math.Clamp(headLen, minHeadLen, maxHeadLen);
         var baseWidth = Math.Max(arrow.Style.StrokeWidth * 0.8, 2.5);

        return (headLen, headWidth, tailWidth, baseWidth);
    }
}
