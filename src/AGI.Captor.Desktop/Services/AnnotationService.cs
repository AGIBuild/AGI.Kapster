using AGI.Captor.Desktop.Models;
using Serilog;
using AGI.Captor.Desktop.Commands;
using AGI.Captor.Desktop.Rendering;
using Avalonia;
using Avalonia.Media;
using System;

namespace AGI.Captor.Desktop.Services;

/// <summary>
/// 标注服务实现
/// </summary>
public class AnnotationService : IAnnotationService
{
    private AnnotationToolType _currentTool = AnnotationToolType.None;
    private IAnnotationStyle _currentStyle;
    private readonly ISettingsService? _settingsService;

    public AnnotationManager Manager { get; } = new();

    public AnnotationToolType CurrentTool
    {
        get => _currentTool;
        set
        {
            var oldTool = _currentTool;
            _currentTool = value;
            
            Log.Information("AnnotationService.CurrentTool: {OldTool} -> {NewTool} (Instance: {Hash})", oldTool, value, GetHashCode());
            
            if (oldTool != value)
            {
                ToolChanged?.Invoke(this, new ToolChangedEventArgs(oldTool, value));
                // Don't reset style when tool changes - preserve user's color/style choices
                // Style updates should be handled by UI components listening to StyleChanged events
            }
        }
    }

    public IAnnotationStyle CurrentStyle
    {
        get => _currentStyle;
        set
        {
            var oldStyle = _currentStyle;
            _currentStyle = value ?? throw new ArgumentNullException(nameof(value));
            
            if (oldStyle != value)
            {
                StyleChanged?.Invoke(this, new StyleChangedEventArgs(oldStyle, value));
            }
        }
    }

    public event EventHandler<ToolChangedEventArgs>? ToolChanged;
    public event EventHandler<StyleChangedEventArgs>? StyleChanged;

    public AnnotationService(ISettingsService? settingsService = null)
    {
        _settingsService = settingsService;
        _currentStyle = CreateStyleFromSettings();
        
        // Settings are loaded immediately in constructor, no need to subscribe to changes
    }


    /// <summary>
    /// Update current style from settings
    /// </summary>
    public void UpdateStyleFromSettings()
    {
        CurrentStyle = CreateStyleFromSettings();
    }


    public IAnnotationItem? StartCreate(Point startPoint)
    {
        return CurrentTool switch
        {
            AnnotationToolType.Rectangle => CreateRectangle(startPoint),
            AnnotationToolType.Ellipse => CreateEllipse(startPoint),
            AnnotationToolType.Arrow => CreateArrow(startPoint),
            AnnotationToolType.Text => CreateText(startPoint),
            AnnotationToolType.Freehand => CreateFreehand(),
            AnnotationToolType.Emoji => CreateEmoji(startPoint),
            _ => null
        };
    }

    public void UpdateCreate(Point currentPoint, IAnnotationItem item)
    {
        switch (item)
        {
            case RectangleAnnotation rect:
                UpdateRectangle(rect, currentPoint);
                break;
            case EllipseAnnotation ellipse:
                UpdateEllipse(ellipse, currentPoint);
                break;
            case ArrowAnnotation arrow:
                UpdateArrow(arrow, currentPoint);
                break;
            case FreehandAnnotation freehand:
                UpdateFreehand(freehand, currentPoint);
                break;
            // Text and Emoji annotations don't need update during creation
        }
    }

    public bool FinishCreate(IAnnotationItem item)
    {
        // Validate minimum size for shapes
        if (item is RectangleAnnotation rect)
        {
            if (rect.Width < 2 || rect.Height < 2)
                return false;
        }
        else if (item is EllipseAnnotation ellipse)
        {
            if (ellipse.RadiusX < 1 || ellipse.RadiusY < 1)
                return false;
        }
        else if (item is ArrowAnnotation arrow)
        {
            if (arrow.Length < 5)
                return false;
        }
        else if (item is FreehandAnnotation freehand)
        {
            // Finish the path and apply smoothing
            freehand.FinishPath();
            
            // Validate minimum number of points
            if (freehand.Points.Count < 2)
                return false;
        }

        // Add to manager
        item.State = AnnotationState.Normal;
        Manager.AddItem(item);
        return true;
    }

    public void CancelCreate(IAnnotationItem item)
    {
        // Nothing special needed for cancellation
        // The item will simply not be added to the manager
    }

    public IAnnotationItem? HitTest(Point point)
    {
        return Manager.HitTest(point);
    }

    public System.Collections.Generic.IEnumerable<IAnnotationItem> HitTest(Rect region)
    {
        return Manager.HitTest(region);
    }

    /// <summary>
    /// 创建矩形标注
    /// </summary>
    private RectangleAnnotation CreateRectangle(Point startPoint)
    {
        var style = AnnotationFactory.CreateStyleVariant(
            AnnotationFactory.GetDefaultStyle(AnnotationType.Rectangle),
            GetCurrentColor(),
            CurrentStyle.StrokeWidth
        );
        
        return AnnotationFactory.CreateRectangle(startPoint, startPoint, style);
    }

    /// <summary>
    /// 更新矩形标注
    /// </summary>
    private void UpdateRectangle(RectangleAnnotation rect, Point currentPoint)
    {
        // Get the original start point from the current rectangle
        var startPoint = rect.TopLeft;
        
        // Create new rectangle from start point to current point
        var left = Math.Min(startPoint.X, currentPoint.X);
        var top = Math.Min(startPoint.Y, currentPoint.Y);
        var right = Math.Max(startPoint.X, currentPoint.X);
        var bottom = Math.Max(startPoint.Y, currentPoint.Y);
        
        rect.Rectangle = new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// 创建椭圆标注
    /// </summary>
    private EllipseAnnotation CreateEllipse(Point startPoint)
    {
        var style = AnnotationFactory.CreateStyleVariant(
            AnnotationFactory.GetDefaultStyle(AnnotationType.Ellipse),
            GetCurrentColor(),
            CurrentStyle.StrokeWidth
        );
        
        return AnnotationFactory.CreateEllipse(new Rect(startPoint, startPoint), style);
    }

    /// <summary>
    /// 更新椭圆标注
    /// </summary>
    private void UpdateEllipse(EllipseAnnotation ellipse, Point currentPoint)
    {
        // Similar to rectangle, create bounding rect from start to current
        var startPoint = ellipse.BoundingRect.TopLeft;
        
        var left = Math.Min(startPoint.X, currentPoint.X);
        var top = Math.Min(startPoint.Y, currentPoint.Y);
        var right = Math.Max(startPoint.X, currentPoint.X);
        var bottom = Math.Max(startPoint.Y, currentPoint.Y);
        
        ellipse.BoundingRect = new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// 创建箭头标注
    /// </summary>
    private ArrowAnnotation CreateArrow(Point startPoint)
    {
        var style = AnnotationFactory.CreateStyleVariant(
            AnnotationFactory.GetDefaultStyle(AnnotationType.Arrow),
            GetCurrentColor(),
            CurrentStyle.StrokeWidth
        );
        
        return AnnotationFactory.CreateArrow(startPoint, startPoint, style);
    }

    /// <summary>
    /// 更新箭头标注
    /// </summary>
    private void UpdateArrow(ArrowAnnotation arrow, Point currentPoint)
    {
        arrow.EndPoint = currentPoint;
    }

    /// <summary>
    /// 创建文本标注
    /// </summary>
    private TextAnnotation CreateText(Point startPoint)
    {
        var style = AnnotationFactory.CreateStyleVariant(
            AnnotationFactory.GetDefaultStyle(AnnotationType.Text),
            GetCurrentColor(),
            fontSize: CurrentStyle.FontSize
        );
        
        var text = AnnotationFactory.CreateText(startPoint, "", style);
        text.State = AnnotationState.Editing; // Start in editing mode
        return text;
    }

    /// <summary>
    /// 创建自由画笔标注
    /// </summary>
    private FreehandAnnotation CreateFreehand()
    {
        var style = AnnotationFactory.CreateStyleVariant(
            AnnotationFactory.GetDefaultStyle(AnnotationType.Freehand),
            GetCurrentColor(),
            CurrentStyle.StrokeWidth
        );
        
        return AnnotationFactory.CreateFreehand(style);
    }

    /// <summary>
    /// 更新自由画笔标注
    /// </summary>
    private void UpdateFreehand(FreehandAnnotation freehand, Point currentPoint)
    {
        freehand.AddPoint(currentPoint);
    }

    /// <summary>
    /// 创建Emoji标注
    /// </summary>
    private EmojiAnnotation CreateEmoji(Point startPoint)
    {
        var style = AnnotationFactory.CreateStyleVariant(
            AnnotationFactory.GetDefaultStyle(AnnotationType.Emoji),
            GetCurrentColor(),
            fontSize: CurrentStyle.FontSize
        );
        
        return AnnotationFactory.CreateEmoji(startPoint, "😀", style);
    }

    /// <summary>
    /// Create style from current settings
    /// </summary>
    private IAnnotationStyle CreateStyleFromSettings()
    {
        if (_settingsService == null)
        {
            return new AnnotationStyle(); // Default style
        }

        var settings = _settingsService.Settings;
        var style = new AnnotationStyle();

        // Apply text settings
        if (settings.DefaultStyles?.Text != null)
        {
            style.FontSize = settings.DefaultStyles.Text.FontSize;
            style.FontFamily = settings.DefaultStyles.Text.FontFamily;
            
            try
            {
                style.StrokeColor = settings.DefaultStyles.Text.Color;
                style.FillColor = settings.DefaultStyles.Text.Color;
            }
            catch
            {
                style.StrokeColor = Colors.Black;
                style.FillColor = Colors.Black;
            }

            // Parse font weight
            if (Enum.TryParse<FontWeight>(settings.DefaultStyles.Text.FontWeight, out var fontWeight))
            {
                style.FontWeight = fontWeight;
            }

            // Parse font style
            if (Enum.TryParse<FontStyle>(settings.DefaultStyles.Text.FontStyle, out var fontStyle))
            {
                style.FontStyle = fontStyle;
            }
        }

        // Apply shape settings
        if (settings.DefaultStyles?.Shape != null)
        {
            style.StrokeWidth = settings.DefaultStyles.Shape.StrokeThickness;
            
            try
            {
                style.StrokeColor = settings.DefaultStyles.Shape.StrokeColor;
            }
            catch
            {
                style.StrokeColor = Colors.Red;
            }
        }

        return style;
    }

    /// <summary>
    /// 获取当前颜色
    /// </summary>
    private Color GetCurrentColor()
    {
        return CurrentStyle.StrokeColor;
    }
}
