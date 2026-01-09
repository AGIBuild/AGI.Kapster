using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace AGI.Kapster.Desktop.Services.Telemetry;

/// <summary>
/// Collects environment information for telemetry
/// </summary>
public static class EnvironmentInfo
{
    private static string? _machineId;

    /// <summary>
    /// Gets the unique machine identifier based on hardware fingerprint
    /// </summary>
    public static string MachineId => GetMachineId();

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
            ["machine_id"] = GetMachineId(),
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

    private static string GetMachineId()
    {
        if (_machineId != null) return _machineId;

        var sb = new StringBuilder();

        // 1. Network Interfaces (MAC Address)
        // This is a standard .NET API available on all platforms that gives us a hardware identifier.
        sb.Append(GetPrimaryMacAddress());

        // 2. Stable environment properties as salt
        // These might change if the user renames the machine, but together with MAC address
        // they provide a reasonable fingerprint using only standard APIs.
        sb.Append(Environment.MachineName);
        sb.Append(Environment.ProcessorCount);

        _machineId = ComputeSha256Hash(sb.ToString());
        return _machineId;
    }

    private static string GetPrimaryMacAddress()
    {
        try
        {
            // Use NetworkInterface to get hardware address
            // This is robust and works across Windows, macOS, and Linux via standard .NET API
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            
            // Sort to ensure consistency
            Array.Sort(interfaces, (a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

            foreach (var ni in interfaces)
            {
                // Skip loopback and temporary interfaces
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                
                // We prefer interfaces that are actually Up, but if none are Up, we take what we can get
                // to ensure we at least get some ID.
                // However, "Up" status might change (e.g. unplugged cable), so relying solely on Up interfaces
                // for a permanent ID is risky if the user often disconnects.
                // Instead, we prioritize Ethernet/WiFi and persistence.
                
                // Basic filtering for valid physical addresses
                try {
                    var address = ni.GetPhysicalAddress();
                    var bytes = address.GetAddressBytes();
                    if (bytes.Length > 0)
                    {
                        return address.ToString();
                    }
                } catch {}
            }
        }
        catch
        {
            // Ignore access denied or other errors
        }
        return string.Empty;
    }

    private static string ComputeSha256Hash(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
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
