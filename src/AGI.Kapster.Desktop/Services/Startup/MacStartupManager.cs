using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Startup;

/// <summary>
/// macOS startup manager using LaunchAgent
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("macos")]
public class MacStartupManager : IStartupManager
{
    private const string LaunchAgentLabel = "com.agibuild.kapster";
    private const int LaunchCtlTimeoutMs = 5000;
    
    private string LaunchAgentPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", $"{LaunchAgentLabel}.plist");

    public bool IsSupported => OperatingSystem.IsMacOS();

    public Task<bool> SetStartupAsync(bool enabled)
    {
        if (!IsSupported)
        {
            Log.Warning("macOS startup manager is only supported on macOS platform");
            return Task.FromResult(false);
        }

        try
        {
            if (enabled)
            {
                // Create LaunchAgent plist file
                return Task.FromResult(CreateLaunchAgent());
            }
            else
            {
                // Remove LaunchAgent plist file
                return Task.FromResult(RemoveLaunchAgent());
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set macOS startup: {Enabled}", enabled);
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
            var isEnabled = File.Exists(LaunchAgentPath);
            Log.Debug("macOS startup status checked: {Enabled}", isEnabled);
            return Task.FromResult(isEnabled);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to check macOS startup status");
            return Task.FromResult(false);
        }
    }

    private bool CreateLaunchAgent()
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

            // Ensure LaunchAgents directory exists
            var directory = Path.GetDirectoryName(LaunchAgentPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Create plist XML
            var plist = new XDocument(
                new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
                new XElement("plist",
                    new XAttribute("version", "1.0"),
                    new XElement("dict",
                        new XElement("key", "Label"),
                        new XElement("string", LaunchAgentLabel),
                        new XElement("key", "ProgramArguments"),
                        new XElement("array",
                            new XElement("string", exePath),
                            new XElement("string", "--minimized")
                        ),
                        new XElement("key", "RunAtLoad"),
                        new XElement("true"),
                        new XElement("key", "KeepAlive"),
                        new XElement("false"),
                        new XElement("key", "StandardErrorPath"),
                        new XElement("string", Path.Combine(Path.GetTempPath(), "agi-kapster-error.log")),
                        new XElement("key", "StandardOutPath"),
                        new XElement("string", Path.Combine(Path.GetTempPath(), "agi-kapster-output.log"))
                    )
                )
            );

            // Save plist file
            plist.Save(LaunchAgentPath);
            Log.Debug("Created macOS LaunchAgent at: {Path}", LaunchAgentPath);

            // Load the LaunchAgent
            LoadLaunchAgent();

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create macOS LaunchAgent");
            return false;
        }
    }

    private bool RemoveLaunchAgent()
    {
        try
        {
            if (File.Exists(LaunchAgentPath))
            {
                // Unload the LaunchAgent first
                UnloadLaunchAgent();

                // Delete the plist file
                File.Delete(LaunchAgentPath);
                Log.Debug("Removed macOS LaunchAgent from: {Path}", LaunchAgentPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to remove macOS LaunchAgent");
            return false;
        }
    }

    private void LoadLaunchAgent()
    {
        try
        {
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/launchctl",
                Arguments = $"load \"{LaunchAgentPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            process?.WaitForExit(LaunchCtlTimeoutMs);
            Log.Debug("Loaded macOS LaunchAgent");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load macOS LaunchAgent (non-critical)");
        }
    }

    private void UnloadLaunchAgent()
    {
        try
        {
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/launchctl",
                Arguments = $"unload \"{LaunchAgentPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            process?.WaitForExit(LaunchCtlTimeoutMs);
            Log.Debug("Unloaded macOS LaunchAgent");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to unload macOS LaunchAgent (non-critical)");
        }
    }
}

