using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using AGI.Kapster.Desktop.Services.Overlay;
using AGI.Kapster.Desktop.Services.Overlay.Platforms;
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
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddTransient<IOverlayWindow, WindowsOverlayWindow>();
            services.AddTransient<IElementDetector, WindowsElementDetector>();
            services.AddSingleton<IScreenCaptureStrategy, WindowsScreenCaptureStrategy>();
            services.AddSingleton<IOverlayRenderer, WindowsOverlayRenderer>();
            services.AddSingleton<IClipboardStrategy, WindowsClipboardStrategy>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddTransient<IOverlayWindow, MacOverlayWindow>();
            services.AddTransient<IElementDetector, NullElementDetector>();
            services.AddSingleton<IScreenCaptureStrategy, MacScreenCaptureStrategy>();
            services.AddSingleton<IOverlayRenderer, WindowsOverlayRenderer>(); // Reuse Windows renderer
            services.AddSingleton<IClipboardStrategy, MacClipboardStrategy>();
        }
        else
        {
            // Default to Windows implementations for other platforms
            services.AddTransient<IOverlayWindow, WindowsOverlayWindow>();
            services.AddTransient<IElementDetector, WindowsElementDetector>();
            services.AddSingleton<IScreenCaptureStrategy, WindowsScreenCaptureStrategy>();
            services.AddSingleton<IOverlayRenderer, WindowsOverlayRenderer>();
            services.AddSingleton<IClipboardStrategy, WindowsClipboardStrategy>();
        }

        return services;
    }
}

