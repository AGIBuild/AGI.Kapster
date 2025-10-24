using System.Threading.Tasks;
using Avalonia;

namespace AGI.Kapster.Desktop.Overlays;

/// <summary>
/// Handles top-level overlay actions initiated by layers (confirm/export/cancel).
/// </summary>
public interface IOverlayActionHandler
{
    Task HandleConfirmAsync(Rect region);
    Task HandleExportAsync(Rect region);
    void HandleCancel(string reason);
}
