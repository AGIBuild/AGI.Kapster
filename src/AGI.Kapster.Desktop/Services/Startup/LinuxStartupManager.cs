using System;
using System.IO;
using System.Threading.Tasks;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Startup;

/// <summary>
/// Linux startup manager using XDG autostart
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
public class LinuxStartupManager : IStartupManager
{
    private const string DesktopFileName = "agi-kapster.desktop";
    private string AutostartPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "autostart", DesktopFileName);

    public bool IsSupported => OperatingSystem.IsLinux();

    public Task<bool> SetStartupAsync(bool enabled)
    {
        if (!IsSupported)
        {
            Log.Warning("Linux startup manager is only supported on Linux platform");
            return Task.FromResult(false);
        }

        try
        {
            if (enabled)
            {
                // Create autostart desktop entry
                return Task.FromResult(CreateDesktopEntry());
            }
            else
            {
                // Remove autostart desktop entry
                return Task.FromResult(RemoveDesktopEntry());
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set Linux startup: {Enabled}", enabled);
            return Task.FromResult(false);
        }
    }

    public Task<bool> IsStartupEnabledAsync()
    {
        if (!IsSupported)
        {
            return Task.FromResult(false);
        }

        try
        {
            var isEnabled = File.Exists(AutostartPath);
            Log.Debug("Linux startup status checked: {Enabled}", isEnabled);
            return Task.FromResult(isEnabled);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to check Linux startup status");
            return Task.FromResult(false);
        }
    }

    private bool CreateDesktopEntry()
    {
        try
        {
            // Get application executable path
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
            {
                Log.Error("Could not determine application executable path");
                return false;
            }

            // Ensure autostart directory exists
            var directory = Path.GetDirectoryName(AutostartPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Create desktop entry content
            var desktopEntry = $@"[Desktop Entry]
Type=Application
Version=1.0
Name=AGI Kapster
Comment=Screenshot and Annotation Tool
Exec={exePath} --minimized
Icon=agi-kapster
Terminal=false
Categories=Utility;Graphics;
StartupNotify=false
X-GNOME-Autostart-enabled=true
";

            // Write desktop entry file
            File.WriteAllText(AutostartPath, desktopEntry);
            Log.Debug("Created Linux autostart desktop entry at: {Path}", AutostartPath);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create Linux desktop entry");
            return false;
        }
    }

    private bool RemoveDesktopEntry()
    {
        try
        {
            if (File.Exists(AutostartPath))
            {
                File.Delete(AutostartPath);
                Log.Debug("Removed Linux autostart desktop entry from: {Path}", AutostartPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to remove Linux desktop entry");
            return false;
        }
    }
}

