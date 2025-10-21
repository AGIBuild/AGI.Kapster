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
            services.AddSingleton<IElementDetector, WindowsElementDetector>(); // Singleton: maintains detection mode state
            services.AddSingleton<IScreenCaptureStrategy, WindowsScreenCaptureStrategy>(); // Singleton: stateless, reusable
            services.AddTransient<IOverlayRenderer, WindowsOverlayRenderer>(); // Transient: mutable Theme per render
            services.AddSingleton<IClipboardStrategy, WindowsClipboardStrategy>(); // Singleton: stateless Win32 wrapper
            services.AddTransient<IScreenCoordinateMapper, WindowsCoordinateMapper>(); // Transient: fresh screen info per screenshot
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddSingleton<IElementDetector, NullElementDetector>(); // Singleton: no state needed
            services.AddSingleton<IScreenCaptureStrategy, MacScreenCaptureStrategy>(); // Singleton: stateless, reusable
            services.AddTransient<IOverlayRenderer, WindowsOverlayRenderer>(); // Transient: mutable Theme per render
            services.AddSingleton<IClipboardStrategy, MacClipboardStrategy>(); // Singleton: stateless API wrapper
            services.AddTransient<IScreenCoordinateMapper, MacCoordinateMapper>(); // Transient: fresh screen info per screenshot
        }
        else
        {
            // Default to Windows implementations for other platforms
            services.AddSingleton<IElementDetector, WindowsElementDetector>(); // Singleton: maintains detection mode state
            services.AddSingleton<IScreenCaptureStrategy, WindowsScreenCaptureStrategy>(); // Singleton: stateless, reusable
            services.AddTransient<IOverlayRenderer, WindowsOverlayRenderer>(); // Transient: mutable Theme per render
            services.AddSingleton<IClipboardStrategy, WindowsClipboardStrategy>(); // Singleton: stateless Win32 wrapper
            services.AddTransient<IScreenCoordinateMapper, WindowsCoordinateMapper>(); // Transient: fresh screen info per screenshot
        }
#pragma warning restore CA1416

        return services;
    }
}

