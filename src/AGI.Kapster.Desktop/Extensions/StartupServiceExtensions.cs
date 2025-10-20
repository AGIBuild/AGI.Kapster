using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using AGI.Kapster.Desktop.Services;
using AGI.Kapster.Desktop.Services.Startup;

namespace AGI.Kapster.Desktop.Extensions;

/// <summary>
/// Extension methods for registering system startup services
/// </summary>
public static class StartupServiceExtensions
{
    /// <summary>
    /// Register platform-specific startup manager for controlling app autostart
    /// </summary>
    public static IServiceCollection AddStartupServices(this IServiceCollection services)
    {
#pragma warning disable CA1416 // Platform compatibility validation handled by RuntimeInformation check
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<IStartupManager>(sp => new WindowsStartupManager());
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddSingleton<IStartupManager>(sp => new MacStartupManager());
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            services.AddSingleton<IStartupManager>(sp => new LinuxStartupManager());
        }
        else
        {
            services.AddSingleton<IStartupManager>(sp => new UnsupportedStartupManager());
        }
#pragma warning restore CA1416

        return services;
    }
}

