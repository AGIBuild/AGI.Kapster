using AGI.Captor.Desktop.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Collections.Generic;

namespace AGI.Captor.Desktop.Rendering;

/// <summary>
/// 标注渲染器接口
/// </summary>
public interface IAnnotationRenderer
{
    /// <summary>
    /// 渲染单个标注项到Canvas
    /// </summary>
    /// <param name="canvas">目标Canvas</param>
    /// <param name="item">要渲染的标注项</param>
    void Render(Canvas canvas, IAnnotationItem item);
    
    /// <summary>
    /// 批量渲染标注项
    /// </summary>
    /// <param name="canvas">目标Canvas</param>
    /// <param name="items">要渲染的标注项集合</param>
    void RenderAll(Canvas canvas, IEnumerable<IAnnotationItem> items);
    
    /// <summary>
    /// Incremental render: only re-render items intersecting the dirty rectangle
    /// </summary>
    /// <param name="canvas">Target canvas</param>
    /// <param name="items">All items on the canvas (used to find affected ones)</param>
    /// <param name="dirtyRect">Dirty rectangle in canvas coordinates</param>
    void RenderChanged(Canvas canvas, IEnumerable<IAnnotationItem> items, Rect dirtyRect);
    
    /// <summary>
    /// 清除Canvas上的所有渲染内容
    /// </summary>
    /// <param name="canvas">目标Canvas</param>
    void Clear(Canvas canvas);
    
    /// <summary>
    /// 移除特定标注项的渲染
    /// </summary>
    /// <param name="canvas">目标Canvas</param>
    /// <param name="item">要移除的标注项</param>
    void RemoveRender(Canvas canvas, IAnnotationItem item);
}

/// <summary>
/// 标注渲染选项
/// </summary>
public class AnnotationRenderOptions
{
    /// <summary>
    /// 是否显示选择手柄
    /// </summary>
    public bool ShowSelectionHandles { get; set; } = true;
    
    /// <summary>
    /// 选择手柄大小
    /// </summary>
    public double HandleSize { get; set; } = 8.0;
    
    /// <summary>
    /// 选择框颜色
    /// </summary>
    public Color SelectionColor { get; set; } = Colors.Blue;
    
    /// <summary>
    /// 是否显示边界框
    /// </summary>
    public bool ShowBounds { get; set; } = false;
    
    /// <summary>
    /// 边界框颜色
    /// </summary>
    public Color BoundsColor { get; set; } = Colors.Gray;
}
