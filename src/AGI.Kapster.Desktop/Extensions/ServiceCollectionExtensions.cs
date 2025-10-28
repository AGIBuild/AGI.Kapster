using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using AGI.Kapster.Desktop.Services;
using AGI.Kapster.Desktop.Services.Annotation;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.Capture.Platforms;
using AGI.Kapster.Desktop.Services.Clipboard;
using AGI.Kapster.Desktop.Services.Clipboard.Platforms;
using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Services.ErrorHandling;
using AGI.Kapster.Desktop.Services.Export;
using AGI.Kapster.Desktop.Services.Hotkeys;
using AGI.Kapster.Desktop.Services.Input;
using AGI.Kapster.Desktop.Services.Overlay;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using AGI.Kapster.Desktop.Services.Overlay.Platforms;
using AGI.Kapster.Desktop.Services.Overlay.State;
using AGI.Kapster.Desktop.Services.Settings;
using AGI.Kapster.Desktop.Services.Startup;
using AGI.Kapster.Desktop.Services.UI;
using AGI.Kapster.Desktop.Services.Update;
using AGI.Kapster.Desktop.Rendering.Overlays;
using AGI.Kapster.Desktop.Views;

namespace AGI.Kapster.Desktop.Extensions;

/// <summary>
/// Unified service collection extension methods for AGI.Kapster
/// Organizes all service registrations by functional domain
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register all AGI.Kapster services
    /// </summary>
    public static IServiceCollection AddKapsterServices(this IServiceCollection services)
    {
        return services
            .AddCoreServices()
            .AddCaptureServices()
            .AddOverlayServices()
            .AddAnnotationServices()
            .AddHotkeyServices()
            .AddStartupServices();
    }

    #region Core Services (Settings, Tray, Lifecycle, Notifications, Error Handling)

    /// <summary>
    /// Register core application services including settings, notifications, and updates
    /// </summary>
    private static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Screen monitor service (resolved from MainWindow)
        services.AddSingleton<IScreenMonitorService>(sp =>
        {
            var lifetime = (IClassicDesktopStyleApplicationLifetime?)Application.Current?.ApplicationLifetime;
            if (lifetime?.MainWindow is IScreenMonitorService service)
            {
                return service;
            }
            throw new InvalidOperationException("MainWindow is not IScreenMonitorService");
        });
        
        // Core services
        services.AddSingleton<ISystemTrayService, SystemTrayService>();
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IApplicationController, ApplicationController>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<IErrorHandler, ErrorHandler>();
        
        // UI windows
        services.AddTransient<SettingsWindow>();

        return services;
    }

    #endregion

    #region Capture Services (Screen Capture, Clipboard, Element Detection)

    /// <summary>
    /// Register platform-specific capture services
    /// </summary>
    private static IServiceCollection AddCaptureServices(this IServiceCollection services)
    {
#pragma warning disable CA1416 // Platform compatibility checked by RuntimeInformation.IsOSPlatform

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddTransient<IElementDetector, WindowsElementDetector>();
            services.AddSingleton<IScreenCaptureStrategy, WindowsScreenCaptureStrategy>();
            services.AddSingleton<IClipboardStrategy, WindowsClipboardStrategy>();
            services.AddTransient<IScreenCoordinateMapper, WindowsCoordinateMapper>();
            services.AddTransient<IImeController, WindowsImeController>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddTransient<IElementDetector, NullElementDetector>();
            services.AddSingleton<IScreenCaptureStrategy, MacScreenCaptureStrategy>();
            services.AddSingleton<IClipboardStrategy, MacClipboardStrategy>();
            services.AddTransient<IScreenCoordinateMapper, MacCoordinateMapper>();
            services.AddTransient<IImeController, NoOpImeController>();
        }
        else
        {
            // Default implementations for other platforms
            services.AddTransient<IElementDetector, WindowsElementDetector>();
            services.AddSingleton<IScreenCaptureStrategy, WindowsScreenCaptureStrategy>();
            services.AddSingleton<IClipboardStrategy, WindowsClipboardStrategy>();
            services.AddTransient<IScreenCoordinateMapper, WindowsCoordinateMapper>();
            services.AddTransient<IImeController, NoOpImeController>();
        }

#pragma warning restore CA1416

        return services;
    }

    #endregion

    #region Overlay Services (Coordinators, Factories, Rendering)

    /// <summary>
    /// Register overlay coordination and rendering services
    /// </summary>
    private static IServiceCollection AddOverlayServices(this IServiceCollection services)
    {
#pragma warning disable CA1416 // Platform compatibility checked by RuntimeInformation.IsOSPlatform

        // Platform-specific coordinator
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddTransient<IOverlayRenderer, WindowsOverlayRenderer>();
            services.AddSingleton<IOverlayCoordinator, WindowsOverlayCoordinator>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddTransient<IOverlayRenderer, WindowsOverlayRenderer>();
            services.AddSingleton<IOverlayCoordinator, MacOverlayCoordinator>();
        }
        else
        {
            services.AddTransient<IOverlayRenderer, WindowsOverlayRenderer>();
            services.AddSingleton<IOverlayCoordinator, WindowsOverlayCoordinator>();
        }

#pragma warning restore CA1416

        // Cross-platform services
        services.AddSingleton<IToolbarPositionCalculator, ToolbarPositionCalculator>();
        services.AddSingleton<IOverlayWindowFactory, OverlayWindowFactory>();
        services.AddSingleton<IOverlaySessionFactory, OverlaySessionFactory>();

        return services;
    }

    #endregion

    #region Annotation Services (Annotation Management, Export)

    /// <summary>
    /// Register annotation and export services
    /// </summary>
    private static IServiceCollection AddAnnotationServices(this IServiceCollection services)
    {
        services.AddTransient<IAnnotationService, AnnotationService>();
        services.AddTransient<IExportService, ExportService>();

        return services;
    }

    #endregion

    #region Hotkey Services (Global Hotkey Management)

    /// <summary>
    /// Register platform-specific hotkey services
    /// </summary>
    private static IServiceCollection AddHotkeyServices(this IServiceCollection services)
    {
#pragma warning disable CA1416 // Platform compatibility checked by RuntimeInformation.IsOSPlatform

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<IHotkeyProvider, WindowsHotkeyProvider>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddSingleton<IHotkeyProvider, MacHotkeyProvider>();
        }
        else
        {
            services.AddSingleton<IHotkeyProvider, UnsupportedHotkeyProvider>();
        }

#pragma warning restore CA1416

        services.AddSingleton<IHotkeyManager, HotkeyManager>();

        return services;
    }

    #endregion

    #region Startup Services (Platform-specific Startup Managers)

    /// <summary>
    /// Register platform-specific startup services
    /// </summary>
    private static IServiceCollection AddStartupServices(this IServiceCollection services)
    {
#pragma warning disable CA1416 // Platform compatibility checked by RuntimeInformation.IsOSPlatform

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<IStartupManager, WindowsStartupManager>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddSingleton<IStartupManager, MacStartupManager>();
        }
        else
        {
            services.AddSingleton<IStartupManager, UnsupportedStartupManager>();
        }

#pragma warning restore CA1416

        return services;
    }

    #endregion
}
