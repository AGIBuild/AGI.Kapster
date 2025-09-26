using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using AGI.Kapster.Desktop.Models.Update;
using AGI.Kapster.Desktop.Services.Settings;
using AGI.Kapster.Desktop.Services.Update.Platforms;
using Serilog;
using Timer = System.Timers.Timer;

namespace AGI.Kapster.Desktop.Services.Update;

/// <summary>
/// Automatic update service implementation
/// </summary>
public class UpdateService : IUpdateService, IDisposable
{
    private readonly GitHubUpdateProvider _updateProvider;
    private readonly ISettingsService _settingsService;
    private readonly HttpClient _httpClient;
    private readonly Timer _updateTimer;
    private readonly ILogger _logger = Log.ForContext<UpdateService>();

    private UpdateSettings _settings;
    private bool _isCheckingForUpdates = false;
    private bool _disposed = false;

    public UpdateService(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        // Load current settings first
        _settings = LoadUpdateSettings();

        // Initialize GitHub update provider with configurable repository
        _updateProvider = new GitHubUpdateProvider(_settings.GitHubRepository);
        _httpClient = new HttpClient();

        // Setup timer for periodic checks
        _updateTimer = new Timer();
        _updateTimer.Elapsed += OnTimerElapsed;
        _updateTimer.AutoReset = true;

        UpdateTimerInterval();

        _logger.Information("Update service initialized. Auto-update enabled: {Enabled}", IsAutoUpdateEnabled);
    }

    public bool IsAutoUpdateEnabled =>
        !IsDebugMode() && _settings.Enabled;

    public event EventHandler<UpdateAvailableEventArgs>? UpdateAvailable;
    public event EventHandler<UpdateCompletedEventArgs>? UpdateCompleted;
    public event EventHandler<DownloadProgress>? DownloadProgressChanged;

    /// <summary>
    /// Check if running in debug/development mode
    /// Can be overridden via settings for testing and configuration flexibility
    /// </summary>
    private bool IsDebugMode()
    {
        // Default behavior based on build configuration
#if DEBUG
        return true;
#else
        return Debugger.IsAttached;
#endif
    }

    /// <summary>
    /// Get the installer file path for a given version and platform
    /// </summary>
    /// <param name="version">Update version</param>
    /// <param name="platformInfo">Platform information, if null will get current platform</param>
    /// <returns>Full path to the installer file</returns>
    public static string GetInstallerPath(string version, (string Identifier, string Extension)? platformInfo = null)
    {
        var platform = platformInfo ?? PlatformUpdateHelper.GetPlatformInfo();
        var tempDir = Path.Combine(Path.GetTempPath(), "AGI.Kapster", "Updates");
        return Path.Combine(tempDir, $"AGI.Kapster-{version}-{platform.Identifier}.{platform.Extension}");
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        if (_isCheckingForUpdates)
        {
            _logger.Debug("Update check already in progress, skipping");
            return null;
        }

        if (!IsAutoUpdateEnabled)
        {
            _logger.Debug("Auto-update disabled in current environment");
            return null;
        }

        try
        {
            _isCheckingForUpdates = true;
            _logger.Information("Checking for updates...");

            var updateInfo = await _updateProvider.GetLatestReleaseAsync(_settings.UsePreReleases);

            if (updateInfo == null)
            {
                _logger.Information("No updates available");
                _settings.LastCheckTime = DateTime.UtcNow;
                await SaveUpdateSettingsAsync();
                return null;
            }

            var currentVersion = GetCurrentVersion();
            if (!updateInfo.IsNewerThan(currentVersion))
            {
                _logger.Information("Current version {Current} is up to date (latest: {Latest})",
                    currentVersion, updateInfo.Version);
                _settings.LastCheckTime = DateTime.UtcNow;
                await SaveUpdateSettingsAsync();
                return null;
            }

            _logger.Information("Update available: {Version} (current: {Current})",
                updateInfo.Version, currentVersion);

            _settings.LastCheckTime = DateTime.UtcNow;
            await SaveUpdateSettingsAsync();

            return updateInfo;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking for updates");
            return null;
        }
        finally
        {
            _isCheckingForUpdates = false;
        }
    }

    public async Task<bool> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<DownloadProgress>? progress = null)
    {
        try
        {
            var filePath = GetInstallerPath(updateInfo.Version);
            var tempDir = Path.GetDirectoryName(filePath)!;
            Directory.CreateDirectory(tempDir);

            _logger.Information("Downloading update from {Url} to {Path}", updateInfo.DownloadUrl, filePath);

            using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[8192];
            var totalDownloaded = 0L;
            var stopwatch = Stopwatch.StartNew();

            while (true)
            {
                var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalDownloaded += bytesRead;

                if (progress != null && stopwatch.ElapsedMilliseconds > 100) // Update every 100ms
                {
                    var downloadProgress = new DownloadProgress
                    {
                        TotalBytes = totalBytes,
                        DownloadedBytes = totalDownloaded,
                        BytesPerSecond = totalDownloaded * 1000 / Math.Max(stopwatch.ElapsedMilliseconds, 1)
                    };

                    if (downloadProgress.BytesPerSecond > 0)
                    {
                        var remainingBytes = totalBytes - totalDownloaded;
                        downloadProgress.EstimatedTimeRemaining = TimeSpan.FromSeconds(remainingBytes / downloadProgress.BytesPerSecond);
                    }

                    progress.Report(downloadProgress);
                    DownloadProgressChanged?.Invoke(this, downloadProgress);
                    stopwatch.Restart();
                }
            }

            _logger.Information("Download completed: {Path} ({Size:N0} bytes)", filePath, totalDownloaded);

            // Verify file size
            var fileInfo = new FileInfo(filePath);
            if (updateInfo.FileSize > 0 && fileInfo.Length != updateInfo.FileSize)
            {
                _logger.Warning("Downloaded file size mismatch. Expected: {Expected}, Actual: {Actual}",
                    updateInfo.FileSize, fileInfo.Length);
                File.Delete(filePath);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error downloading update");
            return false;
        }
    }

    public async Task<bool> InstallUpdateAsync(string installerPath)
    {
        try
        {
            if (!File.Exists(installerPath))
            {
                _logger.Error("Installer file not found: {Path}", installerPath);
                return false;
            }

            _logger.Information("Starting silent installation: {Path}", installerPath);

            ProcessStartInfo startInfo;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows MSI installation
                startInfo = new ProcessStartInfo
                {
                    FileName = "msiexec",
                    Arguments = $"/i \"{installerPath}\" /quiet /norestart LAUNCH_APP=0",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Use specialized macOS installer
                var macInstaller = new MacOSUpdateInstaller();
                var installResult = await macInstaller.InstallUpdateAsync(installerPath);

                if (installResult)
                {
                    _logger.Information("macOS installation completed successfully");

                    // Schedule application restart with proper error handling
                    ScheduleApplicationRestart("macOS installation completed");

                    return true;
                }
                else
                {
                    _logger.Error("macOS installation failed");
                    return false;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux DEB installation
                startInfo = new ProcessStartInfo
                {
                    FileName = "sudo",
                    Arguments = $"dpkg -i \"{installerPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
            }
            else
            {
                _logger.Error("Unsupported platform for installation: {Platform}", RuntimeInformation.OSDescription);
                return false;
            }

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.Error("Failed to start installation process");
                return false;
            }

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                _logger.Information("Installation completed successfully");

                // Schedule application restart with proper error handling
                ScheduleApplicationRestart("Installation completed successfully");

                return true;
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync();
                _logger.Error("Installation failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error installing update");
            return false;
        }
    }

    public void StartBackgroundChecking()
    {
        if (!IsAutoUpdateEnabled)
        {
            _logger.Debug("Background checking disabled in current environment");
            return;
        }

        _logger.Information("Starting background update checking");
        _updateTimer.Start();

        // Perform initial check after 5 seconds with proper error handling
        ScheduleDelayedUpdateCheck("Background update service started");
    }

    public void StopBackgroundChecking()
    {
        _logger.Information("Stopping background update checking");
        _updateTimer.Stop();
    }

    public UpdateSettings GetSettings() => _settings;

    public async Task UpdateSettingsAsync(UpdateSettings settings)
    {
        _settings = settings;
        await SaveUpdateSettingsAsync();
        UpdateTimerInterval();

        _logger.Information("Update settings saved. Auto-update enabled: {Enabled}", IsAutoUpdateEnabled);
    }

    private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            var update = await CheckForUpdatesAsync();
            if (update != null)
            {
                UpdateAvailable?.Invoke(this, new UpdateAvailableEventArgs(update, true));

                if (_settings.InstallAutomatically && !_settings.NotifyBeforeInstall && PlatformUpdateHelper.SupportsSilentInstall())
                {
                    await PerformAutomaticUpdateAsync(update);
                }
                else if (_settings.InstallAutomatically && !PlatformUpdateHelper.SupportsSilentInstall())
                {
                    // For platforms that don't support silent install (like macOS), notify user
                    _logger.Information("Automatic installation not supported on {Platform}, user notification required",
                        PlatformUpdateHelper.GetPlatformDisplayName());
                    UpdateAvailable?.Invoke(this, new UpdateAvailableEventArgs(update, false));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during background update check");
        }
    }

    private async Task PerformAutomaticUpdateAsync(UpdateInfo updateInfo)
    {
        try
        {
            _logger.Information("Performing automatic update to version {Version}", updateInfo.Version);

            var downloadSuccess = await DownloadUpdateAsync(updateInfo);
            if (!downloadSuccess)
            {
                UpdateCompleted?.Invoke(this, new UpdateCompletedEventArgs(false, updateInfo, "Download failed"));
                return;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "AGI.Kapster", "Updates");
            var platformInfo = PlatformUpdateHelper.GetPlatformInfo();
            var installerPath = Path.Combine(tempDir, $"AGI.Kapster-{updateInfo.Version}-{platformInfo.Identifier}.{platformInfo.Extension}");

            var installSuccess = await InstallUpdateAsync(installerPath);
            UpdateCompleted?.Invoke(this, new UpdateCompletedEventArgs(installSuccess, updateInfo,
                installSuccess ? null : "Installation failed"));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during automatic update");
            UpdateCompleted?.Invoke(this, new UpdateCompletedEventArgs(false, updateInfo, ex.Message));
        }
    }

    private void RestartApplication()
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            var executablePath = currentProcess.MainModule?.FileName ?? Assembly.GetEntryAssembly()?.Location;

            if (!string.IsNullOrEmpty(executablePath))
            {
                _logger.Information("Restarting application: {Path}", executablePath);
                Process.Start(new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "--updated",
                    UseShellExecute = true
                });
            }

            // Gracefully dispose resources before exiting
            _logger.Information("Performing graceful shutdown before restart");
            Dispose();

            // Give a brief moment for cleanup to complete
            Thread.Sleep(500);

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error restarting application");
            // If restart fails, at least try to dispose cleanly
            try
            {
                Dispose();
            }
            catch (Exception disposeEx)
            {
                _logger.Error(disposeEx, "Error during cleanup after failed restart");
            }
        }
    }

    /// <summary>
    /// Schedule application restart with proper error handling
    /// </summary>
    private void ScheduleApplicationRestart(string reason)
    {
        Task.Run(async () =>
        {
            try
            {
                _logger.Information("Scheduling application restart in 2 seconds. Reason: {Reason}", reason);
                await Task.Delay(2000);
                RestartApplication();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during scheduled application restart");
            }
        });
    }

    /// <summary>
    /// Schedule delayed update check with proper error handling
    /// </summary>
    private void ScheduleDelayedUpdateCheck(string reason)
    {
        Task.Run(async () =>
        {
            try
            {
                _logger.Information("Scheduling delayed update check in 5 seconds. Reason: {Reason}", reason);
                await Task.Delay(5000);

                var update = await CheckForUpdatesAsync();
                if (update != null)
                {
                    UpdateAvailable?.Invoke(this, new UpdateAvailableEventArgs(update, true));

                    if (_settings.InstallAutomatically && !_settings.NotifyBeforeInstall && PlatformUpdateHelper.SupportsSilentInstall())
                    {
                        await PerformAutomaticUpdateAsync(update);
                    }
                    else if (_settings.InstallAutomatically && !PlatformUpdateHelper.SupportsSilentInstall())
                    {
                        // For platforms that don't support silent install (like macOS), notify user
                        _logger.Information("Automatic installation not supported on {Platform}, user notification required",
                            PlatformUpdateHelper.GetPlatformDisplayName());
                        UpdateAvailable?.Invoke(this, new UpdateAvailableEventArgs(update, false));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during scheduled update check");
            }
        });
    }

    private Version GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetName().Version ?? new Version(1, 0, 0, 0);
    }

    private void UpdateTimerInterval()
    {
        _updateTimer.Interval = TimeSpan.FromHours(_settings.CheckFrequencyHours).TotalMilliseconds;
    }

    private UpdateSettings LoadUpdateSettings()
    {
        try
        {
            var settings = _settingsService.Settings;
            return new UpdateSettings
            {
                Enabled = settings.AutoUpdate?.Enabled ?? true,
                CheckFrequencyHours = settings.AutoUpdate?.CheckFrequencyHours ?? 24,
                InstallAutomatically = settings.AutoUpdate?.InstallAutomatically ?? true,
                NotifyBeforeInstall = settings.AutoUpdate?.NotifyBeforeInstall ?? false,
                UsePreReleases = settings.AutoUpdate?.UsePreReleases ?? false,
                LastCheckTime = settings.AutoUpdate?.LastCheckTime ?? DateTime.MinValue
            };
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error loading update settings, using defaults");
            return new UpdateSettings();
        }
    }

    private async Task SaveUpdateSettingsAsync()
    {
        try
        {
            var settings = _settingsService.Settings;
            settings.AutoUpdate = new Models.AutoUpdateSettings
            {
                Enabled = _settings.Enabled,
                CheckFrequencyHours = _settings.CheckFrequencyHours,
                InstallAutomatically = _settings.InstallAutomatically,
                NotifyBeforeInstall = _settings.NotifyBeforeInstall,
                UsePreReleases = _settings.UsePreReleases,
                LastCheckTime = _settings.LastCheckTime
            };

            await _settingsService.UpdateSettingsAsync(settings);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error saving update settings");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _updateTimer?.Stop();
        _updateTimer?.Dispose();
        _updateProvider?.Dispose();
        _httpClient?.Dispose();

        _disposed = true;
        _logger.Debug("Update service disposed");
    }
}