using AGI.Kapster.Desktop.Dialogs;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.Export;
using AGI.Kapster.Desktop.Services.Export.Imaging;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AGI.Kapster.Desktop.Overlays.Handlers;

/// <summary>
/// Manages screenshot capture and export workflow
/// Handles image capture, annotation compositing, file dialogs, and export progress
/// </summary>
internal sealed class CaptureHandler
{
    private readonly Window _window;
    private readonly SelectionOverlay _selector;
    private readonly AnnotationHandler _annotationHandler;
    private readonly IScreenCaptureStrategy? _screenCaptureStrategy;
    private readonly IScreenCoordinateMapper? _coordinateMapper;
    private readonly Func<Bitmap?> _getFrozenBackground;
    private readonly Func<IReadOnlyList<Screen>?> _getScreens;

    // Event for requesting overlay close
    public event Action<string>? CloseRequested;

    public CaptureHandler(
        Window window,
        SelectionOverlay selector,
        AnnotationHandler annotationHandler,
        IScreenCaptureStrategy? screenCaptureStrategy,
        IScreenCoordinateMapper? coordinateMapper,
        Func<Bitmap?> getFrozenBackground,
        Func<IReadOnlyList<Screen>?> getScreens)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _annotationHandler = annotationHandler ?? throw new ArgumentNullException(nameof(annotationHandler));
        _screenCaptureStrategy = screenCaptureStrategy;
        _coordinateMapper = coordinateMapper;
        _getFrozenBackground = getFrozenBackground ?? throw new ArgumentNullException(nameof(getFrozenBackground));
        _getScreens = getScreens ?? throw new ArgumentNullException(nameof(getScreens));
    }

    #region Capture Methods

    /// <summary>
    /// Cross-platform screenshot capture method using strategy pattern
    /// </summary>
    public async Task<Bitmap?> CaptureRegionAsync(Rect rect)
    {
        if (_screenCaptureStrategy == null)
        {
            Log.Error("No screen capture strategy available");
            return null;
        }

        try
        {
            var skBitmap = await _screenCaptureStrategy.CaptureWindowRegionAsync(rect, _window);
            return BitmapConverter.ConvertToAvaloniaBitmap(skBitmap);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to capture region");
            return null;
        }
    }

    /// <summary>
    /// Capture window region with annotations rendered on top
    /// </summary>
    public async Task<Bitmap?> CaptureWindowRegionWithAnnotationsAsync(Rect region)
    {
        try
        {
            // End text editing before capture to prevent TextBox background artifacts
            _annotationHandler.EndTextEditing();

            // Temporarily hide selector
            bool selectorWasVisible = _selector.IsVisible;
            _selector.IsVisible = false;

            try
            {
                var baseScreenshot = ExtractRegionFromFrozenBackground(region);
                if (baseScreenshot == null)
                {
                    Log.Warning("Failed to extract region from frozen background");
                    return null;
                }

                var annotations = _annotationHandler.Annotator?.GetAnnotations();
                if (annotations == null || !annotations.Any())
                {
                    return baseScreenshot;
                }

                // Determine target screen for correct DPI handling
                var targetScreen = GetScreenForSelection(region);

                var exportService = new ExportService();
                return await exportService.CreateCompositeImageWithAnnotationsAsync(
                    baseScreenshot, annotations, region, targetScreen);
            }
            finally
            {
                // Restore selector visibility
                if (selectorWasVisible)
                {
                    _selector.IsVisible = true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to capture window region with annotations");
            return null;
        }
    }

    /// <summary>
    /// Returns base screenshot for a region: uses frozen background if available, otherwise live capture
    /// </summary>
    public async Task<Bitmap?> GetBaseScreenshotForRegionAsync(Rect region)
    {
        var frozenBackground = _getFrozenBackground();
        if (frozenBackground != null)
        {
            Log.Debug("Using frozen background for region {Region}", region);
            return ExtractRegionFromFrozenBackground(region, frozenBackground);
        }
        Log.Debug("Frozen background not available, using live capture for region {Region}", region);
        return await CaptureRegionAsync(region);
    }

    /// <summary>
    /// Extract a region from the frozen background with DPI-aware source rect scaling
    /// </summary>
    public Bitmap? ExtractRegionFromFrozenBackground(Rect region, Bitmap? frozenBackground = null)
    {
        frozenBackground ??= _getFrozenBackground();
        if (frozenBackground == null)
            return null;

        var totalDipWidth = Math.Max(1.0, _window.Bounds.Width);
        var totalDipHeight = Math.Max(1.0, _window.Bounds.Height);
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

    /// <summary>
    /// Get full screen screenshot for color sampling
    /// </summary>
    public async Task<Bitmap?> GetFullScreenScreenshotAsync()
    {
        if (_screenCaptureStrategy == null)
        {
            Log.Debug("No screen capture strategy available for full screen screenshot");
            return null;
        }

        try
        {
            var screenBounds = new Rect(0, 0, _window.Bounds.Width, _window.Bounds.Height);
            var skBitmap = await _screenCaptureStrategy.CaptureWindowRegionAsync(screenBounds, _window);
            return BitmapConverter.ConvertToAvaloniaBitmap(skBitmap);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to capture full screen screenshot for color sampling");
            return null;
        }
    }

    /// <summary>
    /// Determine which screen the selection region is on (using center point)
    /// </summary>
    private Screen? GetScreenForSelection(Rect selectionRect)
    {
        if (_coordinateMapper == null)
        {
            Log.Debug("No coordinate mapper available, cannot determine target screen");
            return null;
        }

        try
        {
            var centerX = selectionRect.X + selectionRect.Width / 2;
            var centerY = selectionRect.Y + selectionRect.Height / 2;
            var centerPoint = new PixelPoint((int)centerX, (int)centerY);

            var screens = _getScreens();
            if (screens == null || screens.Count == 0)
            {
                Log.Warning("Cannot determine target screen: screens not available");
                return null;
            }

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

    #endregion

    #region Export Methods

    /// <summary>
    /// Handle export request from annotation overlay
    /// </summary>
    public async Task HandleExportRequestAsync()
    {
        try
        {
            if (!ValidateExportPreconditions())
                return;

            var settings = await ShowExportSettingsDialogAsync();
            if (settings == null)
                return;

            var file = await ShowSaveFileDialogAsync(settings);
            if (file == null)
                return;

            await PerformExportAsync(file.Path.LocalPath, settings);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to handle export request");
        }
    }

    private bool ValidateExportPreconditions()
    {
        var annotator = _annotationHandler.Annotator;
        if (annotator == null)
            return false;

        var selectionRect = annotator.SelectionRect;
        if (selectionRect.Width <= 0 || selectionRect.Height <= 0)
        {
            Log.Warning("Cannot export: no valid selection area");
            return false;
        }

        return true;
    }

    private async Task<ExportSettings?> ShowExportSettingsDialogAsync()
    {
        var exportService = new ExportService();
        var defaultSettings = exportService.GetDefaultSettings();
        var annotator = _annotationHandler.Annotator!;
        var imageSize = new Size(annotator.SelectionRect.Width, annotator.SelectionRect.Height);
        var settingsDialog = new ExportSettingsDialog(defaultSettings, imageSize);

        var dialogResult = await settingsDialog.ShowDialog<bool?>(_window);
        if (dialogResult != true)
        {
            Log.Information("Export cancelled by user");
            return null;
        }

        return settingsDialog.Settings;
    }

    private async Task<IStorageFile?> ShowSaveFileDialogAsync(ExportSettings settings)
    {
        var storageProvider = TopLevel.GetTopLevel(_window)?.StorageProvider;
        if (storageProvider == null)
        {
            Log.Error("Cannot access storage provider for file dialog");
            return null;
        }

        var exportService = new ExportService();
        var fileTypes = CreateFileTypesFromFormats(exportService.GetSupportedFormats());
        var suggestedFileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}{settings.GetFileExtension()}";

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Annotated Screenshot",
            FileTypeChoices = fileTypes,
            DefaultExtension = settings.GetFileExtension().TrimStart('.'),
            SuggestedFileName = suggestedFileName
        });

        if (file == null)
        {
            Log.Information("File save cancelled by user");
        }

        return file;
    }

    private async Task PerformExportAsync(string filePath, ExportSettings settings)
    {
        var progressDialog = new ExportProgressDialog();
        progressDialog.SetFileInfo(System.IO.Path.GetFileName(filePath), settings.Format.ToString());

        _ = progressDialog.ShowDialog(_window);

        var wasVisible = _selector.IsVisible;

        try
        {
            await HideSelectorAndWaitAsync(progressDialog);

            var finalImage = await CaptureScreenshotForExportAsync(progressDialog);
            if (finalImage == null)
                throw new InvalidOperationException("Failed to capture screenshot");

            await ExportImageToFileAsync(finalImage, filePath, settings, progressDialog);

            progressDialog.Close();
            Log.Information("Successfully exported to {FilePath}: {Format}, Q={Quality}",
                filePath, settings.Format, settings.Quality);

            CloseRequested?.Invoke("export");
        }
        catch (Exception ex)
        {
            RestoreSelectorVisibility(wasVisible);
            var errorMessage = ex.InnerException?.Message ?? ex.Message;
            progressDialog.ShowError($"Export failed: {errorMessage}");
            Log.Error(ex, "Export failed");
            throw;
        }
        finally
        {
            RestoreSelectorVisibility(wasVisible);
        }
    }

    private async Task HideSelectorAndWaitAsync(ExportProgressDialog progressDialog)
    {
        progressDialog.UpdateProgress(5, "Preparing capture...");
        _selector.IsVisible = false;
        await Task.Delay(OverlayConstants.StandardUiDelay);
    }

    private async Task<Bitmap?> CaptureScreenshotForExportAsync(ExportProgressDialog progressDialog)
    {
        progressDialog.UpdateProgress(10, "Capturing screenshot...");
        var annotator = _annotationHandler.Annotator!;
        return await CaptureWindowRegionWithAnnotationsAsync(annotator.SelectionRect);
    }

    private async Task ExportImageToFileAsync(
        Bitmap image,
        string filePath,
        ExportSettings settings,
        ExportProgressDialog progressDialog)
    {
        var exportService = new ExportService();
        await exportService.ExportToFileDirectAsync(image, filePath, settings,
            (percentage, status) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    progressDialog.UpdateProgress(percentage, status),
                    Avalonia.Threading.DispatcherPriority.Background);
            });
    }

    private void RestoreSelectorVisibility(bool wasVisible)
    {
        if (wasVisible)
        {
            _selector.IsVisible = true;
        }
    }

    private FilePickerFileType[] CreateFileTypesFromFormats(ExportFormat[] formats)
    {
        var fileTypes = new List<FilePickerFileType>();

        foreach (var format in formats)
        {
            var extension = GetExtensionForFormat(format);
            var description = GetDescriptionForFormat(format);

            fileTypes.Add(new FilePickerFileType(description)
            {
                Patterns = new[] { $"*{extension}" }
            });
        }

        // Add "All Supported Images" option
        var allPatterns = formats.Select(f => $"*{GetExtensionForFormat(f)}").ToArray();
        fileTypes.Insert(0, new FilePickerFileType("All Supported Images")
        {
            Patterns = allPatterns
        });

        return fileTypes.ToArray();
    }

    private string GetExtensionForFormat(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.PNG => ".png",
            ExportFormat.JPEG => ".jpg",
            ExportFormat.BMP => ".bmp",
            ExportFormat.TIFF => ".tiff",
            ExportFormat.WebP => ".webp",
            ExportFormat.GIF => ".gif",
            _ => ".png"
        };
    }

    private string GetDescriptionForFormat(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.PNG => "PNG Image",
            ExportFormat.JPEG => "JPEG Image",
            ExportFormat.BMP => "Bitmap Image",
            ExportFormat.TIFF => "TIFF Image",
            ExportFormat.WebP => "WebP Image",
            ExportFormat.GIF => "GIF Image",
            _ => "Image File"
        };
    }

    #endregion
}
