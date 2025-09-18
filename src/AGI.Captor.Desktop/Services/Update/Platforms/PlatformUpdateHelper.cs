using System;
using System.Runtime.InteropServices;

namespace AGI.Captor.Desktop.Services.Update.Platforms;

/// <summary>
/// Platform-specific update information and utilities
/// </summary>
public static class PlatformUpdateHelper
{
    /// <summary>
    /// Get the package file extension for the current platform
    /// </summary>
    public static string GetPackageExtension()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "msi";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "pkg";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "deb";
        else
            throw new PlatformNotSupportedException($"Platform {RuntimeInformation.OSDescription} is not supported for auto-updates");
    }

    /// <summary>
    /// Get the platform identifier for the current architecture
    /// </summary>
    public static string GetPlatformIdentifier()
    {
        var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"win-{arch}";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return $"osx-{arch}";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return $"linux-{arch}";
        else
            throw new PlatformNotSupportedException($"Platform {RuntimeInformation.OSDescription} is not supported for auto-updates");
    }

    /// <summary>
    /// Get the complete platform info for package identification
    /// </summary>
    public static (string Identifier, string Extension) GetPlatformInfo()
    {
        return (GetPlatformIdentifier(), GetPackageExtension());
    }

    /// <summary>
    /// Check if the current platform supports silent installation
    /// </summary>
    public static bool SupportsSilentInstall()
    {
        // Windows MSI and Linux DEB support silent installation
        // macOS PKG requires user interaction for security
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || 
               RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    }

    /// <summary>
    /// Get platform-specific installation notes for user display
    /// </summary>
    public static string GetInstallationNotes()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Update will be installed silently in the background.";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "You will be prompted for administrator password to install the update.";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "Update will be installed using the system package manager.";
        else
            return "Platform-specific installation method will be used.";
    }

    /// <summary>
    /// Get the current platform name for display purposes
    /// </summary>
    public static string GetPlatformDisplayName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Windows";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "macOS";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "Linux";
        else
            return "Unknown Platform";
    }
}