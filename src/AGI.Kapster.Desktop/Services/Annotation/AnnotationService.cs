using AGI.Kapster.Desktop.Models;
using Serilog;
using AGI.Kapster.Desktop.Commands;
using AGI.Kapster.Desktop.Rendering;
using AGI.Kapster.Desktop.Services.Settings;
using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace AGI.Kapster.Desktop.Services.Annotation;

/// <summary>
/// Annotation service implementation
/// </summary>
public class AnnotationService : IAnnotationService
{
    private AnnotationToolType _currentTool = AnnotationToolType.None;
    private IAnnotationStyle _currentStyle;
    private readonly ISettingsService? _settingsService;
    private readonly Dictionary<Guid, Point> _creationAnchors = new();

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


    public IAnnotationItem? StartAnnotation(Point startPoint)
    {
        IAnnotationItem? item = null;
        if (CurrentTool == AnnotationToolType.Rectangle)
        {
            item = CreateRectangle(startPoint);
        }
        else if (CurrentTool == AnnotationToolType.Ellipse)
        {
            item = CreateEllipse(startPoint);
        }
        else if (CurrentTool == AnnotationToolType.Arrow)
        {
            item = CreateArrow(startPoint);
        }
        else if (CurrentTool == AnnotationToolType.Text)
        {
            item = CreateText(startPoint);
        }
        else if (CurrentTool == AnnotationToolType.Freehand)
        {
            item = CreateFreehand();
        }
        else if (CurrentTool == AnnotationToolType.Emoji)
        {
            item = CreateEmoji(startPoint);
        }
        else if (CurrentTool == AnnotationToolType.Mosaic)
        {
            item = CreateMosaic(startPoint);
        }

        if (item != null)
        {
            // Record immutable creation anchor for shapes
            if (item is RectangleAnnotation || item is EllipseAnnotation || item is ArrowAnnotation)
            {
                _creationAnchors[item.Id] = startPoint;
            }
        }
        return item;
    }

    public void UpdateAnnotation(Point currentPoint, IAnnotationItem item)
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
            case MosaicAnnotation mosaic:
                UpdateMosaic(mosaic, currentPoint);
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
        else if (item is MosaicAnnotation mosaic)
        {
            // Validate minimum number of points for mosaic trail
            if (mosaic.Points.Count < 1)
                return false;
            
            // Clear rendered cells set for next edit
            mosaic.RenderedCells.Clear();
        }

        // Add to manager
        item.State = AnnotationState.Normal;
        Manager.AddItem(item);
        _creationAnchors.Remove(item.Id);
        return true;
    }

    public void CancelCreate(IAnnotationItem item)
    {
        // Nothing special needed for cancellation
        // The item will simply not be added to the manager
        _creationAnchors.Remove(item.Id);
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
    /// Create rectangle annotation
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
    /// Update rectangle annotation
    /// </summary>
    private void UpdateRectangle(RectangleAnnotation rect, Point currentPoint)
    {
        // Use immutable creation anchor to support all drag directions
        var startPoint = _creationAnchors.TryGetValue(rect.Id, out var sp) ? sp : rect.TopLeft;

        // Create new rectangle from start point to current point
        var left = Math.Min(startPoint.X, currentPoint.X);
        var top = Math.Min(startPoint.Y, currentPoint.Y);
        var right = Math.Max(startPoint.X, currentPoint.X);
        var bottom = Math.Max(startPoint.Y, currentPoint.Y);

        rect.Rectangle = new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Create ellipse annotation
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
    /// Update ellipse annotation
    /// </summary>
    private void UpdateEllipse(EllipseAnnotation ellipse, Point currentPoint)
    {
        // Use immutable creation anchor to support all drag directions
        var startPoint = _creationAnchors.TryGetValue(ellipse.Id, out var sp) ? sp : ellipse.BoundingRect.TopLeft;

        var left = Math.Min(startPoint.X, currentPoint.X);
        var top = Math.Min(startPoint.Y, currentPoint.Y);
        var right = Math.Max(startPoint.X, currentPoint.X);
        var bottom = Math.Max(startPoint.Y, currentPoint.Y);

        ellipse.BoundingRect = new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Create arrow annotation
    /// </summary>
    private ArrowAnnotation CreateArrow(Point startPoint)
    {
        var style = AnnotationFactory.CreateStyleVariant(
            AnnotationFactory.GetDefaultStyle(AnnotationType.Arrow),
            GetCurrentColor(),
            CurrentStyle.StrokeWidth
        );

        var arrow = AnnotationFactory.CreateArrow(startPoint, startPoint, style);
        arrow.Trail.Add(startPoint);
        return arrow;
    }

    /// <summary>
    /// Update arrow annotation
    /// </summary>
    private void UpdateArrow(ArrowAnnotation arrow, Point currentPoint)
    {
        arrow.EndPoint = currentPoint;
        arrow.Trail.Add(currentPoint);
    }

    /// <summary>
    /// Create text annotation
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
    /// Create freehand annotation
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
    /// Update freehand annotation
    /// </summary>
    private void UpdateFreehand(FreehandAnnotation freehand, Point currentPoint)
    {
        freehand.AddPoint(currentPoint);
    }

    /// <summary>
    /// Create Emoji annotation
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
        if (settings == null)
        {
            return new AnnotationStyle(); // Default style
        }

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

    /// <summary>
    /// Create brush-style mosaic annotation
    /// </summary>
    private MosaicAnnotation CreateMosaic(Point startPoint)
    {
        var style = AnnotationFactory.CreateStyleVariant(
            AnnotationFactory.GetDefaultStyle(AnnotationType.Mosaic),
            GetCurrentColor(),
            CurrentStyle.StrokeWidth // Use current size setting for brush size control
        );

        var mosaic = AnnotationFactory.CreateMosaic(style);
        mosaic.AddPoint(startPoint); // Add initial point
        return mosaic;
    }

    /// <summary>
    /// Update mosaic annotation by adding points along the trail
    /// </summary>
    private void UpdateMosaic(MosaicAnnotation mosaic, Point currentPoint)
    {
        // Get the last point to interpolate from
        if (mosaic.Points.Count > 0)
        {
            var lastPoint = mosaic.Points[mosaic.Points.Count - 1];
            var dx = currentPoint.X - lastPoint.X;
            var dy = currentPoint.Y - lastPoint.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            
            // Dynamic step size based on brush size to ensure continuous coverage
            // Use 25% of brush size for dense sampling with good overlap
            var stepSize = Math.Max(1.0, mosaic.BrushSize * 0.25);
            
            if (distance > stepSize)
            {
                // Add interpolated points for continuous stroke
                var steps = (int)Math.Ceiling(distance / stepSize);
                for (int i = 1; i <= steps; i++)
                {
                    var t = (double)i / steps;
                    var interpolatedPoint = new Point(
                        lastPoint.X + dx * t,
                        lastPoint.Y + dy * t
                    );
                    mosaic.AddPoint(interpolatedPoint);
                }
            }
            else if (distance > 0.5) // Minimum distance threshold to avoid excessive duplicate points
            {
                mosaic.AddPoint(currentPoint);
            }
            // If distance <= 0.5, skip to avoid creating too many overlapping points
        }
        else
        {
            mosaic.AddPoint(currentPoint);
        }
    }

}
