using Avalonia;
using System;
using System.Collections.Generic;

namespace AGI.Kapster.Desktop.Models;

/// <summary>
/// 标注项基础抽象类
/// </summary>
public abstract class AnnotationItemBase : IAnnotationItem
{
    private AnnotationState _state = AnnotationState.Normal;
    private int _zIndex = 0;
    private bool _isVisible = true;
    private bool _isLocked = false;
    private IAnnotationStyle _style;

    public Guid Id { get; } = Guid.NewGuid();
    public abstract AnnotationType Type { get; }

    public AnnotationState State
    {
        get => _state;
        set
        {
            _state = value;
            ModifiedAt = DateTime.Now;
            OnStateChanged();
        }
    }

    public abstract Rect Bounds { get; }

    public int ZIndex
    {
        get => _zIndex;
        set
        {
            _zIndex = value;
            ModifiedAt = DateTime.Now;
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            _isVisible = value;
            ModifiedAt = DateTime.Now;
        }
    }

    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            _isLocked = value;
            ModifiedAt = DateTime.Now;
        }
    }

    public DateTime CreatedAt { get; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;

    public IAnnotationStyle Style
    {
        get => _style;
        set
        {
            _style = value ?? throw new ArgumentNullException(nameof(value));
            ModifiedAt = DateTime.Now;
            OnStyleChanged();
        }
    }

    protected AnnotationItemBase(IAnnotationStyle? style = null)
    {
        _style = style ?? new AnnotationStyle();
    }

    public abstract bool HitTest(Point point);

    public virtual void Move(Vector offset)
    {
        if (IsLocked) return;
        OnMove(offset);
        ModifiedAt = DateTime.Now;
    }

    public virtual void Scale(double scale, Point center)
    {
        if (IsLocked || scale <= 0) return;
        OnScale(scale, center);
        ModifiedAt = DateTime.Now;
    }

    public virtual void Rotate(double angle, Point center)
    {
        if (IsLocked) return;
        OnRotate(angle, center);
        ModifiedAt = DateTime.Now;
    }

    public abstract IAnnotationItem Clone();

    public virtual Dictionary<string, object> Serialize()
    {
        return new Dictionary<string, object>
        {
            ["Id"] = Id.ToString(),
            ["Type"] = Type.ToString(),
            ["State"] = State.ToString(),
            ["ZIndex"] = ZIndex,
            ["IsVisible"] = IsVisible,
            ["IsLocked"] = IsLocked,
            ["CreatedAt"] = CreatedAt.ToString("O"),
            ["ModifiedAt"] = ModifiedAt.ToString("O"),
            ["Style"] = Style.Serialize()
        };
    }

    public virtual void Deserialize(Dictionary<string, object> data)
    {
        if (data.TryGetValue("State", out var state))
            State = Enum.Parse<AnnotationState>(state.ToString()!);
        if (data.TryGetValue("ZIndex", out var zIndex))
            ZIndex = Convert.ToInt32(zIndex);
        if (data.TryGetValue("IsVisible", out var isVisible))
            IsVisible = Convert.ToBoolean(isVisible);
        if (data.TryGetValue("IsLocked", out var isLocked))
            IsLocked = Convert.ToBoolean(isLocked);
        if (data.TryGetValue("ModifiedAt", out var modifiedAt))
            ModifiedAt = DateTime.Parse(modifiedAt.ToString()!);
        if (data.TryGetValue("Style", out var styleData) && styleData is Dictionary<string, object> styleDictionary)
            Style.Deserialize(styleDictionary);
    }

    /// <summary>
    /// 子类实现具体的移动逻辑
    /// </summary>
    protected abstract void OnMove(Vector offset);

    /// <summary>
    /// 子类实现具体的缩放逻辑
    /// </summary>
    protected abstract void OnScale(double scale, Point center);

    /// <summary>
    /// 子类实现具体的旋转逻辑
    /// </summary>
    protected abstract void OnRotate(double angle, Point center);

    /// <summary>
    /// 状态改变时的回调
    /// </summary>
    protected virtual void OnStateChanged() { }

    /// <summary>
    /// 样式改变时的回调
    /// </summary>
    protected virtual void OnStyleChanged() { }

    /// <summary>
    /// 检查点是否在矩形内
    /// </summary>
    protected static bool IsPointInRect(Point point, Rect rect)
    {
        return point.X >= rect.Left && point.X <= rect.Right &&
               point.Y >= rect.Top && point.Y <= rect.Bottom;
    }

    /// <summary>
    /// 计算点到线段的距离
    /// </summary>
    protected static double DistanceToLineSegment(Point point, Point lineStart, Point lineEnd, double threshold = 5.0)
    {
        var line = lineEnd - lineStart;
        var lineLength = Math.Sqrt(line.X * line.X + line.Y * line.Y);

        if (lineLength == 0)
            return Vector.Distance(point, lineStart);

        var t = Math.Max(0, Math.Min(1, Vector.Dot(point - lineStart, line) / (lineLength * lineLength)));
        var projection = lineStart + t * line;
        return Vector.Distance(point, projection);
    }
}
