using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
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
    private const int BackgroundCheckIntervalHours = 12;
    private const string DefaultRepositoryOwner = "AGIBuild";
    private const string DefaultRepositoryName = "AGI.Kapster";

    private readonly GitHubUpdateProvider _updateProvider;
    private readonly ISettingsService _settingsService;
    private readonly HttpClient _httpClient;
    private readonly Timer _updateTimer;
    private readonly ILogger _logger = Log.ForContext<UpdateService>();
    private readonly SemaphoreSlim _downloadSemaphore = new(1, 1);
    private readonly TimeSpan _retryInterval = TimeSpan.FromMinutes(15);

    private UpdateSettings _settings;
    private bool _isCheckingForUpdates;
    private bool _disposed;
    private string? _pendingInstallerPath;

    public string? PendingInstallerPath => _pendingInstallerPath is not null && File.Exists(_pendingInstallerPath) ? _pendingInstallerPath : null;

    public void ClearPendingInstaller() => _pendingInstallerPath = null;

    public UpdateService(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        _settings = LoadUpdateSettings();

        var owner = string.IsNullOrWhiteSpace(_settings.RepositoryOwner) ? DefaultRepositoryOwner : _settings.RepositoryOwner;
        var name = string.IsNullOrWhiteSpace(_settings.RepositoryName) ? DefaultRepositoryName : _settings.RepositoryName;

        _updateProvider = new GitHubUpdateProvider($"{owner}/{name}");
        _httpClient = new HttpClient();

        _updateTimer = new Timer
        {
            AutoReset = true,
            Interval = TimeSpan.FromHours(BackgroundCheckIntervalHours).TotalMilliseconds
        };
        _updateTimer.Elapsed += OnTimerElapsed;

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
        return false;
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
        await _downloadSemaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            var filePath = GetInstallerPath(updateInfo.Version);
            var tempDir = Path.GetDirectoryName(filePath)!;
            Directory.CreateDirectory(tempDir);

            filePath = await EnsureWritableInstallerPathAsync(filePath).ConfigureAwait(false);

            _logger.Information("Downloading update from {Url} to {Path}", updateInfo.DownloadUrl, filePath);

            using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);

            var buffer = new byte[8192];
            var totalDownloaded = 0L;
            var stopwatch = Stopwatch.StartNew();

            while (true)
            {
                var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalDownloaded += bytesRead;

                if (progress != null && stopwatch.ElapsedMilliseconds > 100)
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

            var fileInfo = new FileInfo(filePath);
            if (updateInfo.FileSize > 0 && fileInfo.Length != updateInfo.FileSize)
            {
                _logger.Warning("Downloaded file size mismatch. Expected: {Expected}, Actual: {Actual}",
                    updateInfo.FileSize, fileInfo.Length);
                File.Delete(filePath);
                return false;
            }

            _pendingInstallerPath = filePath;
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error downloading update");
            return false;
        }
        finally
        {
            _downloadSemaphore.Release();
        }
    }

    private static bool IsSharingViolation(IOException ex)
        => ex.HResult == unchecked((int)0x80070020);

    private async Task<string> EnsureWritableInstallerPathAsync(string initialPath)
    {
        var directory = Path.GetDirectoryName(initialPath)!;
        var baseName = Path.GetFileNameWithoutExtension(initialPath);
        var extension = Path.GetExtension(initialPath);
        var candidate = initialPath;
        var attempt = 0;

        while (true)
        {
            try
            {
                if (File.Exists(candidate))
                {
                    using (File.Open(candidate, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                    {
                        File.Delete(candidate);
                    }
                }

                using (File.Open(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                }

                File.Delete(candidate);
                return candidate;
            }
            catch (IOException ioEx) when (IsSharingViolation(ioEx) || File.Exists(candidate))
            {
                attempt++;
                var suffix = attempt switch
                {
                    0 => string.Empty,
                    1 => DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"),
                    _ => Guid.NewGuid().ToString("N")
                };

                if (!string.IsNullOrEmpty(suffix))
                {
                    candidate = Path.Combine(directory, $"{baseName}-{suffix}{extension}");
                }

                await Task.Delay(Math.Min(1500, attempt * 250)).ConfigureAwait(false);
            }
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

            var processInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            };

            _logger.Information("Launching installer: {Path}", installerPath);

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                _logger.Error("Failed to launch installer process");
                return false;
            }

            _pendingInstallerPath = null;
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error launching installer");
            return false;
        }
        finally
        {
            if (_pendingInstallerPath != null && !File.Exists(_pendingInstallerPath))
            {
                _pendingInstallerPath = null;
            }
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

        _logger.Information("Update settings saved. Auto-update enabled: {Enabled}", IsAutoUpdateEnabled);
    }

    private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            var update = await CheckForUpdatesAsync();
            if (update != null)
            {
                TriggerUpdateAvailable(update);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during background update check");
        }
    }

    private void TriggerUpdateAvailable(UpdateInfo update)
    {
        UpdateAvailable?.Invoke(this, new UpdateAvailableEventArgs(update, true));
    }

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
                    TriggerUpdateAvailable(update);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during scheduled update check");
            }
        });
    }

    public void ScheduleRetryDownload(UpdateInfo updateInfo)
    {
        Task.Run(async () =>
        {
            _logger.Warning("Scheduling update download retry in {Delay} mins for version {Version}", _retryInterval.TotalMinutes, updateInfo.Version);
            await Task.Delay(_retryInterval).ConfigureAwait(false);
            TriggerUpdateAvailable(updateInfo);
        });
    }

    private Version GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetName().Version ?? new Version(1, 0, 0, 0);
    }

    private void UpdateTimerInterval()
    {
        _updateTimer.Interval = TimeSpan.FromHours(BackgroundCheckIntervalHours).TotalMilliseconds;
    }

    private UpdateSettings LoadUpdateSettings()
    {
        var settings = _settingsService.Settings;
        return new UpdateSettings
        {
            Enabled = settings.AutoUpdate?.Enabled ?? true,
            NotifyBeforeInstall = settings.AutoUpdate?.NotifyBeforeInstall ?? false,
            UsePreReleases = settings.AutoUpdate?.UsePreReleases ?? false,
            RepositoryOwner = settings.AutoUpdate?.RepositoryOwner,
            RepositoryName = settings.AutoUpdate?.RepositoryName,
            LastCheckTime = settings.AutoUpdate?.LastCheckTime ?? DateTime.MinValue
        };
    }

    private async Task SaveUpdateSettingsAsync()
    {
        try
        {
            var settings = _settingsService.Settings;
            settings.AutoUpdate = new Models.AutoUpdateSettings
            {
                Enabled = _settings.Enabled,
                NotifyBeforeInstall = _settings.NotifyBeforeInstall,
                UsePreReleases = _settings.UsePreReleases,
                RepositoryOwner = _settings.RepositoryOwner ?? DefaultRepositoryOwner,
                RepositoryName = _settings.RepositoryName ?? DefaultRepositoryName,
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
        _downloadSemaphore.Dispose();

        _disposed = true;
        _logger.Debug("Update service disposed");
    }
}