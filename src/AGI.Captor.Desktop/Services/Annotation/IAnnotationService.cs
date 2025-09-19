using AGI.Captor.Desktop.Models;
using Avalonia;
using System;

namespace AGI.Captor.Desktop.Services.Annotation;

/// <summary>
/// 标注工具类型
/// </summary>
public enum AnnotationToolType
{
    None,        // 选择工具
    Rectangle,   // 矩形工具
    Ellipse,     // 椭圆工具
    Arrow,       // 箭头工具
    Text,        // 文本工具
    Freehand,    // 自由画笔
    Emoji        // Emoji工具
}

/// <summary>
/// 标注服务接口
/// </summary>
public interface IAnnotationService
{
    /// <summary>
    /// 标注管理器
    /// </summary>
    AnnotationManager Manager { get; }

    /// <summary>
    /// 当前选择的工具
    /// </summary>
    AnnotationToolType CurrentTool { get; set; }

    /// <summary>
    /// 当前样式
    /// </summary>
    IAnnotationStyle CurrentStyle { get; set; }

    /// <summary>
    /// 工具变更事件
    /// </summary>
    event EventHandler<ToolChangedEventArgs>? ToolChanged;

    /// <summary>
    /// 样式变更事件
    /// </summary>
    event EventHandler<StyleChangedEventArgs>? StyleChanged;

    /// <summary>
    /// 开始创建标注
    /// </summary>
    /// <param name="startPoint">起始点</param>
    /// <returns>正在创建的标注项，如果不支持则返回null</returns>
    IAnnotationItem? StartAnnotation(Point startPoint);

    /// <summary>
    /// 更新正在创建的标注
    /// </summary>
    /// <param name="currentPoint">当前点</param>
    /// <param name="item">正在创建的标注项</param>
    void UpdateAnnotation(Point currentPoint, IAnnotationItem item);

    /// <summary>
    /// 完成创建标注
    /// </summary>
    /// <param name="item">正在创建的标注项</param>
    /// <returns>是否成功完成创建</returns>
    bool FinishCreate(IAnnotationItem item);

    /// <summary>
    /// 取消创建标注
    /// </summary>
    /// <param name="item">正在创建的标注项</param>
    void CancelCreate(IAnnotationItem item);

    /// <summary>
    /// 点击测试
    /// </summary>
    /// <param name="point">测试点</param>
    /// <returns>命中的标注项</returns>
    IAnnotationItem? HitTest(Point point);

    /// <summary>
    /// 区域测试
    /// </summary>
    /// <param name="region">测试区域</param>
    /// <returns>命中的标注项集合</returns>
    System.Collections.Generic.IEnumerable<IAnnotationItem> HitTest(Rect region);

}

/// <summary>
/// 工具变更事件参数
/// </summary>
public class ToolChangedEventArgs : EventArgs
{
    public AnnotationToolType OldTool { get; }
    public AnnotationToolType NewTool { get; }

    public ToolChangedEventArgs(AnnotationToolType oldTool, AnnotationToolType newTool)
    {
        OldTool = oldTool;
        NewTool = newTool;
    }
}

/// <summary>
/// 样式变更事件参数
/// </summary>
public class StyleChangedEventArgs : EventArgs
{
    public IAnnotationStyle OldStyle { get; }
    public IAnnotationStyle NewStyle { get; }

    public StyleChangedEventArgs(IAnnotationStyle oldStyle, IAnnotationStyle newStyle)
    {
        OldStyle = oldStyle;
        NewStyle = newStyle;
    }
}
