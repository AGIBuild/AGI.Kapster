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
using Polly;

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
    private readonly IFileSystemService _fileSystemService;
    private readonly Timer _updateTimer;
    private readonly ILogger _logger = Log.ForContext<UpdateService>();
    private readonly SemaphoreSlim _downloadSemaphore = new(1, 1);
    private readonly TimeSpan _retryInterval;
    private readonly AsyncPolicy<bool> _downloadRetryPolicy;
    private readonly bool _ownsHttpClient;
 
    private UpdateSettings _settings;
    private bool _isCheckingForUpdates;
    private bool _disposed;
    private bool _backgroundCheckingStarted;
    protected string? _pendingInstallerPath;

    public string? PendingInstallerPath => _pendingInstallerPath is not null && _fileSystemService.FileExists(_pendingInstallerPath) ? _pendingInstallerPath : null;

    public void ClearPendingInstaller() => _pendingInstallerPath = null;

    public UpdateService(ISettingsService settingsService, TimeSpan? retryInterval = null, HttpClient? httpClient = null, IFileSystemService? fileSystemService = null)
    {
        _settingsService = settingsService;

        _settings = LoadUpdateSettings();

        var owner = string.IsNullOrWhiteSpace(_settings.RepositoryOwner) ? DefaultRepositoryOwner : _settings.RepositoryOwner;
        var name = string.IsNullOrWhiteSpace(_settings.RepositoryName) ? DefaultRepositoryName : _settings.RepositoryName;

        _updateProvider = new GitHubUpdateProvider($"{owner}/{name}");
        _httpClient = httpClient ?? new HttpClient();
        _fileSystemService = fileSystemService ?? new FileSystemService();
        _ownsHttpClient = httpClient is null;

        _updateTimer = new Timer
        {
            AutoReset = true,
            Interval = TimeSpan.FromHours(BackgroundCheckIntervalHours).TotalMilliseconds
        };
        _updateTimer.Elapsed += OnTimerElapsed;

        _retryInterval = retryInterval ?? TimeSpan.FromMinutes(15);

        _downloadRetryPolicy = Policy
            .HandleResult<bool>(success => !success)
            .WaitAndRetryAsync(1,
                _ => _retryInterval,
                (outcome, delay, attempt, _) =>
                    _logger.Warning("Retrying update download in {Delay} (attempt {Attempt})", delay, attempt + 1));

        _logger.Information("Update service initialized. Auto-update enabled: {Enabled}", IsAutoUpdateEnabled);
    }

    public bool IsAutoUpdateEnabled =>
        !IsDebugMode() && _settings.Enabled;

    public bool IsBackgroundCheckingActive => _backgroundCheckingStarted;

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
            return await _downloadRetryPolicy.ExecuteAsync(() => TryDownloadAsync(updateInfo, progress)).ConfigureAwait(false);
        }
        finally
        {
            _downloadSemaphore.Release();
        }
    }

    protected virtual async Task<bool> TryDownloadAsync(UpdateInfo updateInfo, IProgress<DownloadProgress>? progress)
    {
        try
        {
            var filePath = GetInstallerPath(updateInfo.Version);
            var tempDir = Path.GetDirectoryName(filePath)!;
            _fileSystemService.CreateDirectory(tempDir);

            filePath = await _fileSystemService.EnsureWritablePathAsync(filePath).ConfigureAwait(false);

            _logger.Information("Downloading update from {Url} to {Path}", updateInfo.DownloadUrl, filePath);

            using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning("Download failed with status {StatusCode}", response.StatusCode);
                return false;
            }

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            using var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var fileStream = _fileSystemService.CreateFileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);

            var buffer = new byte[8192];
            var totalDownloaded = 0L;
            var stopwatch = Stopwatch.StartNew();

            while (true)
            {
                var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (bytesRead == 0) break;

                await fileStream.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
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

            // Ensure all data is written to the file before checking its size
            await fileStream.FlushAsync().ConfigureAwait(false);

            _logger.Information("Download completed: {Path} ({Size:N0} bytes)", filePath, totalDownloaded);

            var fileInfo = _fileSystemService.GetFileInfo(filePath);
            if (updateInfo.FileSize > 0 && fileInfo.Length != updateInfo.FileSize)
            {
                _logger.Warning("Downloaded file size mismatch. Expected: {Expected}, Actual: {Actual}",
                    updateInfo.FileSize, fileInfo.Length);
                _fileSystemService.DeleteFile(filePath);
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
            if (_pendingInstallerPath != null && !_fileSystemService.FileExists(_pendingInstallerPath))
            {
                _pendingInstallerPath = null;
            }
        }
    }


    private static bool IsSharingViolation(IOException ex)
        => ex.HResult == unchecked((int)0x80070020);

    public async Task<bool> InstallUpdateAsync(string installerPath)
    {
        try
        {
            if (!_fileSystemService.FileExists(installerPath))
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
            if (_pendingInstallerPath != null && !_fileSystemService.FileExists(_pendingInstallerPath))
            {
                _pendingInstallerPath = null;
            }
        }
    }

    public void StartBackgroundChecking()
    {
        if (_backgroundCheckingStarted)
        {
            _logger.Debug("Background checking already started, skipping");
            return;
        }

        if (!IsAutoUpdateEnabled)
        {
            _logger.Debug("Background checking disabled in current environment");
            return;
        }

        _logger.Information("Starting background update checking");
        _backgroundCheckingStarted = true;
        _updateTimer.Start();

        // Perform initial check after 5 seconds with proper error handling
        ScheduleDelayedUpdateCheck("Background update service started");
    }

    public void StopBackgroundChecking()
    {
        if (!_backgroundCheckingStarted)
        {
            _logger.Debug("Background checking not started, skipping stop");
            return;
        }

        _logger.Information("Stopping background update checking");
        _backgroundCheckingStarted = false;
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
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
        _downloadSemaphore.Dispose();

        _disposed = true;
        _logger.Debug("Update service disposed");
    }
}