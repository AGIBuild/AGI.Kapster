using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.Overlay;
using AGI.Kapster.Desktop.Services.Overlay.Controllers;
using AGI.Kapster.Desktop.Services.Overlay.State;

namespace AGI.Kapster.Desktop.Extensions;

/// <summary>
/// Extension methods for registering overlay controller
/// </summary>
public static class OverlayServiceExtensions
{
    /// <summary>
    /// Register platform-specific overlay controller
    /// </summary>
    public static IServiceCollection AddOverlayServices(this IServiceCollection services)
    {
        // Register OverlayWindow factory (Singleton factory creates Transient windows)
        services.AddSingleton<IOverlayWindowFactory, OverlayWindowFactory>();
        
        // Register OverlaySession factory (Singleton factory creates Transient sessions)
        services.AddSingleton<IOverlaySessionFactory, OverlaySessionFactory>();

#pragma warning disable CA1416 // Platform compatibility checked by RuntimeInformation.IsOSPlatform
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<IOverlayController, WindowsOverlayController>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddSingleton<IOverlayController, MacOverlayController>();
        }
        else
        {
            // Default to Windows implementation for other platforms
            services.AddSingleton<IOverlayController, WindowsOverlayController>();
        }
#pragma warning restore CA1416

        return services;
    }
}

