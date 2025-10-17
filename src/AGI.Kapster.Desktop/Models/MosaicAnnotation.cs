using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AGI.Kapster.Desktop.Models;

/// <summary>
/// Mosaic annotation item for pixelating/blurring regions with brush-like strokes
/// </summary>
public class MosaicAnnotation : AnnotationItemBase
{
    private List<Point> _points = new();
    private Rect _boundingRect;
    private int _brushSize = 20; // Default brush diameter
    private int _pixelSize = 8;  // Default mosaic pixel size
    private HashSet<(int, int)> _renderedCells = new(); // Track rendered grid cells for incremental rendering

    public override AnnotationType Type => AnnotationType.Mosaic;

    /// <summary>
    /// Trail points where mosaic effect is applied
    /// </summary>
    public IReadOnlyList<Point> Points => _points.AsReadOnly();

    /// <summary>
    /// Rendered cells (for incremental rendering)
    /// </summary>
    public HashSet<(int, int)> RenderedCells => _renderedCells;

    /// <summary>
    /// Brush size (diameter of the mosaic brush stroke)
    /// Gets automatically scaled by StrokeWidth (Size slider)
    /// Uses linear-then-saturate function for smooth scaling
    /// </summary>
    public int BrushSize
    {
        get
        {
            // Use StrokeWidth as size input (typically 1-20)
            var userSize = Math.Clamp(Style.StrokeWidth, 1.0, 20.0);
            
            // Linear-then-saturate function with smooth transition
            // size 1-12: linear growth for clear differentiation
            // size 12-20: smooth saturation to prevent excessive size
            double LinearThenSaturate(double input, double min, double max, double linearThreshold = 12.0)
            {
                if (input <= linearThreshold)
                {
                    // Linear phase: size 1-12 maps linearly to min to ~75% of range
                    var linearRatio = (input - 1.0) / (linearThreshold - 1.0);
                    return min + (max - min) * 0.75 * linearRatio;
                }
                else
                {
                    // Saturation phase: size 12-20 maps smoothly from 75% to 100%
                    var saturationInput = (input - linearThreshold) / (20.0 - linearThreshold);
                    var saturationValue = 1.0 - Math.Exp(-3.0 * saturationInput); // Smooth approach to 1.0
                    return min + (max - min) * (0.75 + 0.25 * saturationValue);
                }
            }
            
            // Brush size: range 14-56px
            // size=1: 14px (readable font size)
            // size=6: ~28.5px
            // size=12: 45.5px
            // size=20: ~55.5px
            var brushSize = LinearThenSaturate(userSize, 14.0, 56.0, 12.0);
            return (int)Math.Round(brushSize);
        }
        set
        {
            _brushSize = Math.Max(5, Math.Min(100, value)); // Clamp between 5-100
            ModifiedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// Size of mosaic pixels (larger = more pixelated)
    /// </summary>
    public int PixelSize
    {
        get => _pixelSize;
        set
        {
            _pixelSize = Math.Max(2, Math.Min(20, value)); // Clamp between 2-20
            ModifiedAt = DateTime.Now;
        }
    }

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

    public MosaicAnnotation(IAnnotationStyle? style = null, int brushSize = 20, int pixelSize = 8)
        : base(style ?? AnnotationStyle.CreateShapeStyle(Color.FromRgb(128, 128, 128), 10))
    {
        _brushSize = Math.Max(5, Math.Min(100, brushSize));
        _pixelSize = Math.Max(2, Math.Min(20, pixelSize));
    }

    /// <summary>
    /// Add new point to the mosaic trail
    /// </summary>
    public void AddPoint(Point point)
    {
        _points.Add(point);
        _boundingRect = default; // Force recalculation
        ModifiedAt = DateTime.Now;
    }

    /// <summary>
    /// Add multiple points to the trail
    /// </summary>
    public void AddPoints(IEnumerable<Point> points)
    {
        _points.AddRange(points);
        _boundingRect = default; // Force recalculation
        ModifiedAt = DateTime.Now;
    }

    /// <summary>
    /// Update bounding rectangle based on trail points
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

        // Expand bounds to include brush size
        var brushHalf = BrushSize / 2.0;
        _boundingRect = new Rect(
            minX - brushHalf,
            minY - brushHalf,
            maxX - minX + BrushSize,
            maxY - minY + BrushSize);
    }

    public override bool HitTest(Point point)
    {
        if (!IsVisible || _points.Count == 0) return false;

        var tolerance = Math.Max(BrushSize / 2.0, 10); // Minimum 10px hit area

        // Check if point is near any trail point
        foreach (var trailPoint in _points)
        {
            var distance = Math.Sqrt(
                Math.Pow(point.X - trailPoint.X, 2) + 
                Math.Pow(point.Y - trailPoint.Y, 2));
            
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
        _boundingRect = default; // Force recalculation
    }

    protected override void OnScale(double scale, Point center)
    {
        for (int i = 0; i < _points.Count; i++)
        {
            var relative = _points[i] - center;
            _points[i] = center + relative * scale;
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
        _boundingRect = default; // Force recalculation
    }

    public override IAnnotationItem Clone()
    {
        var clone = new MosaicAnnotation(Style.Clone(), _brushSize, _pixelSize)
        {
            ZIndex = ZIndex,
            IsVisible = IsVisible,
            IsLocked = IsLocked
        };

        clone._points = new List<Point>(_points);
        clone._boundingRect = _boundingRect;

        return clone;
    }

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        
        var pointsData = _points.Select(p => new { X = p.X, Y = p.Y }).ToArray();
        data["Points"] = pointsData;
        data["BrushSize"] = _brushSize;
        data["PixelSize"] = _pixelSize;
        
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);

        if (data.TryGetValue("Points", out var pointsObj))
        {
            _points.Clear();
            // Deserialize points from the expected format: array of objects with X and Y properties
            if (pointsObj is IEnumerable<object> pointsEnumerable)
            {
                foreach (var pointItem in pointsEnumerable)
                {
                    // Try to handle both Dictionary<string, object> and dynamic objects
                    double x = 0, y = 0;
                    if (pointItem is Dictionary<string, object> dict)
                    {
                        if (dict.TryGetValue("X", out var xObj)) x = Convert.ToDouble(xObj);
                        if (dict.TryGetValue("Y", out var yObj)) y = Convert.ToDouble(yObj);
                    }
                    else
                    {
                        var type = pointItem.GetType();
                        var xProp = type.GetProperty("X");
                        var yProp = type.GetProperty("Y");
                        if (xProp != null) x = Convert.ToDouble(xProp.GetValue(pointItem));
                        if (yProp != null) y = Convert.ToDouble(yProp.GetValue(pointItem));
                    }
                    _points.Add(new Point(x, y));
                }
            }
        }

        var brushSize = data.TryGetValue("BrushSize", out var bVal) ? Convert.ToInt32(bVal) : 20;
        var pixelSize = data.TryGetValue("PixelSize", out var pVal) ? Convert.ToInt32(pVal) : 8;

        _brushSize = Math.Max(5, Math.Min(100, brushSize));
        _pixelSize = Math.Max(2, Math.Min(20, pixelSize));

        UpdateBounds();
    }
}
