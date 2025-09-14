using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AGI.Captor.Desktop.Models;

/// <summary>
/// 矩形标注项
/// </summary>
public class RectangleAnnotation : AnnotationItemBase
{
    private Rect _rectangle;

    public override AnnotationType Type => AnnotationType.Rectangle;

    /// <summary>
    /// 矩形区域
    /// </summary>
    public Rect Rectangle
    {
        get => _rectangle;
        set
        {
            _rectangle = value;
            ModifiedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// 左上角点
    /// </summary>
    public Point TopLeft
    {
        get => _rectangle.TopLeft;
        set => _rectangle = new Rect(value, _rectangle.BottomRight);
    }

    /// <summary>
    /// 右下角点
    /// </summary>
    public Point BottomRight
    {
        get => _rectangle.BottomRight;
        set => _rectangle = new Rect(_rectangle.TopLeft, value);
    }

    /// <summary>
    /// 宽度
    /// </summary>
    public double Width
    {
        get => _rectangle.Width;
        set => _rectangle = _rectangle.WithWidth(value);
    }

    /// <summary>
    /// 高度
    /// </summary>
    public double Height
    {
        get => _rectangle.Height;
        set => _rectangle = _rectangle.WithHeight(value);
    }

    public override Rect Bounds
    {
        get
        {
            // 扩展边界以包含描边宽度
            var strokeHalf = Style.StrokeWidth / 2;
            return _rectangle.Inflate(strokeHalf);
        }
    }

    public RectangleAnnotation(Rect rectangle, IAnnotationStyle? style = null)
        : base(style ?? AnnotationStyle.CreateShapeStyle(Color.FromRgb(255, 0, 0), 2.0))
    {
        _rectangle = rectangle;
    }

    public RectangleAnnotation(Point topLeft, Point bottomRight, IAnnotationStyle? style = null)
        : this(new Rect(topLeft, bottomRight), style)
    {
    }

    public override bool HitTest(Point point)
    {
        if (!IsVisible) return false;
        
        // 如果有填充，检查是否在矩形内部
        if (Style.FillMode != FillMode.None && IsPointInRect(point, _rectangle))
            return true;
        
        // 检查是否在描边附近 - 增大选择区域提高可选择性
        var strokeHalf = Math.Max(Style.StrokeWidth / 2, 6); // 最小6px选择区域
        var outerRect = _rectangle.Inflate(strokeHalf);
        var innerRect = _rectangle.Deflate(strokeHalf);
        
        return IsPointInRect(point, outerRect) && !IsPointInRect(point, innerRect);
    }

    protected override void OnMove(Vector offset)
    {
        _rectangle = new Rect(_rectangle.Position + offset, _rectangle.Size);
    }

    protected override void OnScale(double scale, Point center)
    {
        var topLeft = _rectangle.TopLeft;
        var bottomRight = _rectangle.BottomRight;
        
        var newTopLeft = center + (topLeft - center) * scale;
        var newBottomRight = center + (bottomRight - center) * scale;
        
        _rectangle = new Rect(newTopLeft, newBottomRight);
    }

    protected override void OnRotate(double angle, Point center)
    {
        // 矩形旋转需要转换为多边形，这里暂时简化处理
        // 实际应用中可能需要将矩形转换为自由多边形标注
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        
        var corners = new[]
        {
            _rectangle.TopLeft,
            _rectangle.TopRight,
            _rectangle.BottomRight,
            _rectangle.BottomLeft
        };
        
        for (int i = 0; i < corners.Length; i++)
        {
            var relative = corners[i] - center;
            corners[i] = center + new Vector(
                relative.X * cos - relative.Y * sin,
                relative.X * sin + relative.Y * cos
            );
        }
        
        // 计算旋转后的包围盒
        var minX = corners.Min(p => p.X);
        var minY = corners.Min(p => p.Y);
        var maxX = corners.Max(p => p.X);
        var maxY = corners.Max(p => p.Y);
        
        _rectangle = new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    public override IAnnotationItem Clone()
    {
        return new RectangleAnnotation(_rectangle, Style.Clone())
        {
            ZIndex = ZIndex,
            IsVisible = IsVisible,
            IsLocked = IsLocked
        };
    }

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["X"] = _rectangle.X;
        data["Y"] = _rectangle.Y;
        data["Width"] = _rectangle.Width;
        data["Height"] = _rectangle.Height;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        
        var x = data.TryGetValue("X", out var xVal) ? Convert.ToDouble(xVal) : 0;
        var y = data.TryGetValue("Y", out var yVal) ? Convert.ToDouble(yVal) : 0;
        var width = data.TryGetValue("Width", out var wVal) ? Convert.ToDouble(wVal) : 0;
        var height = data.TryGetValue("Height", out var hVal) ? Convert.ToDouble(hVal) : 0;
        
        _rectangle = new Rect(x, y, width, height);
    }
}
