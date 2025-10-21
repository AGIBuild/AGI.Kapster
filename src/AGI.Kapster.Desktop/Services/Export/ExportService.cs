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
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Rendering;
using AGI.Kapster.Desktop.Services;
using AGI.Kapster.Desktop.Services.Settings;
using Serilog;
using SkiaSharp;

namespace AGI.Kapster.Desktop.Services.Export;

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
    public async Task ExportToFileAsync(Bitmap screenshot, IEnumerable<IAnnotationItem> annotations, Rect selectionRect, string filePath, Screen? targetScreen = null)
    {
        var defaultSettings = GetDefaultSettings();
        await ExportToFileAsync(screenshot, annotations, selectionRect, filePath, defaultSettings, null, targetScreen);
    }

    /// <summary>
    /// Export annotated screenshot to file with custom settings
    /// </summary>
    public async Task ExportToFileAsync(Bitmap screenshot, IEnumerable<IAnnotationItem> annotations, Rect selectionRect, string filePath, ExportSettings settings, Action<int, string>? progressCallback = null, Screen? targetScreen = null)
    {
        try
        {
            progressCallback?.Invoke(10, "Creating composite image...");
            // UI operations must run on UI thread
            var compositeImage = await CreateCompositeImageAsync(screenshot, annotations, selectionRect, targetScreen);

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
    /// Export pre-rendered image directly to file (WYSIWYG approach - no re-rendering)
    /// </summary>
    public async Task ExportToFileDirectAsync(Bitmap finalImage, string filePath, ExportSettings settings, Action<int, string>? progressCallback = null)
    {
        try
        {
            progressCallback?.Invoke(10, "Preparing image...");
            Log.Information("Exporting pre-rendered image directly (WYSIWYG): {W}x{H}", 
                finalImage.PixelSize.Width, finalImage.PixelSize.Height);

            progressCallback?.Invoke(40, "Converting image format...");
            // Convert Avalonia bitmap to SkiaSharp for advanced format support
            using var skiaBitmap = await ConvertToSkiaBitmapAsync(finalImage, settings);

            progressCallback?.Invoke(70, "Encoding image...");
            await SaveSkiaBitmapAsync(skiaBitmap, filePath, settings, progressCallback);

            progressCallback?.Invoke(100, "Export completed");
            Log.Information("Exported image directly to {FilePath} with format {Format}", filePath, settings.Format);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export image to {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Export annotated screenshot to clipboard (simplified)
    /// </summary>
    public async Task ExportToClipboardAsync(Bitmap screenshot, IEnumerable<IAnnotationItem> annotations, Rect selectionRect, Screen? targetScreen = null)
    {
        try
        {
            // For now, save to a temp file and notify - clipboard image support can be improved later
            var defaultSettings = GetDefaultSettings();
            var tempPath = Path.Combine(Path.GetTempPath(), $"AGI_Kapster_Export_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            await ExportToFileAsync(screenshot, annotations, selectionRect, tempPath, defaultSettings, null, targetScreen);

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
            // Get singleton settings service from DI container
            var settingsService = App.Services?.GetService(typeof(ISettingsService)) as ISettingsService
                ?? throw new InvalidOperationException("ISettingsService not found in DI container. Ensure services are properly registered in CoreServiceExtensions.AddCoreServices()");

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
            Log.Error(ex, "Failed to get default settings from application settings");
            throw;
        }
    }

    /// <summary>
    /// Create composite image with annotations for macOS clipboard support
    /// </summary>
    public async Task<Bitmap?> CreateCompositeImageWithAnnotationsAsync(Bitmap screenshot, IEnumerable<IAnnotationItem> annotations, Rect selectionRect, Screen? targetScreen = null)
    {
        try
        {
            return await CreateCompositeImageAsync(screenshot, annotations, selectionRect, targetScreen);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create composite image with annotations");
            return null;
        }
    }

    /// <summary>
    /// Create composite image with annotations rendered on top of screenshot
    /// Must be called on UI thread due to Canvas and RenderTargetBitmap operations
    /// </summary>
    private Task<Bitmap> CreateCompositeImageAsync(Bitmap screenshot, IEnumerable<IAnnotationItem> annotations, Rect selectionRect, Screen? targetScreen = null)
    {
        try
        {
            // Use screenshot's actual pixel size for high quality (it's already at physical resolution)
            var pixelWidth = screenshot.PixelSize.Width;
            var pixelHeight = screenshot.PixelSize.Height;
            
            // Calculate scale factor between screenshot pixels and selection DIPs (for RenderTransform)
            var pixelScaleX = pixelWidth / selectionRect.Width;
            var pixelScaleY = pixelHeight / selectionRect.Height;
            
            var canvas = new Canvas
            {
                Width = selectionRect.Width,
                Height = selectionRect.Height,
                Background = Brushes.Transparent
            };

            var annotationList = annotations.ToList();
            if (annotationList.Any())
            {
                var offsetAnnotations = new List<IAnnotationItem>();
                var offsetX = -selectionRect.X;
                var offsetY = -selectionRect.Y;
                
                foreach (var annotation in annotationList)
                {
                    var offsetAnnotation = CreateOffsetAnnotation(annotation, offsetX, offsetY);
                    offsetAnnotations.Add(offsetAnnotation);
                }

                _renderer.RenderAll(canvas, offsetAnnotations);
            }

            // Force layout pass before rendering
            canvas.Measure(new Size(selectionRect.Width, selectionRect.Height));
            canvas.Arrange(new Rect(0, 0, selectionRect.Width, selectionRect.Height));

            // Apply scale transform for high-DPI rendering
            canvas.RenderTransform = new ScaleTransform(pixelScaleX, pixelScaleY);

            // Calculate effective DPI for rendering quality
            // Use target screen's actual DPI scaling if provided, otherwise derive from pixel scale
            double screenScaling;
            if (targetScreen != null)
            {
                // Use real screen DPI scaling for multi-monitor support
                screenScaling = targetScreen.Scaling;
                Log.Debug("Using target screen DPI scaling: {Scaling}", screenScaling);
            }
            else
            {
                // Fallback: derive from pixel/DIP ratio (handles single monitor or unknown screen)
                screenScaling = pixelScaleX; // Assume uniform scaling
                Log.Debug("No target screen specified, deriving DPI from pixel scale: {Scale}", screenScaling);
            }
            
            var effectiveDpiX = 96 * screenScaling;
            var effectiveDpiY = 96 * screenScaling;
            
            // Cap DPI to reasonable limit (300 DPI) to avoid excessive memory usage
            effectiveDpiX = Math.Min(300, effectiveDpiX);
            effectiveDpiY = Math.Min(300, effectiveDpiY);
            
            Log.Debug("Rendering annotations at {DpiX}x{DpiY} DPI for screen scaling {Scaling} (pixel scale: {PixelScaleX}x{PixelScaleY})",
                effectiveDpiX, effectiveDpiY, screenScaling, pixelScaleX, pixelScaleY);

            var annotationBitmap = new RenderTargetBitmap(
                new PixelSize(pixelWidth, pixelHeight),
                new Vector(effectiveDpiX, effectiveDpiY));

            // Clear with transparent background before rendering
            using (var clearCtx = annotationBitmap.CreateDrawingContext())
            {
                clearCtx.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, pixelWidth, pixelHeight));
            }

            annotationBitmap.Render(canvas);
            canvas.RenderTransform = null;

            // Create final composite with high DPI for maximum quality
            var composite = new RenderTargetBitmap(
                new PixelSize(pixelWidth, pixelHeight),
                new Vector(effectiveDpiX, effectiveDpiY));

            using var context = composite.CreateDrawingContext();

            // Draw screenshot with high-quality interpolation
            context.DrawImage(screenshot, 
                new Rect(0, 0, pixelWidth, pixelHeight), 
                new Rect(0, 0, pixelWidth, pixelHeight));

            // Draw annotations with alpha blending
            using (context.PushOpacity(1.0))
            {
                context.DrawImage(annotationBitmap, 
                    new Rect(0, 0, pixelWidth, pixelHeight));
            }
            
            Log.Debug("Composite image created at {Width}x{Height} with {DpiX}x{DpiY} DPI",
                pixelWidth, pixelHeight, effectiveDpiX, effectiveDpiY);
                
            return Task.FromResult<Bitmap>(composite);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create composite image");
            throw;
        }
    }

    /// <summary>
    /// Create a scaled and offset copy of an annotation for high-quality export rendering
    /// </summary>
    private IAnnotationItem CreateScaledOffsetAnnotation(IAnnotationItem original, double offsetX, double offsetY, double scaleX, double scaleY)
    {
        IAnnotationItem scaledItem = original switch
        {
            TextAnnotation text => new TextAnnotation(
                new Point(text.Position.X * scaleX + offsetX, text.Position.Y * scaleY + offsetY),
                text.Text,
                ScaleAnnotationStyle(text.Style, scaleX, scaleY)),
            ArrowAnnotation arrow => CreateScaledOffsetArrowAnnotation(arrow, offsetX, offsetY, scaleX, scaleY),
            RectangleAnnotation rect => new RectangleAnnotation(
                new Rect(rect.Rectangle.X * scaleX + offsetX, rect.Rectangle.Y * scaleY + offsetY,
                         rect.Rectangle.Width * scaleX, rect.Rectangle.Height * scaleY),
                ScaleAnnotationStyle(rect.Style, scaleX, scaleY)),
            EllipseAnnotation ellipse => new EllipseAnnotation(
                new Rect(ellipse.BoundingRect.X * scaleX + offsetX, ellipse.BoundingRect.Y * scaleY + offsetY,
                         ellipse.BoundingRect.Width * scaleX, ellipse.BoundingRect.Height * scaleY),
                ScaleAnnotationStyle(ellipse.Style, scaleX, scaleY)),
            MosaicAnnotation mosaic => CreateScaledOffsetMosaicAnnotation(mosaic, offsetX, offsetY, scaleX, scaleY),
            FreehandAnnotation freehand => CreateScaledOffsetFreehandAnnotation(freehand, offsetX, offsetY, scaleX, scaleY),
            EmojiAnnotation emoji => new EmojiAnnotation(
                new Point(emoji.Position.X * scaleX + offsetX, emoji.Position.Y * scaleY + offsetY),
                emoji.Emoji,
                ScaleAnnotationStyle(emoji.Style, scaleX, scaleY)),
            _ => original
        };

        scaledItem.State = AnnotationState.Normal;
        return scaledItem;
    }

    /// <summary>
    /// Scale annotation style for high-quality rendering
    /// </summary>
    private IAnnotationStyle ScaleAnnotationStyle(IAnnotationStyle original, double scaleX, double scaleY)
    {
        var avgScale = (scaleX + scaleY) / 2.0;
        return new AnnotationStyle
        {
            StrokeColor = original.StrokeColor,
            StrokeWidth = original.StrokeWidth * avgScale,
            LineStyle = original.LineStyle,
            FillColor = original.FillColor,
            FillMode = original.FillMode,
            Opacity = original.Opacity,
            FontFamily = original.FontFamily,
            FontSize = original.FontSize * avgScale,
            FontWeight = original.FontWeight,
            FontStyle = original.FontStyle
        };
    }

    /// <summary>
    /// Create scaled offset arrow annotation with trail points
    /// </summary>
    private ArrowAnnotation CreateScaledOffsetArrowAnnotation(ArrowAnnotation original, double offsetX, double offsetY, double scaleX, double scaleY)
    {
        var scaledArrow = new ArrowAnnotation(
            new Point(original.StartPoint.X * scaleX + offsetX, original.StartPoint.Y * scaleY + offsetY),
            new Point(original.EndPoint.X * scaleX + offsetX, original.EndPoint.Y * scaleY + offsetY),
            ScaleAnnotationStyle(original.Style, scaleX, scaleY));

        // Scale and offset trail points
        if (original.Trail != null && original.Trail.Count > 0)
        {
            scaledArrow.Trail = original.Trail
                .Select(p => new Point(p.X * scaleX + offsetX, p.Y * scaleY + offsetY))
                .ToList();
        }

        return scaledArrow;
    }

    /// <summary>
    /// Create scaled offset freehand annotation
    /// </summary>
    private FreehandAnnotation CreateScaledOffsetFreehandAnnotation(FreehandAnnotation original, double offsetX, double offsetY, double scaleX, double scaleY)
    {
        var scaledPoints = original.Points.Select(p => 
            new Point(p.X * scaleX + offsetX, p.Y * scaleY + offsetY)).ToList();
        
        var scaled = new FreehandAnnotation();
        foreach (var point in scaledPoints)
        {
            scaled.AddPoint(point);
        }
        scaled.Style = ScaleAnnotationStyle(original.Style, scaleX, scaleY);
        return scaled;
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
            ArrowAnnotation arrow => CreateOffsetArrowAnnotation(arrow, offsetX, offsetY),
            RectangleAnnotation rect => new RectangleAnnotation(
                new Rect(rect.Rectangle.X + offsetX, rect.Rectangle.Y + offsetY,
                         rect.Rectangle.Width, rect.Rectangle.Height),
                rect.Style),
            EllipseAnnotation ellipse => new EllipseAnnotation(
                new Rect(ellipse.BoundingRect.X + offsetX, ellipse.BoundingRect.Y + offsetY,
                         ellipse.BoundingRect.Width, ellipse.BoundingRect.Height),
                ellipse.Style),
            MosaicAnnotation mosaic => CreateOffsetMosaicAnnotation(mosaic, offsetX, offsetY),
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
    /// Create offset arrow annotation with trail points
    /// </summary>
    private ArrowAnnotation CreateOffsetArrowAnnotation(ArrowAnnotation original, double offsetX, double offsetY)
    {
        var offsetArrow = new ArrowAnnotation(
            new Point(original.StartPoint.X + offsetX, original.StartPoint.Y + offsetY),
            new Point(original.EndPoint.X + offsetX, original.EndPoint.Y + offsetY),
            original.Style);

        // Offset trail points
        if (original.Trail != null && original.Trail.Count > 0)
        {
            offsetArrow.Trail = original.Trail
                .Select(p => new Point(p.X + offsetX, p.Y + offsetY))
                .ToList();
        }

        return offsetArrow;
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
    /// Create scaled offset mosaic annotation
    /// </summary>
    private MosaicAnnotation CreateScaledOffsetMosaicAnnotation(MosaicAnnotation original, double offsetX, double offsetY, double scaleX, double scaleY)
    {
        var scaledPoints = original.Points.Select(p => 
            new Point(p.X * scaleX + offsetX, p.Y * scaleY + offsetY)).ToList();
        
        var scaled = new MosaicAnnotation(
            ScaleAnnotationStyle(original.Style, scaleX, scaleY), 
            original.BrushSize, 
            original.PixelSize);
        
        foreach (var point in scaledPoints)
        {
            scaled.AddPoint(point);
        }
        
        return scaled;
    }

    /// <summary>
    /// Create offset mosaic annotation by manually copying points
    /// </summary>
    private MosaicAnnotation CreateOffsetMosaicAnnotation(MosaicAnnotation original, double offsetX, double offsetY)
    {
        var offsetMosaic = new MosaicAnnotation(original.Style, original.BrushSize, original.PixelSize);

        // Add offset points one by one
        foreach (var point in original.Points)
        {
            offsetMosaic.AddPoint(new Point(point.X + offsetX, point.Y + offsetY));
        }

        return offsetMosaic;
    }

    /// <summary>
    /// Convert Avalonia bitmap to SkiaSharp bitmap with optimal quality preservation
    /// </summary>
    private Task<SKBitmap> ConvertToSkiaBitmapAsync(Bitmap avaloniabitmap, ExportSettings settings)
    {
        return Task.Run(() =>
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
                        IsAntialias = true
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
    private Task SaveSkiaBitmapAsync(SKBitmap bitmap, string filePath, ExportSettings settings, Action<int, string>? progressCallback = null)
    {
        return Task.Run(() =>
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

    /// <summary>
    /// Export annotated screenshot (convenience method for backward compatibility)
    /// </summary>
    public async Task ExportAsync(Bitmap screenshot, string filePath, ExportFormat format)
    {
        var settings = new ExportSettings
        {
            Format = format,
            Quality = 90
        };

        await ExportToFileAsync(screenshot, new List<IAnnotationItem>(), new Rect(0, 0, screenshot.PixelSize.Width, screenshot.PixelSize.Height), filePath, settings);
    }
}