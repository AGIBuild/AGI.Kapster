using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using AGI.Kapster.Desktop.Services.Overlay;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using AGI.Kapster.Desktop.Services.Overlay.State;
using AGI.Kapster.Desktop.Services.Screenshot;

namespace AGI.Kapster.Desktop.Extensions;

/// <summary>
/// Extension methods for registering overlay and screenshot services
/// </summary>
public static class OverlayServiceExtensions
{
    /// <summary>
    /// Register platform-specific overlay services and screenshot service
    /// </summary>
    public static IServiceCollection AddOverlayServices(this IServiceCollection services)
    {
        // Register OverlayWindow factory (Singleton factory creates Transient windows)
        services.AddSingleton<IOverlayWindowFactory, OverlayWindowFactory>();
        
        // Register OverlaySession factory (Singleton factory creates Transient sessions)
        services.AddSingleton<IOverlaySessionFactory, OverlaySessionFactory>();

        // Register platform-specific coordinator (Singleton for lifecycle management)
#pragma warning disable CA1416 // Platform compatibility checked by RuntimeInformation.IsOSPlatform
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<IOverlayCoordinator, WindowsOverlayCoordinator>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddSingleton<IOverlayCoordinator, MacOverlayCoordinator>();
        }
        else
        {
            // Default to Windows implementation for other platforms
            services.AddSingleton<IOverlayCoordinator, WindowsOverlayCoordinator>();
        }
#pragma warning restore CA1416

        // Register high-level screenshot service
        services.AddSingleton<IScreenshotService, ScreenshotService>();

        return services;
    }
}

