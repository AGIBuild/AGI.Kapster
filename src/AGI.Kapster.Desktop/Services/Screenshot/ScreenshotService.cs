using System;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Screenshot;

/// <summary>
/// Default implementation of screenshot service
/// Delegates to platform-specific overlay coordinator
/// </summary>
public class ScreenshotService : IScreenshotService
{
    private readonly IOverlayCoordinator _coordinator;

    public ScreenshotService(IOverlayCoordinator coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public bool IsActive => _coordinator.HasActiveSession;

    public async Task TakeScreenshotAsync()
    {
        try
        {
            Log.Information("[ScreenshotService] Starting screenshot operation");
            await _coordinator.StartSessionAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ScreenshotService] Failed to take screenshot");
            throw;
        }
    }

    public void Cancel()
    {
        try
        {
            Log.Information("[ScreenshotService] Cancelling screenshot operation");
            _coordinator.CloseCurrentSession();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ScreenshotService] Error cancelling screenshot");
        }
    }
}

