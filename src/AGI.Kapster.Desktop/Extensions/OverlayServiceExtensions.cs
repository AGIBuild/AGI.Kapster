using Microsoft.Extensions.DependencyInjection;
using AGI.Kapster.Desktop.Overlays.Events;
using AGI.Kapster.Desktop.Overlays.Layers;
using AGI.Kapster.Desktop.Services.Overlay;

namespace AGI.Kapster.Desktop.Extensions;

/// <summary>
/// Extension methods for registering overlay-related services
/// </summary>
public static class OverlayServiceExtensions
{
    public static IServiceCollection AddOverlayServices(this IServiceCollection services)
    {
        // Event bus - singleton for application-wide event coordination
        services.AddSingleton<IOverlayEventBus, OverlayEventBus>();
        
        // Layer manager - transient per session for state isolation
        services.AddTransient<IOverlayLayerManager, OverlayLayerManager>();
        
        // Register orchestrator infrastructure (transient per overlay window)
        services.AddOverlayOrchestrator();
        
        return services;
    }
}

