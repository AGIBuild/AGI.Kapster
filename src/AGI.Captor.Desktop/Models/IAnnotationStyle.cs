using Avalonia;
using Avalonia.Media;
using System.Collections.Generic;

namespace AGI.Captor.Desktop.Models;

/// <summary>
/// 线条样式枚举
/// </summary>
public enum LineStyle
{
    Solid,      // 实线
    Dashed,     // 虚线
    Dotted,     // 点线
    DashDot     // 点划线
}

/// <summary>
/// 填充模式枚举
/// </summary>
public enum FillMode
{
    None,       // 无填充
    Solid,      // 实填充
    Gradient,   // 渐变填充
    Pattern     // 图案填充
}

/// <summary>
/// 文本对齐枚举
/// </summary>
public enum TextAlignment
{
    Left,
    Center,
    Right,
    Justify
}

/// <summary>
/// 标注样式接口
/// </summary>
public interface IAnnotationStyle
{
    /// <summary>
    /// 描边颜色
    /// </summary>
    Color StrokeColor { get; set; }

    /// <summary>
    /// 描边宽度
    /// </summary>
    double StrokeWidth { get; set; }

    /// <summary>
    /// 线条样式
    /// </summary>
    LineStyle LineStyle { get; set; }

    /// <summary>
    /// 填充颜色
    /// </summary>
    Color FillColor { get; set; }

    /// <summary>
    /// 填充模式
    /// </summary>
    FillMode FillMode { get; set; }

    /// <summary>
    /// 透明度 (0.0 - 1.0)
    /// </summary>
    double Opacity { get; set; }

    /// <summary>
    /// 字体族（用于文本）
    /// </summary>
    string FontFamily { get; set; }

    /// <summary>
    /// 字体大小（用于文本）
    /// </summary>
    double FontSize { get; set; }

    /// <summary>
    /// 字体粗细（用于文本）
    /// </summary>
    FontWeight FontWeight { get; set; }

    /// <summary>
    /// 字体样式（用于文本）
    /// </summary>
    FontStyle FontStyle { get; set; }

    /// <summary>
    /// 文本对齐（用于文本）
    /// </summary>
    TextAlignment TextAlignment { get; set; }

    /// <summary>
    /// 阴影偏移
    /// </summary>
    Vector ShadowOffset { get; set; }

    /// <summary>
    /// 阴影模糊半径
    /// </summary>
    double ShadowBlur { get; set; }

    /// <summary>
    /// 阴影颜色
    /// </summary>
    Color ShadowColor { get; set; }

    /// <summary>
    /// 克隆样式
    /// </summary>
    /// <returns>新的样式对象</returns>
    IAnnotationStyle Clone();

    /// <summary>
    /// 序列化样式
    /// </summary>
    /// <returns>样式属性字典</returns>
    Dictionary<string, object> Serialize();

    /// <summary>
    /// 从字典反序列化样式
    /// </summary>
    /// <param name="data">样式属性字典</param>
    void Deserialize(Dictionary<string, object> data);
}
