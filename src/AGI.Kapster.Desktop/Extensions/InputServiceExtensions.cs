using AGI.Kapster.Desktop.Services.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.InteropServices;

namespace AGI.Kapster.Desktop.Extensions;

/// <summary>
/// Extension methods for registering input-related services
/// </summary>
public static class InputServiceExtensions
{
    public static IServiceCollection AddInputServices(this IServiceCollection services)
    {
        // Register platform-specific IME controller
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<IImeController, WindowsImeController>();
        }
        else
        {
            // macOS and Linux: use no-op implementation for now
            services.AddSingleton<IImeController, NoOpImeController>();
        }

        return services;
    }
}

