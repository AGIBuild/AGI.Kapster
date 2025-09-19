using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace AGI.Captor.Desktop.Models;

/// <summary>
/// 椭圆标注项
/// </summary>
public class EllipseAnnotation : AnnotationItemBase
{
    private Rect _boundingRect;

    public override AnnotationType Type => AnnotationType.Ellipse;

    /// <summary>
    /// 椭圆的包围矩形
    /// </summary>
    public Rect BoundingRect
    {
        get => _boundingRect;
        set
        {
            _boundingRect = value;
            ModifiedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// 椭圆中心点
    /// </summary>
    public Point Center
    {
        get => _boundingRect.Center;
        set
        {
            var offset = value - _boundingRect.Center;
            _boundingRect = new Rect(_boundingRect.Position + offset, _boundingRect.Size);
        }
    }

    /// <summary>
    /// 水平半径
    /// </summary>
    public double RadiusX
    {
        get => _boundingRect.Width / 2;
        set => _boundingRect = _boundingRect.WithWidth(value * 2);
    }

    /// <summary>
    /// 垂直半径
    /// </summary>
    public double RadiusY
    {
        get => _boundingRect.Height / 2;
        set => _boundingRect = _boundingRect.WithHeight(value * 2);
    }

    public override Rect Bounds
    {
        get
        {
            // 扩展边界以包含描边宽度
            var strokeHalf = Style.StrokeWidth / 2;
            return _boundingRect.Inflate(strokeHalf);
        }
    }

    public EllipseAnnotation(Rect boundingRect, IAnnotationStyle? style = null)
        : base(style ?? AnnotationStyle.CreateShapeStyle(Color.FromRgb(255, 0, 0), 2.0))
    {
        _boundingRect = boundingRect;
    }

    public EllipseAnnotation(Point center, double radiusX, double radiusY, IAnnotationStyle? style = null)
        : this(new Rect(center.X - radiusX, center.Y - radiusY, radiusX * 2, radiusY * 2), style)
    {
    }

    public override bool HitTest(Point point)
    {
        if (!IsVisible) return false;

        var center = _boundingRect.Center;
        var radiusX = RadiusX;
        var radiusY = RadiusY;

        // 标准化坐标到椭圆坐标系
        var normalizedX = (point.X - center.X) / radiusX;
        var normalizedY = (point.Y - center.Y) / radiusY;
        var distanceSquared = normalizedX * normalizedX + normalizedY * normalizedY;

        // 如果有填充，检查是否在椭圆内部
        if (Style.FillMode != FillMode.None && distanceSquared <= 1.0)
            return true;

        // 检查是否在描边附近 - 增大选择区域提高可选择性
        var strokeThickness = Math.Max(Style.StrokeWidth / Math.Min(radiusX, radiusY), 0.1); // 最小选择区域
        var innerThreshold = Math.Max(0, 1.0 - strokeThickness);
        var outerThreshold = 1.0 + strokeThickness * 1.5; // 增大外边界

        return distanceSquared >= innerThreshold && distanceSquared <= outerThreshold;
    }

    protected override void OnMove(Vector offset)
    {
        _boundingRect = new Rect(_boundingRect.Position + offset, _boundingRect.Size);
    }

    protected override void OnScale(double scale, Point center)
    {
        var topLeft = _boundingRect.TopLeft;
        var bottomRight = _boundingRect.BottomRight;

        var newTopLeft = center + (topLeft - center) * scale;
        var newBottomRight = center + (bottomRight - center) * scale;

        _boundingRect = new Rect(newTopLeft, newBottomRight);
    }

    protected override void OnRotate(double angle, Point center)
    {
        // 椭圆旋转需要特殊处理，这里暂时通过移动中心点实现简单旋转
        var ellipseCenter = _boundingRect.Center;
        var relative = ellipseCenter - center;

        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);

        var newCenter = center + new Vector(
            relative.X * cos - relative.Y * sin,
            relative.X * sin + relative.Y * cos
        );

        Center = newCenter;
    }

    public override IAnnotationItem Clone()
    {
        return new EllipseAnnotation(_boundingRect, Style.Clone())
        {
            ZIndex = ZIndex,
            IsVisible = IsVisible,
            IsLocked = IsLocked
        };
    }

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["X"] = _boundingRect.X;
        data["Y"] = _boundingRect.Y;
        data["Width"] = _boundingRect.Width;
        data["Height"] = _boundingRect.Height;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);

        var x = data.TryGetValue("X", out var xVal) ? Convert.ToDouble(xVal) : 0;
        var y = data.TryGetValue("Y", out var yVal) ? Convert.ToDouble(yVal) : 0;
        var width = data.TryGetValue("Width", out var wVal) ? Convert.ToDouble(wVal) : 0;
        var height = data.TryGetValue("Height", out var hVal) ? Convert.ToDouble(hVal) : 0;

        _boundingRect = new Rect(x, y, width, height);
    }
}
