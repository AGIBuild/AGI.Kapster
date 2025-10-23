using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace AGI.Kapster.Desktop.Services.Export;

/// <summary>
/// Service for exporting annotated screenshots with UI dialogs and progress tracking
/// </summary>
public interface IAnnotationExportService
{
    /// <summary>
    /// Handle the full export workflow: show dialogs, capture, and save
    /// </summary>
    /// <param name="window">Owner window for dialogs</param>
    /// <param name="selectionRect">Region to export</param>
    /// <param name="captureFunc">Function to capture the final image</param>
    /// <param name="onComplete">Callback when export is complete</param>
    Task HandleExportRequestAsync(
        Window window,
        Rect selectionRect,
        System.Func<Task<Bitmap?>> captureFunc,
        System.Action? onComplete = null);
}

