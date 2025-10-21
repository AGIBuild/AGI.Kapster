using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AGI.Kapster.Desktop.Models;

namespace AGI.Kapster.Desktop.Services.Export;

/// <summary>
/// Export service for saving annotated screenshots
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Export annotated screenshot to file with default settings
    /// </summary>
    /// <param name="screenshot">Background screenshot bitmap</param>
    /// <param name="annotations">Annotations to render</param>
    /// <param name="selectionRect">Selection rectangle in screen coordinates</param>
    /// <param name="filePath">Target file path</param>
    /// <param name="targetScreen">Optional target screen for DPI scaling (uses primary screen if null)</param>
    /// <returns>Task representing the export operation</returns>
    Task ExportToFileAsync(Bitmap screenshot, IEnumerable<IAnnotationItem> annotations, Rect selectionRect, string filePath, Screen? targetScreen = null);

    /// <summary>
    /// Export annotated screenshot to file with custom settings
    /// </summary>
    /// <param name="screenshot">Background screenshot bitmap</param>
    /// <param name="annotations">Annotations to render</param>
    /// <param name="selectionRect">Selection rectangle in screen coordinates</param>
    /// <param name="filePath">Target file path</param>
    /// <param name="settings">Export settings including format and quality</param>
    /// <param name="progressCallback">Optional progress callback (percentage, status message)</param>
    /// <param name="targetScreen">Optional target screen for DPI scaling (uses primary screen if null)</param>
    /// <returns>Task representing the export operation</returns>
    Task ExportToFileAsync(Bitmap screenshot, IEnumerable<IAnnotationItem> annotations, Rect selectionRect, string filePath, ExportSettings settings, Action<int, string>? progressCallback = null, Screen? targetScreen = null);

    /// <summary>
    /// Export annotated screenshot to clipboard
    /// </summary>
    /// <param name="screenshot">Background screenshot bitmap</param>
    /// <param name="annotations">Annotations to render</param>
    /// <param name="selectionRect">Selection rectangle in screen coordinates</param>
    /// <param name="targetScreen">Optional target screen for DPI scaling (uses primary screen if null)</param>
    /// <returns>Task representing the export operation</returns>
    Task ExportToClipboardAsync(Bitmap screenshot, IEnumerable<IAnnotationItem> annotations, Rect selectionRect, Screen? targetScreen = null);

    /// <summary>
    /// Get supported export formats
    /// </summary>
    /// <returns>List of supported export formats</returns>
    ExportFormat[] GetSupportedFormats();

    /// <summary>
    /// Get default export settings
    /// </summary>
    /// <returns>Default export settings</returns>
    ExportSettings GetDefaultSettings();
}
