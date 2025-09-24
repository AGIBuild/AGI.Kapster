using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AGI.Kapster.Desktop.Models;

/// <summary>
/// 自由画笔标注项
/// </summary>
public class FreehandAnnotation : AnnotationItemBase
{
    private List<Point> _points = new();
    private List<Point> _smoothedPoints = new();
    private Rect _boundingRect;

    public override AnnotationType Type => AnnotationType.Freehand;

    /// <summary>
    /// 原始路径点
    /// </summary>
    public IReadOnlyList<Point> Points => _points.AsReadOnly();

    /// <summary>
    /// 平滑后的路径点
    /// </summary>
    public IReadOnlyList<Point> SmoothedPoints => _smoothedPoints.AsReadOnly();

    public override Rect Bounds
    {
        get
        {
            if (_boundingRect == default && _points.Count > 0)
            {
                UpdateBounds();
            }
            return _boundingRect;
        }
    }

    public FreehandAnnotation(IAnnotationStyle? style = null)
        : base(style ?? AnnotationStyle.CreateShapeStyle(Color.FromRgb(255, 0, 0), 3.0))
    {
    }

    /// <summary>
    /// 添加新的路径点
    /// </summary>
    public void AddPoint(Point point)
    {
        _points.Add(point);
        _boundingRect = default; // Force recalculation
        ModifiedAt = DateTime.Now;
    }

    /// <summary>
    /// 批量添加路径点
    /// </summary>
    public void AddPoints(IEnumerable<Point> points)
    {
        _points.AddRange(points);
        _boundingRect = default; // Force recalculation
        ModifiedAt = DateTime.Now;
    }

    /// <summary>
    /// 完成路径绘制，进行平滑处理
    /// </summary>
    public void FinishPath()
    {
        if (_points.Count > 2)
        {
            _smoothedPoints = SmoothPath(_points);
        }
        else
        {
            _smoothedPoints = new List<Point>(_points);
        }

        UpdateBounds();
        ModifiedAt = DateTime.Now;
    }

    /// <summary>
    /// 路径平滑算法 - 使用简单的移动平均
    /// </summary>
    private List<Point> SmoothPath(List<Point> originalPoints)
    {
        if (originalPoints.Count <= 2)
            return new List<Point>(originalPoints);

        var smoothed = new List<Point>();
        const double smoothingFactor = 0.8;

        // 保留起点
        smoothed.Add(originalPoints[0]);

        // 对中间点进行平滑
        for (int i = 1; i < originalPoints.Count - 1; i++)
        {
            var prev = originalPoints[i - 1];
            var curr = originalPoints[i];
            var next = originalPoints[i + 1];

            // 简单的三点平均
            var smoothedX = prev.X * 0.25 + curr.X * 0.5 + next.X * 0.25;
            var smoothedY = prev.Y * 0.25 + curr.Y * 0.5 + next.Y * 0.25;

            // 与原点混合
            var finalX = curr.X * (1 - smoothingFactor) + smoothedX * smoothingFactor;
            var finalY = curr.Y * (1 - smoothingFactor) + smoothedY * smoothingFactor;

            smoothed.Add(new Point(finalX, finalY));
        }

        // 保留终点
        smoothed.Add(originalPoints[originalPoints.Count - 1]);

        return smoothed;
    }

    /// <summary>
    /// 简化路径 - 移除冗余点
    /// </summary>
    public void SimplifyPath(double tolerance = 2.0)
    {
        if (_points.Count <= 2) return;

        _points = DouglasPeucker(_points, tolerance);
        _boundingRect = default; // Force recalculation
        ModifiedAt = DateTime.Now;
    }

    /// <summary>
    /// Douglas-Peucker 算法简化路径
    /// </summary>
    private List<Point> DouglasPeucker(List<Point> points, double tolerance)
    {
        if (points.Count <= 2)
            return new List<Point>(points);

        var maxDistance = 0.0;
        var maxIndex = 0;

        for (int i = 1; i < points.Count - 1; i++)
        {
            var distance = PerpendicularDistance(points[i], points[0], points[points.Count - 1]);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                maxIndex = i;
            }
        }

        if (maxDistance > tolerance)
        {
            var left = DouglasPeucker(points.Take(maxIndex + 1).ToList(), tolerance);
            var right = DouglasPeucker(points.Skip(maxIndex).ToList(), tolerance);

            var result = new List<Point>(left);
            result.AddRange(right.Skip(1)); // Skip duplicate point
            return result;
        }
        else
        {
            return new List<Point> { points[0], points[points.Count - 1] };
        }
    }

    /// <summary>
    /// 计算点到线段的垂直距离
    /// </summary>
    private double PerpendicularDistance(Point point, Point lineStart, Point lineEnd)
    {
        var dx = lineEnd.X - lineStart.X;
        var dy = lineEnd.Y - lineStart.Y;

        if (Math.Abs(dx) < 1e-10 && Math.Abs(dy) < 1e-10)
        {
            return Math.Sqrt(Math.Pow(point.X - lineStart.X, 2) + Math.Pow(point.Y - lineStart.Y, 2));
        }

        var t = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / (dx * dx + dy * dy);
        t = Math.Max(0, Math.Min(1, t));

        var projX = lineStart.X + t * dx;
        var projY = lineStart.Y + t * dy;

        return Math.Sqrt(Math.Pow(point.X - projX, 2) + Math.Pow(point.Y - projY, 2));
    }

    /// <summary>
    /// 更新边界框
    /// </summary>
    private void UpdateBounds()
    {
        if (_points.Count == 0)
        {
            _boundingRect = default;
            return;
        }

        var minX = _points.Min(p => p.X);
        var minY = _points.Min(p => p.Y);
        var maxX = _points.Max(p => p.X);
        var maxY = _points.Max(p => p.Y);

        // Expand bounds to include stroke width
        var strokeHalf = Style.StrokeWidth / 2;
        _boundingRect = new Rect(
            minX - strokeHalf,
            minY - strokeHalf,
            maxX - minX + Style.StrokeWidth,
            maxY - minY + Style.StrokeWidth);
    }

    public override bool HitTest(Point point)
    {
        if (!IsVisible || _points.Count < 2) return false;

        var tolerance = Math.Max(Style.StrokeWidth, 8); // Minimum 8px hit area

        // Check if point is near any line segment
        for (int i = 0; i < _points.Count - 1; i++)
        {
            var distance = PerpendicularDistance(point, _points[i], _points[i + 1]);
            if (distance <= tolerance)
                return true;
        }

        return false;
    }

    protected override void OnMove(Vector offset)
    {
        for (int i = 0; i < _points.Count; i++)
        {
            _points[i] += offset;
        }

        for (int i = 0; i < _smoothedPoints.Count; i++)
        {
            _smoothedPoints[i] += offset;
        }

        _boundingRect = default; // Force recalculation
    }

    protected override void OnScale(double scale, Point center)
    {
        for (int i = 0; i < _points.Count; i++)
        {
            var relative = _points[i] - center;
            _points[i] = center + relative * scale;
        }

        for (int i = 0; i < _smoothedPoints.Count; i++)
        {
            var relative = _smoothedPoints[i] - center;
            _smoothedPoints[i] = center + relative * scale;
        }

        _boundingRect = default; // Force recalculation
    }

    protected override void OnRotate(double angle, Point center)
    {
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);

        for (int i = 0; i < _points.Count; i++)
        {
            var relative = _points[i] - center;
            _points[i] = center + new Vector(
                relative.X * cos - relative.Y * sin,
                relative.X * sin + relative.Y * cos);
        }

        for (int i = 0; i < _smoothedPoints.Count; i++)
        {
            var relative = _smoothedPoints[i] - center;
            _smoothedPoints[i] = center + new Vector(
                relative.X * cos - relative.Y * sin,
                relative.X * sin + relative.Y * cos);
        }

        _boundingRect = default; // Force recalculation
    }

    public override IAnnotationItem Clone()
    {
        var clone = new FreehandAnnotation(Style.Clone())
        {
            ZIndex = ZIndex,
            IsVisible = IsVisible,
            IsLocked = IsLocked
        };

        clone._points = new List<Point>(_points);
        clone._smoothedPoints = new List<Point>(_smoothedPoints);
        clone._boundingRect = _boundingRect;

        return clone;
    }

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();

        // Serialize points
        var pointsData = _points.Select(p => new { X = p.X, Y = p.Y }).ToArray();
        data["Points"] = pointsData;

        var smoothedPointsData = _smoothedPoints.Select(p => new { X = p.X, Y = p.Y }).ToArray();
        data["SmoothedPoints"] = smoothedPointsData;

        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);

        // Deserialize points
        if (data.TryGetValue("Points", out var pointsObj))
        {
            // Handle both array and JSON scenarios
            // This is a simplified version - in real implementation you'd use proper JSON deserialization
            _points.Clear();
            // TODO: Implement proper point deserialization based on your serialization format
        }

        UpdateBounds();
    }
}
