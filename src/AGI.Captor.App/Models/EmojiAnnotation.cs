using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace AGI.Captor.App.Models;

/// <summary>
/// Emoji标注项
/// </summary>
public class EmojiAnnotation : AnnotationItemBase
{
    private Point _position;
    private string _emoji = "😀";
    private double _scale = 1.0;

    public override AnnotationType Type => AnnotationType.Emoji;

    /// <summary>
    /// Emoji位置（中心点）
    /// </summary>
    public Point Position
    {
        get => _position;
        set
        {
            _position = value;
            ModifiedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// Emoji字符
    /// </summary>
    public string Emoji
    {
        get => _emoji;
        set
        {
            _emoji = value ?? "😀";
            ModifiedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// 缩放比例
    /// </summary>
    public new double Scale
    {
        get => _scale;
        set
        {
            _scale = Math.Max(0.1, Math.Min(5.0, value)); // Limit scale between 0.1x and 5x
            ModifiedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// 基础大小（像素）
    /// </summary>
    public double BaseSize => Style.FontSize;

    /// <summary>
    /// 实际显示大小
    /// </summary>
    public double ActualSize => BaseSize * _scale;

    public override Rect Bounds
    {
        get
        {
            var size = ActualSize;
            var halfSize = size / 2;
            return new Rect(_position.X - halfSize, _position.Y - halfSize, size, size);
        }
    }

    public EmojiAnnotation(Point position, string emoji = "😀", IAnnotationStyle? style = null)
        : base(style ?? AnnotationStyle.CreateTextStyle(Color.FromRgb(255, 255, 255), 32))
    {
        _position = position;
        _emoji = emoji ?? "😀";
    }

    public override bool HitTest(Point point)
    {
        if (!IsVisible) return false;
        
        // Use circular hit test for emoji
        var center = _position;
        var radius = ActualSize / 2;
        var distance = Math.Sqrt(Math.Pow(point.X - center.X, 2) + Math.Pow(point.Y - center.Y, 2));
        
        return distance <= radius;
    }

    protected override void OnMove(Vector offset)
    {
        _position += offset;
    }

    protected override void OnScale(double scale, Point center)
    {
        // Scale position relative to center
        var relative = _position - center;
        _position = center + relative * scale;
        
        // Scale the emoji size
        _scale *= scale;
        _scale = Math.Max(0.1, Math.Min(5.0, _scale)); // Keep within bounds
    }

    protected override void OnRotate(double angle, Point center)
    {
        // Rotate position around center
        var relative = _position - center;
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        
        _position = center + new Vector(
            relative.X * cos - relative.Y * sin,
            relative.X * sin + relative.Y * cos);
    }

    public override IAnnotationItem Clone()
    {
        return new EmojiAnnotation(_position, _emoji, Style.Clone())
        {
            Scale = _scale,
            ZIndex = ZIndex,
            IsVisible = IsVisible,
            IsLocked = IsLocked
        };
    }

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["PositionX"] = _position.X;
        data["PositionY"] = _position.Y;
        data["Emoji"] = _emoji;
        data["Scale"] = _scale;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        
        if (data.TryGetValue("PositionX", out var posX) && data.TryGetValue("PositionY", out var posY))
            _position = new Point(Convert.ToDouble(posX), Convert.ToDouble(posY));
            
        if (data.TryGetValue("Emoji", out var emoji))
            _emoji = emoji.ToString() ?? "😀";
            
        if (data.TryGetValue("Scale", out var scale))
            _scale = Math.Max(0.1, Math.Min(5.0, Convert.ToDouble(scale)));
    }

    /// <summary>
    /// 常用Emoji列表
    /// </summary>
    public static readonly string[] CommonEmojis = new[]
    {
        "😀", "😃", "😄", "😁", "😆", "😅", "🤣", "😂", "🙂", "🙃",
        "😉", "😊", "😇", "🥰", "😍", "🤩", "😘", "😗", "😚", "😙",
        "😋", "😛", "😜", "🤪", "😝", "🤑", "🤗", "🤭", "🤫", "🤔",
        "🤐", "🤨", "😐", "😑", "😶", "😏", "😒", "🙄", "😬", "🤥",
        "😔", "😪", "🤤", "😴", "😷", "🤒", "🤕", "🤢", "🤮", "🤧",
        "🥵", "🥶", "🥴", "😵", "🤯", "🤠", "🥳", "😎", "🤓", "🧐",
        "👍", "👎", "👌", "✌️", "🤞", "🤟", "🤘", "🤙", "👈", "👉",
        "👆", "🖕", "👇", "☝️", "👋", "🤚", "🖐️", "✋", "🖖", "👏",
        "🙌", "🤝", "🙏", "✍️", "💪", "🦵", "🦶", "👂", "🦻", "👃",
        "❤️", "🧡", "💛", "💚", "💙", "💜", "🖤", "🤍", "🤎", "💔",
        "❣️", "💕", "💞", "💓", "💗", "💖", "💘", "💝", "💟", "☮️"
    };
}
