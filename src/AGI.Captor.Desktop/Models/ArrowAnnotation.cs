using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace AGI.Captor.Desktop.Models;

/// <summary>
/// 箭头标注项
/// </summary>
public class ArrowAnnotation : AnnotationItemBase
{
    private Point _startPoint;
    private Point _endPoint;

    public override AnnotationType Type => AnnotationType.Arrow;

    /// <summary>
    /// 箭头起始点
    /// </summary>
    public Point StartPoint
    {
        get => _startPoint;
        set
        {
            _startPoint = value;
            ModifiedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// 箭头结束点（箭头尖端）
    /// </summary>
    public Point EndPoint
    {
        get => _endPoint;
        set
        {
            _endPoint = value;
            ModifiedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// 箭头长度
    /// </summary>
    public double Length => Vector.Distance(_startPoint, _endPoint);

    /// <summary>
    /// 箭头角度（弧度）
    /// </summary>
    public double Angle => Math.Atan2(_endPoint.Y - _startPoint.Y, _endPoint.X - _startPoint.X);

    public override Rect Bounds
    {
        get
        {
            var minX = Math.Min(_startPoint.X, _endPoint.X);
            var minY = Math.Min(_startPoint.Y, _endPoint.Y);
            var maxX = Math.Max(_startPoint.X, _endPoint.X);
            var maxY = Math.Max(_startPoint.Y, _endPoint.Y);
            
            // 扩展边界以包含箭头头部和描边宽度
            var headSize = Math.Max(Style.StrokeWidth * 7, 14);
            var padding = Math.Max(Style.StrokeWidth / 2, headSize / 2);
            
            return new Rect(
                minX - padding,
                minY - padding,
                maxX - minX + 2 * padding,
                maxY - minY + 2 * padding
            );
        }
    }

    public ArrowAnnotation(Point startPoint, Point endPoint, IAnnotationStyle? style = null)
        : base(style ?? AnnotationStyle.CreateArrowStyle(Color.FromRgb(255, 0, 0), 3.0))
    {
        _startPoint = startPoint;
        _endPoint = endPoint;
    }

    public override bool HitTest(Point point)
    {
        if (!IsVisible) return false;
        
        // 检查是否在包围盒内
        if (!IsPointInRect(point, Bounds)) return false;
        
        // 检查是否靠近箭头线段 - 增大选择阈值提高可选择性
        var threshold = Math.Max(Style.StrokeWidth * 3, 12);
        return DistanceToLineSegment(point, _startPoint, _endPoint) <= threshold;
    }

    protected override void OnMove(Vector offset)
    {
        _startPoint += offset;
        _endPoint += offset;
    }

    protected override void OnScale(double scale, Point center)
    {
        var startVector = (_startPoint - center) * scale;
        var endVector = (_endPoint - center) * scale;
        _startPoint = center + startVector;
        _endPoint = center + endVector;
    }

    protected override void OnRotate(double angle, Point center)
    {
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        
        // 旋转起始点
        var startRelative = _startPoint - center;
        _startPoint = center + new Vector(
            startRelative.X * cos - startRelative.Y * sin,
            startRelative.X * sin + startRelative.Y * cos
        );
        
        // 旋转结束点
        var endRelative = _endPoint - center;
        _endPoint = center + new Vector(
            endRelative.X * cos - endRelative.Y * sin,
            endRelative.X * sin + endRelative.Y * cos
        );
    }

    public override IAnnotationItem Clone()
    {
        return new ArrowAnnotation(_startPoint, _endPoint, Style.Clone())
        {
            ZIndex = ZIndex,
            IsVisible = IsVisible,
            IsLocked = IsLocked
        };
    }

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["StartPointX"] = _startPoint.X;
        data["StartPointY"] = _startPoint.Y;
        data["EndPointX"] = _endPoint.X;
        data["EndPointY"] = _endPoint.Y;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        
        if (data.TryGetValue("StartPointX", out var startX) && data.TryGetValue("StartPointY", out var startY))
            _startPoint = new Point(Convert.ToDouble(startX), Convert.ToDouble(startY));
        if (data.TryGetValue("EndPointX", out var endX) && data.TryGetValue("EndPointY", out var endY))
            _endPoint = new Point(Convert.ToDouble(endX), Convert.ToDouble(endY));
    }
}
