using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.Capture.Platforms;
using AGI.Kapster.Desktop.Services.Clipboard;
using AGI.Kapster.Desktop.Services.Clipboard.Platforms;
using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Services.Input;
using AGI.Kapster.Desktop.Services.Overlay;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using AGI.Kapster.Desktop.Services.Overlay.Platforms;
using AGI.Kapster.Desktop.Services.Overlay.State;
using AGI.Kapster.Desktop.Services.Screenshot;
using AGI.Kapster.Desktop.Services.UI;
using AGI.Kapster.Desktop.Services.Export;
using AGI.Kapster.Desktop.Rendering.Overlays;

namespace AGI.Kapster.Desktop.Extensions;

/// <summary>
/// Extension methods for registering screen capture, overlay, and screenshot services
/// </summary>
public static class CaptureServiceExtensions
{
    /// <summary>
    /// Register all screenshot-related services including capture, overlay, UI, and coordination
    /// </summary>
    public static IServiceCollection AddCaptureServices(this IServiceCollection services)
    {
#pragma warning disable CA1416 // Platform compatibility checked by RuntimeInformation.IsOSPlatform

        // ============================================================
        // Platform-Specific Services (Capture, Rendering, Clipboard)
        // ============================================================
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddTransient<IElementDetector, WindowsElementDetector>();  // Transient: has mutable state per window
            services.AddSingleton<IScreenCaptureStrategy, WindowsScreenCaptureStrategy>();
            services.AddTransient<IOverlayRenderer, WindowsOverlayRenderer>();
            services.AddSingleton<IClipboardStrategy, WindowsClipboardStrategy>();
            services.AddTransient<IScreenCoordinateMapper, WindowsCoordinateMapper>();
            services.AddTransient<IImeController, WindowsImeController>();  // Transient: manages per-window IME context
            services.AddSingleton<IOverlayCoordinator, WindowsOverlayCoordinator>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddTransient<IElementDetector, NullElementDetector>();  // Transient: consistent with Windows
            services.AddSingleton<IScreenCaptureStrategy, MacScreenCaptureStrategy>();
            services.AddTransient<IOverlayRenderer, WindowsOverlayRenderer>();
            services.AddSingleton<IClipboardStrategy, MacClipboardStrategy>();
            services.AddTransient<IScreenCoordinateMapper, MacCoordinateMapper>();
            services.AddTransient<IImeController, NoOpImeController>();  // Transient: consistent with Windows
            services.AddSingleton<IOverlayCoordinator, MacOverlayCoordinator>();
        }
        else
        {
            // Default to Windows implementations for other platforms
            services.AddTransient<IElementDetector, WindowsElementDetector>();  // Transient: has mutable state per window
            services.AddSingleton<IScreenCaptureStrategy, WindowsScreenCaptureStrategy>();
            services.AddTransient<IOverlayRenderer, WindowsOverlayRenderer>();
            services.AddSingleton<IClipboardStrategy, WindowsClipboardStrategy>();
            services.AddTransient<IScreenCoordinateMapper, WindowsCoordinateMapper>();
            services.AddTransient<IImeController, NoOpImeController>();  // Transient: consistent with Windows
            services.AddSingleton<IOverlayCoordinator, WindowsOverlayCoordinator>();
        }

#pragma warning restore CA1416

        // ============================================================
        // Cross-Platform Services (UI, Factories, Business Logic)
        // ============================================================
        
        // UI services
        services.AddSingleton<IToolbarPositionCalculator, ToolbarPositionCalculator>();
        
        // Factory services (Singleton factories create Transient instances)
        services.AddSingleton<IOverlayWindowFactory, OverlayWindowFactory>();
        services.AddSingleton<IOverlaySessionFactory, OverlaySessionFactory>();

        // High-level business logic
        services.AddSingleton<IScreenshotService, ScreenshotService>();

        // Image Capture Service for overlays
        services.AddTransient<IOverlayImageCaptureService, OverlayImageCaptureService>();

        // Annotation Export Service
        services.AddTransient<IAnnotationExportService, AnnotationExportService>();

        return services;
    }
}

