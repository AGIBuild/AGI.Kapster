using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AGI.Kapster.Desktop.Services.Telemetry;

/// <summary>
/// Collects environment information for telemetry
/// </summary>
public static class EnvironmentInfo
{
    /// <summary>
    /// Gets environment properties for telemetry tracking
    /// </summary>
    public static Dictionary<string, string> GetProperties()
    {
        var props = new Dictionary<string, string>
        {
            // Platform info
            ["os_platform"] = GetOSPlatform(),
            ["os_version"] = Environment.OSVersion.VersionString,
            ["os_architecture"] = RuntimeInformation.OSArchitecture.ToString(),
            ["process_architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            
            // Runtime info
            ["dotnet_version"] = RuntimeInformation.FrameworkDescription,
            ["runtime_identifier"] = RuntimeInformation.RuntimeIdentifier,
            
            // App info
            ["app_version"] = GetAppVersion(),
            ["app_culture"] = CultureInfo.CurrentCulture.Name,
            ["app_ui_culture"] = CultureInfo.CurrentUICulture.Name,
            
            // Machine info (anonymized)
            ["processor_count"] = Environment.ProcessorCount.ToString(),
            ["is_64bit_os"] = Environment.Is64BitOperatingSystem.ToString(),
            ["is_64bit_process"] = Environment.Is64BitProcess.ToString(),
        };

        // Add screen info if available
        try
        {
            // Note: Can't easily get screen info before Avalonia is fully initialized
            // This will be tracked separately if needed
        }
        catch
        {
            // Ignore screen info errors
        }

        return props;
    }

    /// <summary>
    /// Gets a summary string for logging
    /// </summary>
    public static string GetSummary()
    {
        return $"{GetOSPlatform()} {RuntimeInformation.OSArchitecture} | .NET {Environment.Version} | v{GetAppVersion()}";
    }

    private static string GetOSPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "macOS";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "Linux";
        return "Unknown";
    }

    private static string GetAppVersion()
    {
        try
        {
            return Assembly.GetExecutingAssembly()
                .GetName()
                .Version?
                .ToString() ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}
