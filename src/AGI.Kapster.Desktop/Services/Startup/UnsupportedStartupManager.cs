using System.Threading.Tasks;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Startup;

/// <summary>
/// Unsupported platform startup manager (no-op implementation)
/// </summary>
public class UnsupportedStartupManager : IStartupManager
{
    public bool IsSupported => false;

    public Task<bool> SetStartupAsync(bool enabled)
    {
        Log.Warning("Startup with system is not supported on this platform");
        return Task.FromResult(false);
    }

    public Task<bool> IsStartupEnabledAsync()
    {
        return Task.FromResult(false);
    }
}

