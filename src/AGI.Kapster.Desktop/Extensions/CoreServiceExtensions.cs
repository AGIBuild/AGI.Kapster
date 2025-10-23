using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using AGI.Kapster.Desktop.Services;
using AGI.Kapster.Desktop.Services.Settings;
using AGI.Kapster.Desktop.Services.Update;
using AGI.Kapster.Desktop.Services.ErrorHandling;
using AGI.Kapster.Desktop.Views;

namespace AGI.Kapster.Desktop.Extensions;

/// <summary>
/// Extension methods for registering core application services
/// </summary>
public static class CoreServiceExtensions
{
    /// <summary>
    /// Register core application services including settings, notifications, and updates
    /// </summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Register screen monitor service (resolved from MainWindow)
        services.AddSingleton<IScreenMonitorService>(sp =>
        {
            var lifetime = (IClassicDesktopStyleApplicationLifetime?)Application.Current?.ApplicationLifetime;
            if (lifetime?.MainWindow is IScreenMonitorService service)
            {
                return service;
            }
            throw new InvalidOperationException("MainWindow is not IScreenMonitorService");
        });
        
        services.AddSingleton<ISystemTrayService, SystemTrayService>();
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        
        services.AddSingleton<IApplicationController, ApplicationController>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<IErrorHandler, ErrorHandler>();
        services.AddTransient<SettingsWindow>();

        return services;
    }
}

