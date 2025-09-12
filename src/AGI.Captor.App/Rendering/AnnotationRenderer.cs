using AGI.Captor.App.Models;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AGI.Captor.App.Rendering;

/// <summary>
/// 标注渲染器实现
/// </summary>
public class AnnotationRenderer : IAnnotationRenderer
{
    private readonly AnnotationRenderOptions _options;
    private readonly Dictionary<Guid, List<Control>> _renderCache = new();

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

    public void Clear(Canvas canvas)
    {
        canvas.Children.Clear();
        _renderCache.Clear();
    }

    public void RemoveRender(Canvas canvas, IAnnotationItem item)
    {
        if (_renderCache.TryGetValue(item.Id, out var controls))
        {
            foreach (var control in controls)
            {
                canvas.Children.Remove(control);
            }
            _renderCache.Remove(item.Id);
        }

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
        // Create arrow path similar to the current implementation
        var arrowCanvas = new Canvas();
        
        // Create shadow path
        var shadowPath = new Path
        {
            Fill = new SolidColorBrush(Colors.Black, 0.15),
            RenderTransform = new TranslateTransform(1.5, 1.5)
        };
        
        // Create main body path
        var bodyPath = new Path
        {
            Stroke = null
        };
        
        arrowCanvas.Children.Add(shadowPath);
        arrowCanvas.Children.Add(bodyPath);
        
        // Use the arrow update logic from AnnotationOverlay
        UpdateArrowGeometry(bodyPath, shadowPath, arrow);
        
        canvas.Children.Add(arrowCanvas);
        controls.Add(arrowCanvas);
    }

    /// <summary>
    /// 渲染矩形
    /// </summary>
    private void RenderRectangle(Canvas canvas, RectangleAnnotation rectangle, List<Control> controls)
    {
        var rect = new Rectangle
        {
            Width = rectangle.Width,
            Height = rectangle.Height,
            Stroke = CreateBrush(rectangle.Style.StrokeColor),
            StrokeThickness = rectangle.Style.StrokeWidth,
            Fill = rectangle.Style.FillMode != FillMode.None 
                ? CreateBrush(rectangle.Style.FillColor) 
                : Brushes.Transparent,
            Opacity = rectangle.Style.Opacity
        };

        Canvas.SetLeft(rect, rectangle.Rectangle.X);
        Canvas.SetTop(rect, rectangle.Rectangle.Y);

        canvas.Children.Add(rect);
        controls.Add(rect);
    }

    /// <summary>
    /// 渲染椭圆
    /// </summary>
    private void RenderEllipse(Canvas canvas, EllipseAnnotation ellipse, List<Control> controls)
    {
        var ellipseShape = new Ellipse
        {
            Width = ellipse.BoundingRect.Width,
            Height = ellipse.BoundingRect.Height,
            Stroke = CreateBrush(ellipse.Style.StrokeColor),
            StrokeThickness = ellipse.Style.StrokeWidth,
            Fill = ellipse.Style.FillMode != FillMode.None 
                ? CreateBrush(ellipse.Style.FillColor) 
                : Brushes.Transparent,
            Opacity = ellipse.Style.Opacity
        };

        Canvas.SetLeft(ellipseShape, ellipse.BoundingRect.X);
        Canvas.SetTop(ellipseShape, ellipse.BoundingRect.Y);

        canvas.Children.Add(ellipseShape);
        controls.Add(ellipseShape);
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
        int steps = Math.Max(16, (int)(len / 8));
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
            
            var easedT = t * t * (3 - 2 * t);
            var width = tailWidth + (baseWidth - tailWidth) * easedT;
            var halfWidth = width / 2;
            
            leftPoints.Add(new Point(centerX + normalX * halfWidth, centerY + normalY * halfWidth));
            rightPoints.Add(new Point(centerX - normalX * halfWidth, centerY - normalY * halfWidth));
        }
        
        var headLeft = new Point(headBase.X + px * headWidth / 2, headBase.Y + py * headWidth / 2);
        var headRight = new Point(headBase.X - px * headWidth / 2, headBase.Y - py * headWidth / 2);
        
        // Build streamlined path with curves
        var fig = new PathFigure { StartPoint = leftPoints[0], IsClosed = true };
        var segs = new PathSegments();
        
        // Rounded tail cap
        var tailRadius = tailWidth / 2;
        segs.Add(new ArcSegment 
        { 
            Point = rightPoints[0], 
            Size = new Size(tailRadius, tailRadius), 
            IsLargeArc = false, 
            SweepDirection = SweepDirection.Clockwise 
        });
        
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

        // Enhanced gradient for streamlined effect
        var baseColor = arrow.Style.StrokeColor;
        var transparent = Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B);
        var lightSemi = Color.FromArgb((byte)(baseColor.A * 0.3), baseColor.R, baseColor.G, baseColor.B);
        var darkSemi = Color.FromArgb((byte)(baseColor.A * 0.8), baseColor.R, baseColor.G, baseColor.B);
        var darker = Color.FromArgb(baseColor.A, 
            (byte)Math.Max(0, baseColor.R * 0.85), 
            (byte)Math.Max(0, baseColor.G * 0.85), 
            (byte)Math.Max(0, baseColor.B * 0.85));
            
        bodyPath.Fill = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(a, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(b, RelativeUnit.Absolute),
            GradientStops = new GradientStops
            {
                new GradientStop(transparent, 0),
                new GradientStop(lightSemi, 0.1),
                new GradientStop(darkSemi, 0.5),
                new GradientStop(baseColor, 0.8),
                new GradientStop(darker, 1)
            }
        };
        bodyPath.Stroke = null;

        // Softer shadow
        shadowPath.Fill = new SolidColorBrush(Colors.Black, 0.12);
        shadowPath.Stroke = null;
        shadowPath.RenderTransform = new TranslateTransform(1.5, 1.5);
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

        // Create path geometry from points
        var pathGeometry = new PathGeometry();
        var pathFigure = new PathFigure { StartPoint = points[0], IsClosed = false };
        
        if (points.Count == 2)
        {
            // Simple line for two points
            pathFigure.Segments!.Add(new LineSegment { Point = points[1] });
        }
        else
        {
            // Create smooth curves using quadratic bezier segments
            for (int i = 1; i < points.Count; i++)
            {
                if (i == 1)
                {
                    // First segment - line to second point
                    pathFigure.Segments!.Add(new LineSegment { Point = points[1] });
                }
                else if (i == points.Count - 1)
                {
                    // Last segment - line to final point
                    pathFigure.Segments!.Add(new LineSegment { Point = points[i] });
                }
                else
                {
                    // Middle segments - quadratic bezier for smoothness
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

        // Create path control
        var path = new Path
        {
            Data = pathGeometry,
            Stroke = CreateBrush(freehand.Style.StrokeColor),
            StrokeThickness = freehand.Style.StrokeWidth,
            Opacity = freehand.Style.Opacity,
            StrokeLineCap = PenLineCap.Round
        };

        canvas.Children.Add(path);
        controls.Add(path);
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
    private IBrush CreateBrush(Color color)
    {
        return new SolidColorBrush(color);
    }
}
