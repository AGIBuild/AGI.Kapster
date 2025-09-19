using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace AGI.Captor.Desktop.Models;

/// <summary>
/// 标注项工厂类
/// </summary>
public static class AnnotationFactory
{
    /// <summary>
    /// 创建箭头标注
    /// </summary>
    public static ArrowAnnotation CreateArrow(Point startPoint, Point endPoint, IAnnotationStyle? style = null)
    {
        return new ArrowAnnotation(startPoint, endPoint, style);
    }

    /// <summary>
    /// 创建矩形标注
    /// </summary>
    public static RectangleAnnotation CreateRectangle(Point topLeft, Point bottomRight, IAnnotationStyle? style = null)
    {
        return new RectangleAnnotation(topLeft, bottomRight, style);
    }

    /// <summary>
    /// 创建矩形标注（从中心点和尺寸）
    /// </summary>
    public static RectangleAnnotation CreateRectangle(Point center, Size size, IAnnotationStyle? style = null)
    {
        var halfWidth = size.Width / 2;
        var halfHeight = size.Height / 2;
        var topLeft = new Point(center.X - halfWidth, center.Y - halfHeight);
        var bottomRight = new Point(center.X + halfWidth, center.Y + halfHeight);
        return new RectangleAnnotation(topLeft, bottomRight, style);
    }

    /// <summary>
    /// 创建椭圆标注
    /// </summary>
    public static EllipseAnnotation CreateEllipse(Point center, double radiusX, double radiusY, IAnnotationStyle? style = null)
    {
        return new EllipseAnnotation(center, radiusX, radiusY, style);
    }

    /// <summary>
    /// 创建椭圆标注（从包围矩形）
    /// </summary>
    public static EllipseAnnotation CreateEllipse(Rect boundingRect, IAnnotationStyle? style = null)
    {
        return new EllipseAnnotation(boundingRect, style);
    }

    /// <summary>
    /// 创建文本标注
    /// </summary>
    public static TextAnnotation CreateText(Point position, string text = "", IAnnotationStyle? style = null)
    {
        return new TextAnnotation(position, text, style);
    }

    /// <summary>
    /// 创建自由画笔标注
    /// </summary>
    public static FreehandAnnotation CreateFreehand(IAnnotationStyle? style = null)
    {
        return new FreehandAnnotation(style);
    }

    /// <summary>
    /// 创建Emoji标注
    /// </summary>
    public static EmojiAnnotation CreateEmoji(Point position, string emoji = "😀", IAnnotationStyle? style = null)
    {
        return new EmojiAnnotation(position, emoji, style);
    }

    /// <summary>
    /// 从序列化数据创建标注项
    /// </summary>
    public static IAnnotationItem? CreateFromData(Dictionary<string, object> data)
    {
        if (!data.TryGetValue("Type", out var typeValue))
            return null;

        if (!Enum.TryParse<AnnotationType>(typeValue.ToString(), out var type))
            return null;

        IAnnotationItem? item = type switch
        {
            AnnotationType.Arrow => new ArrowAnnotation(new Point(0, 0), new Point(0, 0)),
            AnnotationType.Rectangle => new RectangleAnnotation(new Rect(0, 0, 0, 0)),
            AnnotationType.Ellipse => new EllipseAnnotation(new Rect(0, 0, 0, 0)),
            AnnotationType.Text => new TextAnnotation(new Point(0, 0)),
            AnnotationType.Freehand => new FreehandAnnotation(),
            AnnotationType.Emoji => new EmojiAnnotation(new Point(0, 0)),
            _ => null
        };

        item?.Deserialize(data);
        return item;
    }

    /// <summary>
    /// 获取默认样式
    /// </summary>
    public static IAnnotationStyle GetDefaultStyle(AnnotationType type)
    {
        return type switch
        {
            AnnotationType.Arrow => AnnotationStyle.CreateArrowStyle(Color.FromRgb(255, 0, 0), 3.0),
            AnnotationType.Rectangle => AnnotationStyle.CreateShapeStyle(Color.FromRgb(255, 0, 0), 2.0),
            AnnotationType.Ellipse => AnnotationStyle.CreateShapeStyle(Color.FromRgb(255, 0, 0), 2.0),
            AnnotationType.Text => AnnotationStyle.CreateTextStyle(Color.FromRgb(255, 0, 0), 16),
            AnnotationType.Freehand => AnnotationStyle.CreateShapeStyle(Color.FromRgb(255, 0, 0), 3.0),
            AnnotationType.Emoji => AnnotationStyle.CreateTextStyle(Color.FromRgb(255, 255, 255), 32),
            _ => new AnnotationStyle()
        };
    }

    /// <summary>
    /// 创建样式的变体
    /// </summary>
    public static IAnnotationStyle CreateStyleVariant(IAnnotationStyle baseStyle, Color? color = null, double? strokeWidth = null, double? fontSize = null)
    {
        var newStyle = baseStyle.Clone();

        if (color.HasValue)
        {
            newStyle.StrokeColor = color.Value;
            if (newStyle.FillMode != FillMode.None)
                newStyle.FillColor = color.Value;
        }

        if (strokeWidth.HasValue)
            newStyle.StrokeWidth = strokeWidth.Value;

        if (fontSize.HasValue)
            newStyle.FontSize = fontSize.Value;

        return newStyle;
    }

    /// <summary>
    /// 预定义的颜色调色板
    /// </summary>
    public static readonly Color[] ColorPalette =
    {
        Color.FromRgb(255, 0, 0),     // Red
        Color.FromRgb(255, 165, 0),   // Orange
        Color.FromRgb(255, 255, 0),   // Yellow
        Color.FromRgb(0, 128, 0),     // Green
        Color.FromRgb(0, 0, 255),     // Blue
        Color.FromRgb(128, 0, 128),   // Purple
        Color.FromRgb(255, 192, 203), // Pink
        Color.FromRgb(0, 0, 0),       // Black
        Color.FromRgb(128, 128, 128), // Gray
        Color.FromRgb(255, 255, 255)  // White
    };

    /// <summary>
    /// 预定义的线宽选项
    /// </summary>
    public static readonly double[] StrokeWidths = { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 8.0, 10.0 };

    /// <summary>
    /// 预定义的字体大小选项
    /// </summary>
    public static readonly double[] FontSizes = { 10, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48 };
}
