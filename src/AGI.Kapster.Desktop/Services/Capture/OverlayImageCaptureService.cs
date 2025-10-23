using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Services.Export;
using AGI.Kapster.Desktop.Services.Export.Imaging;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Capture;

/// <summary>
/// Service for capturing and compositing images from overlay windows
/// </summary>
public class OverlayImageCaptureService : IOverlayImageCaptureService
{
    private readonly IScreenCaptureStrategy _captureStrategy;
    private readonly IScreenCoordinateMapper _coordinateMapper;

    public OverlayImageCaptureService(
        IScreenCaptureStrategy captureStrategy,
        IScreenCoordinateMapper coordinateMapper)
    {
        _captureStrategy = captureStrategy ?? throw new ArgumentNullException(nameof(captureStrategy));
        _coordinateMapper = coordinateMapper ?? throw new ArgumentNullException(nameof(coordinateMapper));
    }

    public async Task<Bitmap?> CaptureWindowRegionWithAnnotationsAsync(
        Rect region,
        Bitmap? frozenBackground,
        IEnumerable<IAnnotationItem>? annotations,
        Size windowBounds)
    {
        try
        {
            var baseScreenshot = ExtractRegionFromBackground(region, frozenBackground!, windowBounds);
            if (baseScreenshot == null)
            {
                Log.Warning("Failed to extract region from frozen background");
                return null;
            }

            if (annotations == null || !annotations.Any())
            {
                return baseScreenshot;
            }

            // Composite annotations onto base screenshot
            var exportService = new ExportService();
            return await exportService.CreateCompositeImageWithAnnotationsAsync(
                baseScreenshot, annotations, region, targetScreen: null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to capture window region with annotations");
            return null;
        }
    }

    public Bitmap? ExtractRegionFromBackground(Rect region, Bitmap frozenBackground, Size windowBounds)
    {
        if (frozenBackground == null)
            return null;

        var totalDipWidth = Math.Max(1.0, windowBounds.Width);
        var totalDipHeight = Math.Max(1.0, windowBounds.Height);
        var scaleX = frozenBackground.PixelSize.Width / totalDipWidth;
        var scaleY = frozenBackground.PixelSize.Height / totalDipHeight;

        // Calculate source rectangle in physical pixels
        var sourceRect = new Rect(
            region.X * scaleX,
            region.Y * scaleY,
            Math.Max(1.0, region.Width * scaleX),
            Math.Max(1.0, region.Height * scaleY));

        // Target should be in physical pixels, not DIPs
        var targetWidth = Math.Max(1, (int)Math.Round(region.Width * scaleX));
        var targetHeight = Math.Max(1, (int)Math.Round(region.Height * scaleY));

        // Create bitmap at physical pixel resolution with standard 96 DPI
        var target = new RenderTargetBitmap(new PixelSize(targetWidth, targetHeight), new Vector(96, 96));
        using (var ctx = target.CreateDrawingContext())
        {
            ctx.DrawImage(frozenBackground, sourceRect, new Rect(0, 0, targetWidth, targetHeight));
        }
        return target;
    }

    public async Task<Bitmap?> GetBaseScreenshotForRegionAsync(
        Rect region,
        Bitmap? frozenBackground,
        Size windowBounds,
        Window window)
    {
        if (frozenBackground != null)
        {
            Log.Debug("Using frozen background for region {Region}", region);
            return ExtractRegionFromBackground(region, frozenBackground, windowBounds);
        }
        Log.Debug("Frozen background not available, using live capture for region {Region}", region);
        return await CaptureRegionAsync(region, window);
    }

    public async Task<Bitmap?> CaptureRegionAsync(Rect rect, Window window)
    {
        try
        {
            var skBitmap = await _captureStrategy.CaptureWindowRegionAsync(rect, window);
            return BitmapConverter.ConvertToAvaloniaBitmap(skBitmap);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to capture region");
            return null;
        }
    }

    public async Task<Bitmap?> GetFullScreenScreenshotAsync(Window window, Size bounds)
    {
        try
        {
            var screenBounds = new Rect(0, 0, bounds.Width, bounds.Height);
            var skBitmap = await _captureStrategy.CaptureWindowRegionAsync(screenBounds, window);
            return BitmapConverter.ConvertToAvaloniaBitmap(skBitmap);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to capture full screen screenshot for color sampling");
            return null;
        }
    }

    public Screen? GetScreenForSelection(Rect selectionRect, IReadOnlyList<Screen>? screens)
    {
        try
        {
            if (screens == null || screens.Count == 0)
            {
                Log.Warning("Cannot determine target screen: screens not available");
                return null;
            }

            // Calculate center point of selection (in logical DIPs)
            var centerX = selectionRect.X + selectionRect.Width / 2;
            var centerY = selectionRect.Y + selectionRect.Height / 2;
            var centerPoint = new PixelPoint((int)centerX, (int)centerY);

            var targetScreen = _coordinateMapper.GetScreenFromPoint(centerPoint, screens);
            if (targetScreen != null)
            {
                Log.Debug("Selection at ({X}, {Y}) is on screen with scaling {Scaling}",
                    centerX, centerY, targetScreen.Scaling);
            }
            else
            {
                Log.Debug("Could not determine screen for selection at ({X}, {Y})", centerX, centerY);
            }

            return targetScreen;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to determine target screen for selection");
            return null;
        }
    }
}

