using Microsoft.Extensions.DependencyInjection;
using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.Overlay;

namespace AGI.Kapster.Desktop.Extensions;

/// <summary>
/// Extension methods for registering overlay controller
/// </summary>
public static class OverlayServiceExtensions
{
    /// <summary>
    /// Register overlay controller for managing screenshot overlays
    /// </summary>
    public static IServiceCollection AddOverlayServices(this IServiceCollection services)
    {
        services.AddSingleton<IOverlayController, SimplifiedOverlayManager>();
        return services;
    }
}

