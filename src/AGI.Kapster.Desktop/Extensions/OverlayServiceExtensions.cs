using Microsoft.Extensions.DependencyInjection;
using AGI.Kapster.Desktop.Overlays.Events;
using AGI.Kapster.Desktop.Overlays.Layers;

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
        
        // Note: Individual layers (MaskLayer, SelectionLayer, etc.) will be created
        // per-overlay-instance within the OverlayWindow constructor, not through DI
        // This is because layers need specific XAML control references (Path, Canvas, etc.)
        
        return services;
    }
}

