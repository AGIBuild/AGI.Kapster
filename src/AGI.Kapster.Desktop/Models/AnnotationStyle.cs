using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace AGI.Kapster.Desktop.Models;

/// <summary>
/// 标注样式的默认实现
/// </summary>
public class AnnotationStyle : IAnnotationStyle
{
    public Color StrokeColor { get; set; } = Colors.Red;
    public double StrokeWidth { get; set; } = 2.0;
    public LineStyle LineStyle { get; set; } = LineStyle.Solid;
    public Color FillColor { get; set; } = Colors.Transparent;
    public FillMode FillMode { get; set; } = FillMode.None;
    public double Opacity { get; set; } = 1.0;
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 16.0;
    public FontWeight FontWeight { get; set; } = FontWeight.Normal;
    public FontStyle FontStyle { get; set; } = FontStyle.Normal;
    public TextAlignment TextAlignment { get; set; } = TextAlignment.Left;
    public Vector ShadowOffset { get; set; } = new Vector(2, 2);
    public double ShadowBlur { get; set; } = 4.0;
    public Color ShadowColor { get; set; } = Color.FromArgb(128, 0, 0, 0);

    /// <summary>
    /// 创建默认样式
    /// </summary>
    public AnnotationStyle()
    {
    }

    /// <summary>
    /// 创建指定颜色和宽度的样式
    /// </summary>
    public AnnotationStyle(Color strokeColor, double strokeWidth)
    {
        StrokeColor = strokeColor;
        StrokeWidth = strokeWidth;
    }

    /// <summary>
    /// 创建文本样式
    /// </summary>
    public static AnnotationStyle CreateTextStyle(Color color, double fontSize, string fontFamily = "Segoe UI")
    {
        return new AnnotationStyle
        {
            StrokeColor = color,
            FillColor = color,
            FontSize = fontSize,
            FontFamily = fontFamily,
            StrokeWidth = 0, // 文本通常不需要描边
            FillMode = FillMode.Solid
        };
    }

    /// <summary>
    /// 创建箭头样式
    /// </summary>
    public static AnnotationStyle CreateArrowStyle(Color color, double width)
    {
        return new AnnotationStyle
        {
            StrokeColor = color,
            FillColor = color,
            StrokeWidth = width,
            FillMode = FillMode.Solid,
            ShadowOffset = new Vector(1.5, 1.5),
            ShadowBlur = 3.0,
            ShadowColor = Color.FromArgb(80, 0, 0, 0)
        };
    }

    /// <summary>
    /// 创建形状样式
    /// </summary>
    public static AnnotationStyle CreateShapeStyle(Color strokeColor, double strokeWidth, Color? fillColor = null)
    {
        return new AnnotationStyle
        {
            StrokeColor = strokeColor,
            StrokeWidth = strokeWidth,
            FillColor = fillColor ?? Colors.Transparent,
            FillMode = fillColor.HasValue ? FillMode.Solid : FillMode.None
        };
    }

    public IAnnotationStyle Clone()
    {
        return new AnnotationStyle
        {
            StrokeColor = StrokeColor,
            StrokeWidth = StrokeWidth,
            LineStyle = LineStyle,
            FillColor = FillColor,
            FillMode = FillMode,
            Opacity = Opacity,
            FontFamily = FontFamily,
            FontSize = FontSize,
            FontWeight = FontWeight,
            FontStyle = FontStyle,
            TextAlignment = TextAlignment,
            ShadowOffset = ShadowOffset,
            ShadowBlur = ShadowBlur,
            ShadowColor = ShadowColor
        };
    }

    public Dictionary<string, object> Serialize()
    {
        return new Dictionary<string, object>
        {
            ["StrokeColor"] = StrokeColor.ToString(),
            ["StrokeWidth"] = StrokeWidth,
            ["LineStyle"] = LineStyle.ToString(),
            ["FillColor"] = FillColor.ToString(),
            ["FillMode"] = FillMode.ToString(),
            ["Opacity"] = Opacity,
            ["FontFamily"] = FontFamily,
            ["FontSize"] = FontSize,
            ["FontWeight"] = FontWeight.ToString(),
            ["FontStyle"] = FontStyle.ToString(),
            ["TextAlignment"] = TextAlignment.ToString(),
            ["ShadowOffsetX"] = ShadowOffset.X,
            ["ShadowOffsetY"] = ShadowOffset.Y,
            ["ShadowBlur"] = ShadowBlur,
            ["ShadowColor"] = ShadowColor.ToString()
        };
    }

    public void Deserialize(Dictionary<string, object> data)
    {
        if (data.TryGetValue("StrokeColor", out var strokeColor))
            StrokeColor = Color.Parse(strokeColor.ToString()!);
        if (data.TryGetValue("StrokeWidth", out var strokeWidth))
            StrokeWidth = (double)strokeWidth;
        if (data.TryGetValue("LineStyle", out var lineStyle))
            LineStyle = Enum.Parse<LineStyle>(lineStyle.ToString()!);
        if (data.TryGetValue("FillColor", out var fillColor))
            FillColor = Color.Parse(fillColor.ToString()!);
        if (data.TryGetValue("FillMode", out var fillMode))
            FillMode = Enum.Parse<FillMode>(fillMode.ToString()!);
        if (data.TryGetValue("Opacity", out var opacity))
            Opacity = (double)opacity;
        if (data.TryGetValue("FontFamily", out var fontFamily))
            FontFamily = fontFamily.ToString()!;
        if (data.TryGetValue("FontSize", out var fontSize))
            FontSize = (double)fontSize;
        if (data.TryGetValue("FontWeight", out var fontWeight))
            FontWeight = Enum.Parse<FontWeight>(fontWeight.ToString()!);
        if (data.TryGetValue("FontStyle", out var fontStyle))
            FontStyle = Enum.Parse<Avalonia.Media.FontStyle>(fontStyle.ToString()!);
        if (data.TryGetValue("TextAlignment", out var textAlignment))
            TextAlignment = Enum.Parse<TextAlignment>(textAlignment.ToString()!);
        if (data.TryGetValue("ShadowOffsetX", out var shadowX) && data.TryGetValue("ShadowOffsetY", out var shadowY))
            ShadowOffset = new Vector((double)shadowX, (double)shadowY);
        if (data.TryGetValue("ShadowBlur", out var shadowBlur))
            ShadowBlur = (double)shadowBlur;
        if (data.TryGetValue("ShadowColor", out var shadowColor))
            ShadowColor = Color.Parse(shadowColor.ToString()!);
    }
}
