using System;
using AGI.Kapster.Desktop.Overlays.Infrastructure;
using AGI.Kapster.Desktop.Overlays.Layers;
using AGI.Kapster.Desktop.Overlays.Events;
using AGI.Kapster.Desktop.Services.Input;
using Microsoft.Extensions.DependencyInjection;

namespace AGI.Kapster.Desktop.Extensions;

/// <summary>
/// DI extensions for registering orchestrator and related infrastructure
/// </summary>
public static class OrchestratorServiceExtensions
{
    /// <summary>
    /// Register orchestrator infrastructure components
    /// </summary>
    public static IServiceCollection AddOverlayOrchestrator(this IServiceCollection services)
    {
        // Register OverlayContextProvider (Transient - created per overlay window)
        services.AddTransient<OverlayContextProvider>();

        // Register InputRouter (Transient - created per overlay window)
        services.AddTransient<InputRouter>();

        // Register IOverlayOrchestrator (Transient - created per overlay window via factory)
        services.AddTransient<IOverlayOrchestrator, OverlayOrchestrator>();

        return services;
    }
}

