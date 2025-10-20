using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using AGI.Kapster.Desktop.Services.Hotkeys;

namespace AGI.Kapster.Desktop.Extensions;

/// <summary>
/// Extension methods for registering hotkey services
/// </summary>
public static class HotkeyServiceExtensions
{
    /// <summary>
    /// Register hotkey manager and platform-specific hotkey provider
    /// </summary>
    public static IServiceCollection AddHotkeyServices(this IServiceCollection services)
    {
        services.AddSingleton<IHotkeyManager, HotkeyManager>();
        
        // Register platform-specific hotkey provider
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

        return services;
    }
}

