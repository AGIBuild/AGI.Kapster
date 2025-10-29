using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Update.Platforms;

/// <summary>
/// macOS-specific update installer implementation
/// </summary>
public class MacOSUpdateInstaller : IMacOSUpdateInstaller
{
    private readonly ILogger _logger = Log.ForContext<MacOSUpdateInstaller>();

    public bool CanInstallUpdates()
    {
        try
        {
            // Check if we can write to /Applications directory (for app bundle replacement)
            var applicationsDir = "/Applications";
            var testFile = Path.Combine(applicationsDir, $".test_{Guid.NewGuid()}");

            try
            {
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                // If we can't write to /Applications, check if user is admin
                return IsUserAdmin();
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error checking update installation permissions");
            return false;
        }
    }

    public async Task<bool> InstallUpdateAsync(string packagePath)
    {
        try
        {
            if (!File.Exists(packagePath))
            {
                _logger.Error("Package file not found: {Path}", packagePath);
                return false;
            }

            var extension = Path.GetExtension(packagePath).ToLowerInvariant();

            return extension switch
            {
                ".pkg" => await InstallPkgAsync(packagePath),
                ".dmg" => await InstallDmgAsync(packagePath),
                _ => throw new NotSupportedException($"Unsupported package format: {extension}")
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error installing macOS update from {Path}", packagePath);
            return false;
        }
    }

    private async Task<bool> InstallPkgAsync(string pkgPath)
    {
        try
        {
            _logger.Information("Installing PKG package: {Path}", pkgPath);

            // Method 1: Try installer command (may prompt for password)
            var startInfo = new ProcessStartInfo
            {
                FileName = "installer",
                Arguments = $"-pkg \"{pkgPath}\" -target /",
                UseShellExecute = true, // This allows system to handle password prompts
                CreateNoWindow = false
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.Error("Failed to start installer process");
                return false;
            }

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                _logger.Information("PKG installation completed successfully");
                
                // Launch the updated application after successful installation
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000); // Wait 1 second for installation to complete
                    try
                    {
                        var appPath = "/Applications/AGI Kapster.app";
                        if (File.Exists(appPath))
                        {
                            Process.Start("open", appPath);
                            _logger.Information("Launched updated application: {AppPath}", appPath);
                        }
                        else
                        {
                            _logger.Warning("Updated application not found at expected path: {AppPath}", appPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to launch updated application");
                    }
                });
                
                return true;
            }
            else
            {
                _logger.Warning("PKG installation failed with exit code: {ExitCode}", process.ExitCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error installing PKG package");
            return false;
        }
    }

    private async Task<bool> InstallDmgAsync(string dmgPath)
    {
        try
        {
            _logger.Information("Installing DMG package: {Path}", dmgPath);

            // Mount the DMG
            var mountPoint = await MountDmgAsync(dmgPath);
            if (string.IsNullOrEmpty(mountPoint))
            {
                return false;
            }

            try
            {
                // Find the .app bundle in the mounted DMG
                var appBundles = Directory.GetDirectories(mountPoint, "*.app");
                if (appBundles.Length == 0)
                {
                    _logger.Error("No .app bundle found in DMG");
                    return false;
                }

                var sourceApp = appBundles[0];
                var appName = Path.GetFileName(sourceApp);
                var targetApp = Path.Combine("/Applications", appName);

                // Replace the application
                if (Directory.Exists(targetApp))
                {
                    Directory.Delete(targetApp, true);
                }

                await CopyDirectoryAsync(sourceApp, targetApp);

                _logger.Information("Application replaced successfully: {TargetApp}", targetApp);
                
                // Launch the updated application after successful installation
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000); // Wait 1 second for installation to complete
                    try
                    {
                        Process.Start("open", targetApp);
                        _logger.Information("Launched updated application: {AppPath}", targetApp);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to launch updated application: {AppPath}", targetApp);
                    }
                }).ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        _logger.Error(t.Exception, "Unhandled exception in background application launch task.");
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
                
                return true;
            }
            finally
            {
                // Unmount the DMG
                await UnmountDmgAsync(mountPoint);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error installing DMG package");
            return false;
        }
    }

    private async Task<string?> MountDmgAsync(string dmgPath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "hdiutil",
                Arguments = $"attach \"{dmgPath}\" -nobrowse -plist",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0) return null;

            // Parse plist output to find mount point using robust XML parsing with XPath
            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(output);

                // Use XPath to find the <key> node with value "mount-point" and its following <string> sibling
                var mountPointNode = xmlDoc.SelectSingleNode("//dict/key[.='mount-point']/following-sibling::string[1]");
                if (mountPointNode != null)
                {
                    var mountPoint = mountPointNode.InnerText;
                    if (!string.IsNullOrEmpty(mountPoint) && mountPoint.StartsWith("/Volumes/"))
                    {
                        return mountPoint;
                    }
                }
            }
            catch (XmlException xmlEx)
            {
                _logger.Warning(xmlEx, "Failed to parse plist XML output, falling back to string parsing");

                // Fallback to string parsing if XML parsing fails
                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("/Volumes/"))
                    {
                        var start = line.IndexOf("/Volumes/");
                        var end = line.IndexOf("</string>", start);
                        if (start >= 0 && end > start)
                        {
                            return line.Substring(start, end - start);
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error mounting DMG");
            return null;
        }
    }

    private async Task UnmountDmgAsync(string mountPoint)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "hdiutil",
                Arguments = $"detach \"{mountPoint}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error unmounting DMG at {MountPoint}", mountPoint);
        }
    }

    private static async Task CopyDirectoryAsync(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        // Copy all files
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var targetFile = Path.Combine(targetDir, fileName);
            File.Copy(file, targetFile, true);
        }

        // Copy all subdirectories
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            var targetSubDir = Path.Combine(targetDir, dirName);
            await CopyDirectoryAsync(dir, targetSubDir);
        }
    }

    private static bool IsUserAdmin()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "groups",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output.Contains("admin");
        }
        catch
        {
            return false;
        }
    }
}