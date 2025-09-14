using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using AGI.Captor.Desktop.Models;
using AGI.Captor.Desktop.Rendering;
using AGI.Captor.Desktop.Services;
using Serilog;
using SkiaSharp;

namespace AGI.Captor.Desktop.Services;

/// <summary>
/// Export service implementation for saving annotated screenshots with multiple format support
/// </summary>
public class ExportService : IExportService
{
    private readonly IAnnotationRenderer _renderer;

    public ExportService()
    {
        _renderer = new AnnotationRenderer();
    }

    /// <summary>
    /// Export annotated screenshot to file with default settings
    /// </summary>
    public async Task ExportToFileAsync(Bitmap screenshot, IEnumerable<IAnnotationItem> annotations, Rect selectionRect, string filePath)
    {
        var defaultSettings = GetDefaultSettings();
        await ExportToFileAsync(screenshot, annotations, selectionRect, filePath, defaultSettings);
    }

    /// <summary>
    /// Export annotated screenshot to file with custom settings
    /// </summary>
    public async Task ExportToFileAsync(Bitmap screenshot, IEnumerable<IAnnotationItem> annotations, Rect selectionRect, string filePath, ExportSettings settings, Action<int, string>? progressCallback = null)
    {
        try
        {
            progressCallback?.Invoke(10, "Creating composite image...");
            // UI operations must run on UI thread
            var compositeImage = await CreateCompositeImageAsync(screenshot, annotations, selectionRect);
            
            progressCallback?.Invoke(40, "Converting image format...");
            // Convert Avalonia bitmap to SkiaSharp for advanced format support
            using var skiaBitmap = await ConvertToSkiaBitmapAsync(compositeImage, settings);
            
            progressCallback?.Invoke(70, "Encoding image...");
            await SaveSkiaBitmapAsync(skiaBitmap, filePath, settings, progressCallback);
            
            progressCallback?.Invoke(100, "Export completed");
            Log.Information("Exported annotated screenshot to {FilePath} with format {Format}", filePath, settings.Format);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export annotated screenshot to {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Export annotated screenshot to clipboard (simplified)
    /// </summary>
    public async Task ExportToClipboardAsync(Bitmap screenshot, IEnumerable<IAnnotationItem> annotations, Rect selectionRect)
    {
        try
        {
            // For now, save to a temp file and notify - clipboard image support can be improved later
            var defaultSettings = GetDefaultSettings();
            var tempPath = Path.Combine(Path.GetTempPath(), $"AGI_Captor_Export_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            await ExportToFileAsync(screenshot, annotations, selectionRect, tempPath, defaultSettings);
            
            Log.Information("Exported annotated screenshot to temporary file: {TempPath}", tempPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export annotated screenshot to clipboard");
            throw;
        }
    }

    /// <summary>
    /// Get supported export formats
    /// </summary>
    public ExportFormat[] GetSupportedFormats()
    {
        return new[]
        {
            ExportFormat.PNG,
            ExportFormat.JPEG,
            ExportFormat.BMP,
            ExportFormat.TIFF,
            ExportFormat.WebP,
            ExportFormat.GIF
        };
    }

    /// <summary>
    /// Get default export settings from application settings
    /// </summary>
    public ExportSettings GetDefaultSettings()
    {
        try
        {
            // Create fresh settings service instance to get latest settings
            var settingsService = new SettingsService();
            var appSettings = settingsService.Settings;
            
            // Determine format from settings
            var formatString = appSettings?.General?.DefaultSaveFormat ?? "PNG";
            var format = Enum.TryParse<ExportFormat>(formatString, true, out var parsedFormat) 
                ? parsedFormat 
                : ExportFormat.PNG;
            
            // Get quality settings from advanced settings if available
            var jpegQuality = appSettings?.DefaultStyles?.Export?.JpegQuality ?? 90;
            var pngCompression = appSettings?.DefaultStyles?.Export?.PngCompression ?? 6;
            
            return new ExportSettings
            {
                Format = format,
                Quality = (int)jpegQuality,
                Compression = (int)pngCompression,
                DPI = 96,
                PreserveTransparency = true
            };
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to get default settings from application settings, using fallback defaults");
            return new ExportSettings
            {
                Format = ExportFormat.PNG,
                Quality = 90,
                Compression = 6,
                DPI = 96,
                PreserveTransparency = true
            };
        }
    }

    /// <summary>
    /// Create composite image with annotations rendered on top of screenshot
    /// Must be called on UI thread due to Canvas and RenderTargetBitmap operations
    /// </summary>
    private Task<Bitmap> CreateCompositeImageAsync(Bitmap screenshot, IEnumerable<IAnnotationItem> annotations, Rect selectionRect)
    {
        try
        {
            // Create a canvas to render annotations
            var canvas = new Canvas
            {
                Width = selectionRect.Width,
                Height = selectionRect.Height,
                Background = Brushes.Transparent
            };

            // Adjust annotation coordinates to be relative to selection area
            var annotationList = annotations.ToList();
            if (annotationList.Any())
            {
                // Create offset annotations for proper positioning
                var offsetAnnotations = new List<IAnnotationItem>();
                foreach (var annotation in annotationList)
                {
                    var offsetAnnotation = CreateOffsetAnnotation(annotation, -selectionRect.X, -selectionRect.Y);
                    offsetAnnotations.Add(offsetAnnotation);
                }
                
                _renderer.RenderAll(canvas, offsetAnnotations);
            }

            // Create render target bitmap for annotations
            var annotationBitmap = new RenderTargetBitmap(
                new PixelSize((int)selectionRect.Width, (int)selectionRect.Height),
                new Vector(96, 96));

            annotationBitmap.Render(canvas);

            // Create final composite bitmap
            var composite = new RenderTargetBitmap(
                new PixelSize((int)selectionRect.Width, (int)selectionRect.Height),
                new Vector(96, 96));

            using var context = composite.CreateDrawingContext();
            
            // Draw screenshot portion
            // Since the screenshot is already cropped to selection area, use full screenshot as source
            var sourceRect = new Rect(0, 0, screenshot.PixelSize.Width, screenshot.PixelSize.Height);
            var destRect = new Rect(0, 0, selectionRect.Width, selectionRect.Height);
            context.DrawImage(screenshot, sourceRect, destRect);
            
            // Draw annotations on top
            context.DrawImage(annotationBitmap, destRect);
            
            return Task.FromResult<Bitmap>(composite);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create composite image");
            throw;
        }
    }
    
    /// <summary>
    /// Create an offset copy of an annotation for export rendering
    /// </summary>
    private IAnnotationItem CreateOffsetAnnotation(IAnnotationItem original, double offsetX, double offsetY)
    {
        IAnnotationItem offsetItem = original switch
        {
            TextAnnotation text => new TextAnnotation(
                new Point(text.Position.X + offsetX, text.Position.Y + offsetY),
                text.Text,
                text.Style),
            ArrowAnnotation arrow => new ArrowAnnotation(
                new Point(arrow.StartPoint.X + offsetX, arrow.StartPoint.Y + offsetY),
                new Point(arrow.EndPoint.X + offsetX, arrow.EndPoint.Y + offsetY),
                arrow.Style),
            RectangleAnnotation rect => new RectangleAnnotation(
                new Rect(rect.Bounds.X + offsetX, rect.Bounds.Y + offsetY, 
                         rect.Bounds.Width, rect.Bounds.Height),
                rect.Style),
            EllipseAnnotation ellipse => new EllipseAnnotation(
                new Rect(ellipse.Bounds.X + offsetX, ellipse.Bounds.Y + offsetY,
                         ellipse.Bounds.Width, ellipse.Bounds.Height),
                ellipse.Style),
            FreehandAnnotation freehand => CreateOffsetFreehandAnnotation(freehand, offsetX, offsetY),
            EmojiAnnotation emoji => new EmojiAnnotation(
                new Point(emoji.Position.X + offsetX, emoji.Position.Y + offsetY),
                emoji.Emoji,
                emoji.Style),
            _ => original // Fallback for unknown types
        };
        
        // Ensure the offset item is not selected for clean export
        offsetItem.State = AnnotationState.Normal;
        return offsetItem;
    }
    
    /// <summary>
    /// Create offset freehand annotation by manually copying points
    /// </summary>
    private FreehandAnnotation CreateOffsetFreehandAnnotation(FreehandAnnotation original, double offsetX, double offsetY)
    {
        var offsetFreehand = new FreehandAnnotation(original.Style);
        
        // Add offset points one by one
        foreach (var point in original.Points)
        {
            offsetFreehand.AddPoint(new Point(point.X + offsetX, point.Y + offsetY));
        }
        
        return offsetFreehand;
    }

    /// <summary>
    /// Convert Avalonia bitmap to SkiaSharp bitmap with optimal quality preservation
    /// </summary>
    private async Task<SKBitmap> ConvertToSkiaBitmapAsync(Bitmap avaloniabitmap, ExportSettings settings)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Save Avalonia bitmap to memory stream as PNG to preserve quality
                using var stream = new MemoryStream();
                avaloniabitmap.Save(stream);
                stream.Position = 0;

                // Load as SkiaSharp image
                using var skiaImage = SKImage.FromEncodedData(stream);
                if (skiaImage == null)
                {
                    throw new InvalidOperationException("Failed to create SkiaSharp image from Avalonia bitmap");
                }
                
                var skiaBitmap = SKBitmap.FromImage(skiaImage);
                if (skiaBitmap == null)
                {
                    throw new InvalidOperationException("Failed to create SkiaSharp bitmap from image");
                }

                // Apply background color if transparency is not preserved
                if (!settings.SupportsTransparency() || !settings.PreserveTransparency)
                {
                    var width = skiaBitmap.Width;
                    var height = skiaBitmap.Height;
                    var backgroundBitmap = new SKBitmap(width, height, SKColorType.Rgb888x, SKAlphaType.Opaque);
                    
                    using var canvas = new SKCanvas(backgroundBitmap);
                    using var paint = new SKPaint
                    {
                        IsAntialias = true,
                        FilterQuality = SKFilterQuality.High
                    };
                    
                    // Use white background for formats that don't support transparency
                    canvas.Clear(SKColors.White);
                    canvas.DrawBitmap(skiaBitmap, 0, 0, paint);
                    
                    skiaBitmap.Dispose(); // Clean up original bitmap
                    return backgroundBitmap;
                }

                return skiaBitmap;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to convert Avalonia bitmap to SkiaSharp bitmap");
                throw;
            }
        });
    }

    /// <summary>
    /// Save SkiaSharp bitmap with specific format and quality settings
    /// </summary>
    private async Task SaveSkiaBitmapAsync(SKBitmap bitmap, string filePath, ExportSettings settings, Action<int, string>? progressCallback = null)
    {
        await Task.Run(() =>
        {
            try
            {
                // UI thread safe progress callback
                progressCallback?.Invoke(75, "Creating image...");
                using var image = SKImage.FromBitmap(bitmap);
                using var stream = File.Create(filePath);

                progressCallback?.Invoke(85, $"Encoding as {settings.Format}...");
                
                // Log quality settings for debugging
                if (settings.SupportsQuality())
                {
                    Log.Debug("Exporting {Format} with quality: {Quality}%", settings.Format, settings.Quality);
                }
                
                // Create encode options based on format
                SKData encodedData;
                
                try
                {
                    switch (settings.Format)
                    {
                        case ExportFormat.PNG:
                            encodedData = image.Encode(SKEncodedImageFormat.Png, 100);
                            break;
                            
                        case ExportFormat.JPEG:
                            // Use high-quality JPEG encoding with explicit quality setting
                            // Ensure quality is properly applied (SkiaSharp sometimes ignores low-level settings)
                            var actualQuality = Math.Max(settings.Quality, 85); // Minimum quality for screenshots
                            Log.Debug("Encoding JPEG with quality: {Quality}", actualQuality);
                            
                            // Use proper SkiaSharp JPEG encoding
                            encodedData = image.Encode(SKEncodedImageFormat.Jpeg, actualQuality);
                            if (encodedData == null)
                            {
                                Log.Warning("JPEG encoding failed, falling back to PNG");
                                encodedData = image.Encode(SKEncodedImageFormat.Png, 100);
                            }
                            break;
                            
                        case ExportFormat.WebP:
                            encodedData = image.Encode(SKEncodedImageFormat.Webp, settings.Quality);
                            break;
                            
                        case ExportFormat.BMP:
                            // Try SkiaSharp BMP encoding first, fallback to PNG if it fails
                            try
                            {
                                Log.Debug("Attempting BMP encoding with SkiaSharp");
                                encodedData = image.Encode(SKEncodedImageFormat.Bmp, 100);
                                
                                if (encodedData == null || encodedData.Size == 0)
                                {
                                    Log.Warning("SkiaSharp BMP encoding failed, using PNG fallback");
                                    encodedData = image.Encode(SKEncodedImageFormat.Png, 100);
                                }
                                else
                                {
                                    Log.Debug("BMP encoding successful, size: {Size} bytes", encodedData.Size);
                                }
                            }
                            catch (Exception bmpEx)
                            {
                                Log.Warning(bmpEx, "BMP encoding failed, using PNG fallback");
                                encodedData = image.Encode(SKEncodedImageFormat.Png, 100);
                            }
                            break;
                            
                        case ExportFormat.TIFF:
                            // Fallback to PNG for TIFF since SkiaSharp has limited TIFF support
                            Log.Warning("TIFF format not fully supported, saving as PNG");
                            encodedData = image.Encode(SKEncodedImageFormat.Png, 100);
                            break;
                            
                        case ExportFormat.GIF:
                            // Fallback to PNG for GIF since SkiaSharp has limited GIF encoding support
                            Log.Warning("GIF format encoding not fully supported, saving as PNG");
                            encodedData = image.Encode(SKEncodedImageFormat.Png, 100);
                            break;
                            
                        default:
                            encodedData = image.Encode(SKEncodedImageFormat.Png, 100);
                            break;
                    }
                }
                catch (Exception encodeEx)
                {
                    Log.Warning(encodeEx, "Failed to encode with format {Format}, falling back to PNG", settings.Format);
                    encodedData = image.Encode(SKEncodedImageFormat.Png, 100);
                }

                progressCallback?.Invoke(95, "Saving file...");
                if (encodedData != null)
                {
                    encodedData.SaveTo(stream);
                    Log.Debug("Successfully encoded image with format {Format}", settings.Format);
                }
                else
                {
                    throw new InvalidOperationException($"Failed to encode image with format {settings.Format}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save SkiaSharp bitmap to {FilePath}", filePath);
                throw;
            }
        });
    }
}