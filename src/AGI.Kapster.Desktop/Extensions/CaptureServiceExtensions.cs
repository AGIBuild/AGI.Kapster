using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using AGI.Kapster.Desktop.Services.Overlay;
using AGI.Kapster.Desktop.Services.Overlay.Platforms;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Services.Clipboard;
using AGI.Kapster.Desktop.Services.Clipboard.Platforms;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.Capture.Platforms;
using AGI.Kapster.Desktop.Rendering.Overlays;

namespace AGI.Kapster.Desktop.Extensions;

/// <summary>
/// Extension methods for registering screen capture and overlay services
/// </summary>
public static class CaptureServiceExtensions
{
    /// <summary>
    /// Register platform-specific capture, overlay, and clipboard services
    /// </summary>
    public static IServiceCollection AddCaptureServices(this IServiceCollection services)
    {
#pragma warning disable CA1416 // Platform compatibility checked by RuntimeInformation.IsOSPlatform
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddTransient<IElementDetector, WindowsElementDetector>();
            services.AddSingleton<IScreenCaptureStrategy, WindowsScreenCaptureStrategy>();
            services.AddSingleton<IOverlayRenderer, WindowsOverlayRenderer>();
            services.AddSingleton<IClipboardStrategy, WindowsClipboardStrategy>();
            services.AddTransient<IScreenCoordinateMapper, WindowsCoordinateMapper>(); // Transient: screen info changes dynamically
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddTransient<IElementDetector, NullElementDetector>();
            services.AddSingleton<IScreenCaptureStrategy, MacScreenCaptureStrategy>();
            services.AddSingleton<IOverlayRenderer, WindowsOverlayRenderer>(); // Reuse Windows renderer
            services.AddSingleton<IClipboardStrategy, MacClipboardStrategy>();
            services.AddTransient<IScreenCoordinateMapper, MacCoordinateMapper>(); // Transient: screen info changes dynamically
        }
        else
        {
            // Default to Windows implementations for other platforms
            services.AddTransient<IElementDetector, WindowsElementDetector>();
            services.AddSingleton<IScreenCaptureStrategy, WindowsScreenCaptureStrategy>();
            services.AddSingleton<IOverlayRenderer, WindowsOverlayRenderer>();
            services.AddSingleton<IClipboardStrategy, WindowsClipboardStrategy>();
            services.AddTransient<IScreenCoordinateMapper, WindowsCoordinateMapper>(); // Transient: screen info changes dynamically
        }
#pragma warning restore CA1416

        return services;
    }
}

