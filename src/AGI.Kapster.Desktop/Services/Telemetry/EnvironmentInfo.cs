using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace AGI.Kapster.Desktop.Services.Telemetry;

/// <summary>
/// Collects environment information for telemetry
/// </summary>
public static class EnvironmentInfo
{
    private static string? _machineId;
    private static string? _userId;

    /// <summary>
    /// Gets the device identifier.
    /// macOS uses gethostuuid(3) and returns empty string on failure (no fallback).
    /// Other platforms use the primary MAC address with a machine name fallback.
    /// </summary>
    public static string MachineId => GetMachineId();
    
    /// <summary>
    /// Gets the current OS user identifier (unique across machines).
    /// Windows: SID. macOS: GeneratedUID (mbr_uid_to_uuid). Linux: UID.
    /// Returns empty string on failure.
    /// </summary>
    public static string UserId => GetUserId();

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
            
            // Identifiers (telemetry)
            ["processor_count"] = Environment.ProcessorCount.ToString(),
            ["is_64bit_os"] = Environment.Is64BitOperatingSystem.ToString(),
            ["is_64bit_process"] = Environment.Is64BitProcess.ToString(),
            // Device identifier (macOS: gethostuuid(3); other platforms: primary MAC)
            ["machine_id"] = GetMachineId(),
            // OS user identifier (Windows: SID; macOS/Linux: UID). May be empty if not resolvable.
            ["user_id"] = GetUserId(),
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

    private static string GetUserId()
    {
        if (_userId != null) return _userId;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _userId = WindowsIdentity.GetCurrent().User?.Value ?? string.Empty;
#if DEBUG
                Debug.WriteLine($"[EnvironmentInfo] UserId (SID): {_userId}");
#endif
                return _userId;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _userId = GetMacOSUserUuid();
#if DEBUG
                Debug.WriteLine($"[EnvironmentInfo] UserId (macOS GeneratedUID): {_userId}");
#endif
                return _userId;
            }

            // Linux: use UID
            _userId = geteuid().ToString(CultureInfo.InvariantCulture);
#if DEBUG
            Debug.WriteLine($"[EnvironmentInfo] UserId (UID): {_userId}");
#endif
            return _userId;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EnvironmentInfo] Failed to retrieve UserId: {ex}");
            _userId = string.Empty;
            return _userId;
        }
    }

    /// <summary>
    /// Gets the macOS user's GeneratedUID via mbr_uid_to_uuid().
    /// This is a true UUID that is unique across machines.
    /// </summary>
    private static string GetMacOSUserUuid()
    {
        try
        {
            var uid = geteuid();
            var uuidBytes = new byte[16];
            var rc = mbr_uid_to_uuid(uid, uuidBytes);
            if (rc != 0)
            {
                return string.Empty;
            }

            return FormatUuid(uuidBytes);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EnvironmentInfo] Failed to retrieve macOS user UUID: {ex}");
            return string.Empty;
        }
    }

    private static string GetMachineId()
    {
        if (_machineId != null) return _machineId;

        // macOS: Host UUID (stable across reboots/updates)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            _machineId = GetMacOSHostUuid();
#if DEBUG
            Debug.WriteLine($"[EnvironmentInfo] macOS MachineId (gethostuuid): {_machineId}");
#endif
            return _machineId;
        }

        _machineId = GetPrimaryMacAddress();

        // Fallback to MachineName if MAC is unavailable
        if (string.IsNullOrEmpty(_machineId))
        {
            _machineId = Environment.MachineName;
        }

        return _machineId;
    }

    /// <summary>
    /// Gets macOS Host UUID via gethostuuid(3).
    /// </summary>
    private static string GetMacOSHostUuid()
    {
        try
        {
            var bytes = new byte[16];
            var timeout = new Timespec { tv_sec = 5, tv_nsec = 0 };
            var rc = gethostuuid(bytes, ref timeout);
            if (rc != 0)
            {
                return string.Empty;
            }

            return FormatUuid(bytes);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EnvironmentInfo] Failed to retrieve macOS Host UUID: {ex}");
            return string.Empty;
        }
    }

    private static string GetPrimaryMacAddress()
    {
        try
        {
            // Use NetworkInterface to get hardware address
            // This is robust and works across Windows, macOS, and Linux via standard .NET API
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            
            // Sort to ensure consistency based on MAC address (more stable than interface name)
            Array.Sort(interfaces, (a, b) =>
            {
                var macA = a.GetPhysicalAddress()?.ToString() ?? string.Empty;
                var macB = b.GetPhysicalAddress()?.ToString() ?? string.Empty;

                var macCompare = string.Compare(macA, macB, StringComparison.OrdinalIgnoreCase);
                if (macCompare != 0)
                {
                    return macCompare;
                }

                // Fallback to name only as a tie-breaker when MACs are equal
                return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            });
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
                try
                {
                    var address = ni.GetPhysicalAddress();
                    var bytes = address.GetAddressBytes();
                    if (bytes.Length > 0 && IsValidMacAddress(bytes))
                    {
                        return address.ToString();
                    }
                }
                catch
                {
                    // Ignore errors for individual interfaces and continue
                }
            }
        }
        catch (Exception ex)
        {
            // Ignore access denied or other errors, but log for diagnostics
            Debug.WriteLine($"[EnvironmentInfo] Failed to retrieve primary MAC address: {ex}");
        }
        return string.Empty;
    }

    private static bool IsValidMacAddress(byte[] bytes)
    {
        // All zeros is not a valid hardware address.
        var allZero = true;
        foreach (var b in bytes)
        {
            if (b != 0)
            {
                allZero = false;
                break;
            }
        }
        if (allZero) return false;

        // Known invalid pattern observed on macOS: 00:00:00:00:00:E0
        if (bytes.Length >= 6 &&
            bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0x00 &&
            bytes[3] == 0x00 && bytes[4] == 0x00 && bytes[5] == 0xE0)
        {
            return false;
        }

        return true;
    }

    private static string FormatUuid(byte[] bytes)
    {
        if (bytes.Length < 16) return string.Empty;

        return $"{bytes[0]:x2}{bytes[1]:x2}{bytes[2]:x2}{bytes[3]:x2}-" +
               $"{bytes[4]:x2}{bytes[5]:x2}-" +
               $"{bytes[6]:x2}{bytes[7]:x2}-" +
               $"{bytes[8]:x2}{bytes[9]:x2}-" +
               $"{bytes[10]:x2}{bytes[11]:x2}{bytes[12]:x2}{bytes[13]:x2}{bytes[14]:x2}{bytes[15]:x2}";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Timespec
    {
        public long tv_sec;
        public long tv_nsec;
    }

    [DllImport("libc")]
    private static extern int gethostuuid([Out] byte[] uuid, ref Timespec wait);

    [DllImport("libc")]
    private static extern uint geteuid();

    [DllImport("libc")]
    private static extern int mbr_uid_to_uuid(uint uid, [Out] byte[] uuid);

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
