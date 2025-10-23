using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Dialogs;
using AGI.Kapster.Desktop.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Export;

/// <summary>
/// Service for handling annotation export with UI dialogs and progress tracking
/// </summary>
public class AnnotationExportService : IAnnotationExportService
{
    public AnnotationExportService()
    {
    }

    public async Task HandleExportRequestAsync(
        Window window,
        Rect selectionRect,
        Func<Task<Bitmap?>> captureFunc,
        Action? onComplete = null)
    {
        try
        {
            if (!ValidateExportPreconditions(selectionRect))
                return;

            var settings = await ShowExportSettingsDialogAsync(window, selectionRect);
            if (settings == null)
                return;

            var file = await ShowSaveFileDialogAsync(window, settings);
            if (file == null)
                return;

            await PerformExportAsync(window, file.Path.LocalPath, settings, captureFunc);

            onComplete?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to handle export request");
        }
    }

    private bool ValidateExportPreconditions(Rect selectionRect)
    {
        if (selectionRect.Width <= 0 || selectionRect.Height <= 0)
        {
            Log.Warning("Cannot export: no valid selection area");
            return false;
        }
        return true;
    }

    private async Task<ExportSettings?> ShowExportSettingsDialogAsync(Window window, Rect selectionRect)
    {
        var exportService = new ExportService();
        var defaultSettings = exportService.GetDefaultSettings();
        var imageSize = new Size(selectionRect.Width, selectionRect.Height);
        var settingsDialog = new ExportSettingsDialog(defaultSettings, imageSize);

        var dialogResult = await settingsDialog.ShowDialog<bool?>(window);
        if (dialogResult != true)
        {
            Log.Information("Export cancelled by user");
            return null;
        }

        return settingsDialog.Settings;
    }

    private async Task<IStorageFile?> ShowSaveFileDialogAsync(Window window, ExportSettings settings)
    {
        var storageProvider = TopLevel.GetTopLevel(window)?.StorageProvider;
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

    private async Task PerformExportAsync(
        Window window,
        string filePath,
        ExportSettings settings,
        Func<Task<Bitmap?>> captureFunc)
    {
        var progressDialog = new ExportProgressDialog();
        progressDialog.SetFileInfo(System.IO.Path.GetFileName(filePath), settings.Format.ToString());

        _ = progressDialog.ShowDialog(window);

        try
        {
            progressDialog.UpdateProgress(10, "Capturing screenshot...");
            var finalImage = await captureFunc();
            
            if (finalImage == null)
                throw new InvalidOperationException("Failed to capture screenshot");

            var exportService = new ExportService();
            await exportService.ExportToFileDirectAsync(finalImage, filePath, settings,
                (percentage, status) =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        progressDialog.UpdateProgress(percentage, status),
                        Avalonia.Threading.DispatcherPriority.Background);
                });

            progressDialog.Close();
            Log.Information("Successfully exported to {FilePath}: {Format}, Q={Quality}",
                filePath, settings.Format, settings.Quality);
        }
        catch (Exception ex)
        {
            var errorMessage = ex.InnerException?.Message ?? ex.Message;
            progressDialog.ShowError($"Export failed: {errorMessage}");
            Log.Error(ex, "Export failed");
            throw;
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
}

