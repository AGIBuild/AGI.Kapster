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
        
        // Layer manager - singleton as there's typically one overlay at a time
        services.AddSingleton<IOverlayLayerManager, OverlayLayerManager>();
        
        // Register orchestrator infrastructure (transient per overlay window)
        services.AddOverlayOrchestrator();
        
        // Note: OverlayWindowBuilder is created by Session.CreateWindowBuilder(), not through DI
        // Note: Individual layers (MaskLayer, SelectionLayer, etc.) will be created
        // per-overlay-instance within the OverlayOrchestrator, not through DI
        
        return services;
    }
}

