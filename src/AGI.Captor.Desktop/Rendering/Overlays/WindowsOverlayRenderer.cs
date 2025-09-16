using System;
using Avalonia;
using SkiaSharp;

namespace AGI.Captor.Desktop.Services.Overlay.Platforms;

/// <summary>
/// Windows-specific overlay renderer implementation
/// </summary>
public class WindowsOverlayRenderer : IOverlayRenderer
{
    public OverlayTheme Theme { get; set; } = new OverlayTheme();
    
    public void RenderSelectionBox(SKCanvas canvas, Rect bounds, bool isActive = true)
    {
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = isActive ? Theme.SelectionBorderColor : Theme.SelectionBorderColor.WithAlpha(128),
            StrokeWidth = Theme.BorderWidth,
            IsAntialias = true
        };
        
        var rect = new SKRect(
            (float)bounds.X,
            (float)bounds.Y,
            (float)(bounds.X + bounds.Width),
            (float)(bounds.Y + bounds.Height));
        
        canvas.DrawRect(rect, paint);
        
        // Draw fill
        paint.Style = SKPaintStyle.Fill;
        paint.Color = Theme.SelectionFillColor;
        canvas.DrawRect(rect, paint);
        
        // Draw resize handles if active
        if (isActive)
        {
            RenderResizeHandles(canvas, bounds);
        }
    }
    
    public void RenderElementHighlight(SKCanvas canvas, IElementInfo element)
    {
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = Theme.ElementHighlightColor,
            StrokeWidth = Theme.BorderWidth * 1.5f,
            IsAntialias = true
        };
        
        var bounds = element.Bounds;
        var rect = new SKRect(bounds.X, bounds.Y, bounds.X + bounds.Width, bounds.Y + bounds.Height);
        canvas.DrawRect(rect, paint);
        
        // Draw element info label
        RenderElementLabel(canvas, element);
    }
    
    public void RenderCrosshair(SKCanvas canvas, Point position, double canvasWidth, double canvasHeight)
    {
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = Theme.CrosshairColor,
            StrokeWidth = Theme.CrosshairWidth,
            IsAntialias = true
        };
        
        // Horizontal line
        canvas.DrawLine(0, (float)position.Y, (float)canvasWidth, (float)position.Y, paint);
        
        // Vertical line
        canvas.DrawLine((float)position.X, 0, (float)position.X, (float)canvasHeight, paint);
    }
    
    public void RenderMagnifier(SKCanvas canvas, Point position, SKBitmap source, double zoomLevel = 2.0)
    {
        const int magnifierSize = 150;
        const int halfSize = magnifierSize / 2;
        
        var destX = (float)(position.X + 20);
        var destY = (float)(position.Y + 20);
        
        // Create magnifier circle clip
        using (var clipPath = new SKPath())
        {
            clipPath.AddCircle(destX + halfSize, destY + halfSize, halfSize);
            canvas.Save();
            canvas.ClipPath(clipPath);
            
            // Calculate source rectangle
            var sourceSize = (int)(magnifierSize / zoomLevel);
            var halfSourceSize = sourceSize / 2;
            var sourceX = Math.Max(0, Math.Min(source.Width - sourceSize, (int)position.X - halfSourceSize));
            var sourceY = Math.Max(0, Math.Min(source.Height - sourceSize, (int)position.Y - halfSourceSize));
            
            var sourceRect = new SKRect(sourceX, sourceY, sourceX + sourceSize, sourceY + sourceSize);
            var destRect = new SKRect(destX, destY, destX + magnifierSize, destY + magnifierSize);
            
            canvas.DrawBitmap(source, sourceRect, destRect);
            canvas.Restore();
        }
        
        // Draw magnifier border
        using var borderPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = Theme.CrosshairColor,
            StrokeWidth = 2,
            IsAntialias = true
        };
        canvas.DrawCircle(destX + halfSize, destY + halfSize, halfSize, borderPaint);
    }
    
    public void RenderDimensionLabels(SKCanvas canvas, Rect bounds)
    {
        var text = $"{bounds.Width:0} × {bounds.Height:0}";
        
        using var paint = new SKPaint
        {
            Color = Theme.TextColor,
            TextSize = Theme.TextSize,
            Typeface = Theme.TextTypeface,
            IsAntialias = true
        };
        
        var textBounds = new SKRect();
        paint.MeasureText(text, ref textBounds);
        
        var x = (float)(bounds.X + bounds.Width / 2 - textBounds.Width / 2);
        var y = (float)(bounds.Y - 10);
        
        // Draw background
        using var bgPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = Theme.TextBackgroundColor,
            IsAntialias = true
        };
        
        var padding = 4f;
        var bgRect = new SKRect(
            x - padding,
            y - textBounds.Height - padding,
            x + textBounds.Width + padding,
            y + padding);
        canvas.DrawRoundRect(bgRect, 3, 3, bgPaint);
        
        // Draw text
        canvas.DrawText(text, x, y, paint);
    }
    
    public void RenderPixelInfo(SKCanvas canvas, Point position, SKColor pixelColor)
    {
        var text = $"RGB({pixelColor.Red}, {pixelColor.Green}, {pixelColor.Blue})";
        var hexText = $"#{pixelColor.Red:X2}{pixelColor.Green:X2}{pixelColor.Blue:X2}";
        
        using var paint = new SKPaint
        {
            Color = Theme.TextColor,
            TextSize = Theme.TextSize,
            Typeface = Theme.TextTypeface,
            IsAntialias = true
        };
        
        var x = (float)(position.X + 10);
        var y = (float)(position.Y - 30);
        
        // Measure both texts
        var rgbBounds = new SKRect();
        var hexBounds = new SKRect();
        paint.MeasureText(text, ref rgbBounds);
        paint.MeasureText(hexText, ref hexBounds);
        
        var maxWidth = Math.Max(rgbBounds.Width, hexBounds.Width);
        var totalHeight = rgbBounds.Height + hexBounds.Height + 5;
        
        // Draw background
        using var bgPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = Theme.TextBackgroundColor,
            IsAntialias = true
        };
        
        var padding = 6f;
        var bgRect = new SKRect(
            x - padding,
            y - totalHeight - padding,
            x + maxWidth + padding + 20, // Extra space for color swatch
            y + padding);
        canvas.DrawRoundRect(bgRect, 4, 4, bgPaint);
        
        // Draw color swatch
        using var swatchPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = pixelColor,
            IsAntialias = true
        };
        
        var swatchRect = new SKRect(
            x + maxWidth + 8,
            y - totalHeight + 2,
            x + maxWidth + 18,
            y - 2);
        canvas.DrawRect(swatchRect, swatchPaint);
        
        // Draw texts
        canvas.DrawText(text, x, y - hexBounds.Height - 5, paint);
        canvas.DrawText(hexText, x, y, paint);
    }
    
    public void RenderCaptureMode(SKCanvas canvas, CaptureMode mode, Rect canvasBounds)
    {
        string modeText = mode switch
        {
            CaptureMode.FullScreen => "Full Screen",
            CaptureMode.Window => "Window",
            CaptureMode.Region => "Region",
            CaptureMode.Element => "Element",
            _ => mode.ToString()
        };
        
        using var paint = new SKPaint
        {
            Color = Theme.TextColor,
            TextSize = Theme.TextSize * 1.2f,
            Typeface = Theme.TextTypeface,
            IsAntialias = true
        };
        
        var textBounds = new SKRect();
        paint.MeasureText(modeText, ref textBounds);
        
        var x = (float)(canvasBounds.Width - textBounds.Width - 20);
        var y = 30f;
        
        // Draw background
        using var bgPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = Theme.TextBackgroundColor,
            IsAntialias = true
        };
        
        var padding = 8f;
        var bgRect = new SKRect(
            x - padding,
            y - textBounds.Height - padding,
            x + textBounds.Width + padding,
            y + padding);
        canvas.DrawRoundRect(bgRect, 4, 4, bgPaint);
        
        // Draw text
        canvas.DrawText(modeText, x, y, paint);
    }
    
    private void RenderResizeHandles(SKCanvas canvas, Rect bounds)
    {
        const float handleSize = 8;
        const float halfSize = handleSize / 2;
        
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = Theme.SelectionBorderColor,
            IsAntialias = true
        };
        
        // Corner handles
        canvas.DrawRect(new SKRect(
            (float)bounds.X - halfSize,
            (float)bounds.Y - halfSize,
            (float)bounds.X + halfSize,
            (float)bounds.Y + halfSize), paint);
        
        canvas.DrawRect(new SKRect(
            (float)(bounds.X + bounds.Width) - halfSize,
            (float)bounds.Y - halfSize,
            (float)(bounds.X + bounds.Width) + halfSize,
            (float)bounds.Y + halfSize), paint);
        
        canvas.DrawRect(new SKRect(
            (float)bounds.X - halfSize,
            (float)(bounds.Y + bounds.Height) - halfSize,
            (float)bounds.X + halfSize,
            (float)(bounds.Y + bounds.Height) + halfSize), paint);
        
        canvas.DrawRect(new SKRect(
            (float)(bounds.X + bounds.Width) - halfSize,
            (float)(bounds.Y + bounds.Height) - halfSize,
            (float)(bounds.X + bounds.Width) + halfSize,
            (float)(bounds.Y + bounds.Height) + halfSize), paint);
        
        // Edge handles
        canvas.DrawRect(new SKRect(
            (float)(bounds.X + bounds.Width / 2) - halfSize,
            (float)bounds.Y - halfSize,
            (float)(bounds.X + bounds.Width / 2) + halfSize,
            (float)bounds.Y + halfSize), paint);
        
        canvas.DrawRect(new SKRect(
            (float)(bounds.X + bounds.Width / 2) - halfSize,
            (float)(bounds.Y + bounds.Height) - halfSize,
            (float)(bounds.X + bounds.Width / 2) + halfSize,
            (float)(bounds.Y + bounds.Height) + halfSize), paint);
        
        canvas.DrawRect(new SKRect(
            (float)bounds.X - halfSize,
            (float)(bounds.Y + bounds.Height / 2) - halfSize,
            (float)bounds.X + halfSize,
            (float)(bounds.Y + bounds.Height / 2) + halfSize), paint);
        
        canvas.DrawRect(new SKRect(
            (float)(bounds.X + bounds.Width) - halfSize,
            (float)(bounds.Y + bounds.Height / 2) - halfSize,
            (float)(bounds.X + bounds.Width) + halfSize,
            (float)(bounds.Y + bounds.Height / 2) + halfSize), paint);
    }
    
    private void RenderElementLabel(SKCanvas canvas, IElementInfo element)
    {
        var label = $"{element.ElementType}";
        if (!string.IsNullOrEmpty(element.Name))
        {
            label += $": {element.Name}";
        }
        
        using var paint = new SKPaint
        {
            Color = Theme.TextColor,
            TextSize = Theme.TextSize,
            Typeface = Theme.TextTypeface,
            IsAntialias = true
        };
        
        var textBounds = new SKRect();
        paint.MeasureText(label, ref textBounds);
        
        var x = element.Bounds.X;
        var y = element.Bounds.Y - 5;
        
        // Draw background
        using var bgPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = Theme.ElementHighlightColor.WithAlpha(200),
            IsAntialias = true
        };
        
        var padding = 4f;
        var bgRect = new SKRect(
            x - padding,
            y - textBounds.Height - padding,
            x + textBounds.Width + padding,
            y + padding);
        canvas.DrawRoundRect(bgRect, 3, 3, bgPaint);
        
        // Draw text
        canvas.DrawText(label, x, y, paint);
    }
}


