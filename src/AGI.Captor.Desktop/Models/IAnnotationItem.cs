using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace AGI.Captor.Desktop.Models;

/// <summary>
/// 标注项类型枚举
/// </summary>
public enum AnnotationType
{
    Text,
    Arrow,
    Rectangle,
    Ellipse,
    Freehand,
    Emoji
}

/// <summary>
/// 标注项状态枚举
/// </summary>
public enum AnnotationState
{
    Creating,   // 正在创建
    Normal,     // 正常状态
    Selected,   // 被选中
    Editing     // 正在编辑
}

/// <summary>
/// 标注项基础接口
/// </summary>
public interface IAnnotationItem
{
    /// <summary>
    /// 唯一标识符
    /// </summary>
    Guid Id { get; }
    
    /// <summary>
    /// 标注类型
    /// </summary>
    AnnotationType Type { get; }
    
    /// <summary>
    /// 当前状态
    /// </summary>
    AnnotationState State { get; set; }
    
    /// <summary>
    /// 包围盒（用于碰撞检测和选择）
    /// </summary>
    Rect Bounds { get; }
    
    /// <summary>
    /// Z序（用于层级管理）
    /// </summary>
    int ZIndex { get; set; }
    
    /// <summary>
    /// 是否可见
    /// </summary>
    bool IsVisible { get; set; }
    
    /// <summary>
    /// 是否锁定（锁定后不可编辑）
    /// </summary>
    bool IsLocked { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    DateTime CreatedAt { get; }
    
    /// <summary>
    /// 最后修改时间
    /// </summary>
    DateTime ModifiedAt { get; set; }
    
    /// <summary>
    /// 样式属性
    /// </summary>
    IAnnotationStyle Style { get; set; }
    
    /// <summary>
    /// 点击测试
    /// </summary>
    /// <param name="point">测试点</param>
    /// <returns>是否命中</returns>
    bool HitTest(Point point);
    
    /// <summary>
    /// 移动标注项
    /// </summary>
    /// <param name="offset">偏移量</param>
    void Move(Vector offset);
    
    /// <summary>
    /// 缩放标注项
    /// </summary>
    /// <param name="scale">缩放比例</param>
    /// <param name="center">缩放中心点</param>
    void Scale(double scale, Point center);
    
    /// <summary>
    /// 旋转标注项
    /// </summary>
    /// <param name="angle">旋转角度（弧度）</param>
    /// <param name="center">旋转中心点</param>
    void Rotate(double angle, Point center);
    
    /// <summary>
    /// 克隆标注项
    /// </summary>
    /// <returns>新的标注项</returns>
    IAnnotationItem Clone();
    
    /// <summary>
    /// 序列化为字典（用于保存）
    /// </summary>
    /// <returns>属性字典</returns>
    Dictionary<string, object> Serialize();
    
    /// <summary>
    /// 从字典反序列化（用于加载）
    /// </summary>
    /// <param name="data">属性字典</param>
    void Deserialize(Dictionary<string, object> data);
}
