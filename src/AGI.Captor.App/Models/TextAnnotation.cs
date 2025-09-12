using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using Serilog;

namespace AGI.Captor.App.Models;

/// <summary>
/// Text annotation item
/// </summary>
public class TextAnnotation : AnnotationItemBase
{
    private Point _position;
    private string _text = string.Empty;
    private Size _textSize;
    private Rect? _originalSelectionRect; // Remember the original selection area where text was created

    public override AnnotationType Type => AnnotationType.Text;

    /// <summary>
    /// Text position (top-left corner)
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
    /// Text content
    /// </summary>
    public string Text
    {
        get => _text;
        set
        {
            _text = value ?? string.Empty;
            ModifiedAt = DateTime.Now;
            UpdateTextSize();
        }
    }

    /// <summary>
    /// Text rendering size
    /// </summary>
    public Size TextSize
    {
        get => _textSize;
        private set => _textSize = value;
    }

    /// <summary>
    /// Whether text is being edited
    /// </summary>
    public bool IsEditing => State == AnnotationState.Editing;

    public override Rect Bounds
    {
        get
        {
            if (string.IsNullOrEmpty(_text))
                return new Rect(_position, new Size(20, Style.FontSize)); // Minimum size for empty text
            
            // For selected state, return editing bounds (text size + editing space)
            if (State == AnnotationState.Selected)
            {
                var editingWidth = Math.Max(_textSize.Width + 20, 100); // Give some extra space for editing
                var editingHeight = Math.Max(_textSize.Height + 8, Style.FontSize + 8);
                return new Rect(_position, new Size(editingWidth, editingHeight));
            }
            
            return new Rect(_position, _textSize);
        }
    }

    /// <summary>
    /// Get the actual text rendering bounds (not the editing bounds)
    /// Used for precise resize handle positioning
    /// </summary>
    public Rect GetTextRenderBounds()
    {
        if (string.IsNullOrEmpty(_text))
            return new Rect(_position, new Size(20, Style.FontSize));
        
        return new Rect(_position, _textSize);
    }

    /// <summary>
    /// Set the original selection rectangle where this text was created
    /// </summary>
    public void SetOriginalSelectionRect(Rect selectionRect)
    {
        _originalSelectionRect = selectionRect;
        ModifiedAt = DateTime.Now;
    }

    /// <summary>
    /// Get the original selection rectangle if available
    /// </summary>
    public Rect? OriginalSelectionRect => _originalSelectionRect;

    public TextAnnotation(Point position, string text = "", IAnnotationStyle? style = null)
        : base(style ?? AnnotationStyle.CreateTextStyle(Color.FromRgb(255, 0, 0), 16))
    {
        _position = position;
        _text = text ?? string.Empty;
        UpdateTextSize();
    }

    public override bool HitTest(Point point)
    {
        if (!IsVisible) return false;
        
        return IsPointInRect(point, Bounds);
    }

    protected override void OnMove(Vector offset)
    {
        _position += offset;
    }

    protected override void OnScale(double scale, Point center)
    {
        // Scale text position
        var newPosition = center + (_position - center) * scale;
        _position = newPosition;
        
        // Scale font size
        Style.FontSize *= scale;
        UpdateTextSize();
    }

    protected override void OnRotate(double angle, Point center)
    {
        // Rotate text position
        var relative = _position - center;
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        
        _position = center + new Vector(
            relative.X * cos - relative.Y * sin,
            relative.X * sin + relative.Y * cos
        );
    }

    protected override void OnStyleChanged()
    {
        base.OnStyleChanged();
        UpdateTextSize();
    }

    /// <summary>
    /// Start editing text
    /// </summary>
    public void StartEditing()
    {
        State = AnnotationState.Editing;
    }

    /// <summary>
    /// End editing text
    /// </summary>
    public void EndEditing()
    {
        State = AnnotationState.Normal;
    }

    /// <summary>
    /// Insert text at specified position
    /// </summary>
    public void InsertText(int index, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        
        index = Math.Max(0, Math.Min(index, _text.Length));
        _text = _text.Insert(index, text);
        ModifiedAt = DateTime.Now;
        UpdateTextSize();
    }

    /// <summary>
    /// Delete text in specified range
    /// </summary>
    public void DeleteText(int startIndex, int length)
    {
        if (length <= 0 || startIndex < 0 || startIndex >= _text.Length) return;
        
        length = Math.Min(length, _text.Length - startIndex);
        _text = _text.Remove(startIndex, length);
        ModifiedAt = DateTime.Now;
        UpdateTextSize();
    }

    /// <summary>
    /// Force update text rendering size (public method for external calls)
    /// </summary>
    public void UpdateTextSizeForced()
    {
        UpdateTextSize();
    }

    /// <summary>
    /// Set target display size for text by adjusting font size to match
    /// </summary>
    public void SetTargetSize(Size targetSize)
    {
        if (string.IsNullOrEmpty(_text)) return;
        
        // Reserve some margin space
        var targetWidth = Math.Max(targetSize.Width - 20, 30);
        var targetHeight = Math.Max(targetSize.Height - 8, 16);
        
        // Use binary search to find appropriate font size
        var minFontSize = 6.0;
        var maxFontSize = 200.0;
        var bestFontSize = Style.FontSize;
        
        for (int i = 0; i < 20; i++) // Maximum 20 iterations
        {
            var testFontSize = (minFontSize + maxFontSize) / 2;
            var testSize = MeasureTextSize(testFontSize);
            
            if (testSize.Width <= targetWidth && testSize.Height <= targetHeight)
            {
                bestFontSize = testFontSize;
                minFontSize = testFontSize;
            }
            else
            {
                maxFontSize = testFontSize;
            }
            
            if (maxFontSize - minFontSize < 0.5) break;
        }
        
        Style.FontSize = bestFontSize;
        UpdateTextSize();
        
        Log.Debug("Set target size {TargetSize} resulted in font size {FontSize}", 
                 targetSize, bestFontSize);
    }
    
    /// <summary>
    /// Measure text size at specified font size
    /// </summary>
    private Size MeasureTextSize(double fontSize)
    {
        if (string.IsNullOrEmpty(_text)) return new Size(0, fontSize);
        
        // Ensure font size is within safe range
        fontSize = Math.Max(6.0, Math.Min(1000.0, fontSize));
        
        try
        {
            var typeface = new Typeface(Style.FontFamily, Style.FontStyle, Style.FontWeight);
            var formattedText = new FormattedText(
                _text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.Black
            );
            
            return new Size(formattedText.Width, formattedText.Height);
        }
        catch (ArgumentOutOfRangeException)
        {
            // If font size still has issues, return safe estimated value
            return new Size(_text.Length * fontSize * 0.6, fontSize * 1.2);
        }
    }

    /// <summary>
    /// Update text rendering size
    /// </summary>
    private void UpdateTextSize()
    {
        if (string.IsNullOrEmpty(_text))
        {
            _textSize = new Size(0, Style.FontSize);
            return;
        }

        // Limit font size within reasonable range to prevent Avalonia exceptions
        const double minFontSize = 6.0;
        const double maxFontSize = 1000.0; // Much smaller than Avalonia's 35791 limit, ensuring safety
        var safeFontSize = Math.Max(minFontSize, Math.Min(maxFontSize, Style.FontSize));
        
        // If original font size exceeds range, update font size in Style
        if (Math.Abs(Style.FontSize - safeFontSize) > 0.1)
        {
            Style.FontSize = safeFontSize;
        }

        // Create temporary text measurement object
        var typeface = new Typeface(Style.FontFamily, Style.FontStyle, Style.FontWeight);
        var formattedText = new FormattedText(
            _text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            safeFontSize,
            Brushes.Black // Color does not affect size measurement
        );

        _textSize = new Size(formattedText.Width, formattedText.Height);
    }

    /// <summary>
    /// Synchronize actual rendering size - solve mismatch between selection area and rendering
    /// </summary>
    public void SyncActualSize(Size actualSize)
    {
        // Only update if there's a significant difference to avoid constant updates
        const double tolerance = 0.5;
        if (Math.Abs(_textSize.Width - actualSize.Width) > tolerance || 
            Math.Abs(_textSize.Height - actualSize.Height) > tolerance)
        {
            _textSize = actualSize;
            ModifiedAt = DateTime.Now;
        }
    }

    public override IAnnotationItem Clone()
    {
        return new TextAnnotation(_position, _text, Style.Clone())
        {
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
        data["Text"] = _text;
        data["TextSizeWidth"] = _textSize.Width;
        data["TextSizeHeight"] = _textSize.Height;
        
        // Serialize original selection rect if available
        if (_originalSelectionRect.HasValue)
        {
            var rect = _originalSelectionRect.Value;
            data["OriginalSelectionX"] = rect.X;
            data["OriginalSelectionY"] = rect.Y;
            data["OriginalSelectionWidth"] = rect.Width;
            data["OriginalSelectionHeight"] = rect.Height;
        }
        
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        
        if (data.TryGetValue("PositionX", out var posX) && data.TryGetValue("PositionY", out var posY))
            _position = new Point(Convert.ToDouble(posX), Convert.ToDouble(posY));
        if (data.TryGetValue("Text", out var text))
            _text = text.ToString() ?? string.Empty;
        
        // Deserialize original selection rect if available
        if (data.TryGetValue("OriginalSelectionX", out var selX) && 
            data.TryGetValue("OriginalSelectionY", out var selY) &&
            data.TryGetValue("OriginalSelectionWidth", out var selW) && 
            data.TryGetValue("OriginalSelectionHeight", out var selH))
        {
            _originalSelectionRect = new Rect(
                Convert.ToDouble(selX), 
                Convert.ToDouble(selY),
                Convert.ToDouble(selW), 
                Convert.ToDouble(selH));
        }
        
        UpdateTextSize(); // Recalculate text size
    }
}
