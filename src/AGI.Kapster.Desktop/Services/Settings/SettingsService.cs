using AGI.Kapster.Desktop.Models;
using Serilog;
using System;
using System.IO;
using System.Text.Json;
using AGI.Kapster.Desktop.Services.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace AGI.Kapster.Desktop.Services.Settings;

/// <summary>
/// Settings service implementation with JSON file persistence (Singleton pattern)
/// Implements 3-tier configuration loading:
/// 1. Default settings (hardcoded)
/// 2. appsettings.json (application directory)
/// 3. settings.json (user data directory)
/// Later configurations override earlier ones
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly IFileSystemService _fileSystemService;
    private readonly IConfiguration? _configuration;
    private AppSettings _settings;
    private readonly object _saveLock = new object();

    private static readonly JsonSerializerOptions JsonOptions = AppJsonContext.Default.Options;

    public AppSettings Settings => _settings;

    // Remove parameterless constructor - force DI usage
    public SettingsService(IFileSystemService fileSystemService, IConfiguration? configuration = null)
    {
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        _configuration = configuration;

        var appFolder = _fileSystemService.GetApplicationDataPath();

        // Ensure application folder exists
        _fileSystemService.EnsureDirectoryExists(appFolder);

        _settingsFilePath = Path.Combine(appFolder, "settings.json");

        // Load settings immediately in constructor using 3-tier approach
        _settings = LoadSettingsWithThreeTierApproach();

        Log.Debug("SettingsService initialized as singleton with settings file: {FilePath}", _settingsFilePath);
    }

    /// <summary>
    /// Load settings using 3-tier approach:
    /// 1. Start with default settings
    /// 2. Merge with appsettings.json from application directory
    /// 3. Merge with settings.json from user data directory (if exists)
    /// </summary>
    private AppSettings LoadSettingsWithThreeTierApproach()
    {
        try
        {
            // Tier 1: Default settings (hardcoded)
            var settings = CreateDefaultSettings();
            Log.Debug("Tier 1: Default settings loaded");

            // Tier 2: Merge with appsettings.json from IConfiguration
            if (_configuration != null)
            {
                MergeWithConfiguration(settings, _configuration);
                Log.Debug("Tier 2: Configuration from appsettings.json merged");
            }

            // Tier 3: Merge with user settings.json (user data directory)
            if (_fileSystemService.FileExists(_settingsFilePath))
            {
                try
                {
                    var json = _fileSystemService.ReadAllText(_settingsFilePath);
                    var userSettings = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings);

                    if (userSettings != null)
                    {
                        MergeSettings(settings, userSettings);
                        Log.Debug("Tier 3: User settings from {FilePath} merged", _settingsFilePath);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to load user settings from {FilePath}, skipping tier 3", _settingsFilePath);
                }
            }
            else
            {
                Log.Debug("Tier 3: User settings file not found at {FilePath}, using tier 1+2", _settingsFilePath);
                // Save initial settings to user directory for future use
                SaveSettingsSync(settings);
            }

            return settings;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load settings with 3-tier approach, using defaults only");
            return CreateDefaultSettings();
        }
    }

    /// <summary>
    /// Merge configuration values into settings (tier 2)
    /// </summary>
    private void MergeWithConfiguration(AppSettings settings, IConfiguration configuration)
    {
        // Merge AutoUpdate settings from configuration
        var autoUpdateSection = configuration.GetSection("AutoUpdate");
        if (autoUpdateSection.Exists())
        {
            settings.AutoUpdate ??= new AutoUpdateSettings();
            settings.AutoUpdate.Enabled = autoUpdateSection.GetValue("Enabled", settings.AutoUpdate.Enabled);
            settings.AutoUpdate.NotifyBeforeInstall = autoUpdateSection.GetValue("NotifyBeforeInstall", settings.AutoUpdate.NotifyBeforeInstall);
            settings.AutoUpdate.UsePreReleases = autoUpdateSection.GetValue("UsePreReleases", settings.AutoUpdate.UsePreReleases);
            settings.AutoUpdate.RepositoryOwner = autoUpdateSection.GetValue("RepositoryOwner", settings.AutoUpdate.RepositoryOwner) ?? "AGIBuild";
            settings.AutoUpdate.RepositoryName = autoUpdateSection.GetValue("RepositoryName", settings.AutoUpdate.RepositoryName) ?? "AGI.Kapster";
        }

        // Can add more configuration sections here as needed
    }

    /// <summary>
    /// Merge user settings into base settings (tier 3 overrides tier 1+2)
    /// Note: AutoUpdate only merges Enabled field, other fields come from appsettings.json
    /// </summary>
    private void MergeSettings(AppSettings baseSettings, AppSettings userSettings)
    {
        // General settings
        if (userSettings.General != null)
        {
            baseSettings.General = userSettings.General;
        }

        // Hotkey settings
        if (userSettings.Hotkeys != null)
        {
            baseSettings.Hotkeys = userSettings.Hotkeys;
        }

        // Default styles
        if (userSettings.DefaultStyles != null)
        {
            baseSettings.DefaultStyles = userSettings.DefaultStyles;
        }

        // AutoUpdate settings - only merge Enabled field
        // Other fields (Repository, NotifyBeforeInstall, etc.) always come from appsettings.json
        if (userSettings.AutoUpdate != null && baseSettings.AutoUpdate != null)
        {
            baseSettings.AutoUpdate.Enabled = userSettings.AutoUpdate.Enabled;
            Log.Debug("AutoUpdate.Enabled merged from user settings: {Enabled}", userSettings.AutoUpdate.Enabled);
        }
    }

    public async Task SaveAsync()
    {
        string json;
        
        // Serialize within lock (fast operation)
        lock (_saveLock)
        {
            // Create a copy for saving to user directory with limited AutoUpdate fields
            var settingsToSave = PrepareSettingsForUserSave(_settings);
            json = JsonSerializer.Serialize(settingsToSave, AppJsonContext.Default.AppSettings);
        }

        // Write outside lock (I/O operation)
        try
        {
            await _fileSystemService.WriteAllTextAsync(_settingsFilePath, json);
            Log.Debug("Settings saved successfully to {FilePath}", _settingsFilePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warning(ex, "Insufficient permissions to save settings to {FilePath}, operation skipped", _settingsFilePath);
            // Don't throw - gracefully handle permission issues
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings to {FilePath}", _settingsFilePath);
            throw;
        }
    }

    /// <summary>
    /// Prepare settings for saving to user directory
    /// Only save user-configurable fields, exclude system configuration
    /// </summary>
    private AppSettings PrepareSettingsForUserSave(AppSettings settings)
    {
        var userSettings = new AppSettings
        {
            General = settings.General,
            Hotkeys = settings.Hotkeys,
            DefaultStyles = settings.DefaultStyles
        };

        // For AutoUpdate, only save Enabled field (user preference)
        // Other fields (Repository, NotifyBeforeInstall, etc.) come from appsettings.json
        if (settings.AutoUpdate != null)
        {
            userSettings.AutoUpdate = new AutoUpdateSettings
            {
                Enabled = settings.AutoUpdate.Enabled
                // Don't save: NotifyBeforeInstall, UsePreReleases, RepositoryOwner, RepositoryName
                // These always come from appsettings.json
            };
        }

        return userSettings;
    }

    /// <summary>
    /// Synchronous save for constructor use (with lock)
    /// </summary>
    private void SaveSettingsSync(AppSettings settings)
    {
        try
        {
            string json;
            
            // Serialize within lock (fast operation)
            lock (_saveLock)
            {
                // Prepare settings for user directory (limited AutoUpdate fields)
                var settingsToSave = PrepareSettingsForUserSave(settings);
                json = JsonSerializer.Serialize(settingsToSave, AppJsonContext.Default.AppSettings);
            }
            
            // Use synchronous write for constructor
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                _fileSystemService.EnsureDirectoryExists(directory);
            }

            // Direct write outside lock (I/O operation)
            _fileSystemService.WriteAllText(_settingsFilePath, json);

            Log.Debug("Settings saved synchronously to {FilePath}", _settingsFilePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warning(ex, "Insufficient permissions to save initial settings to {FilePath}", _settingsFilePath);
            // Don't throw - gracefully handle permission issues
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save initial settings to {FilePath}", _settingsFilePath);
            // Don't throw in constructor
        }
    }

    public void ResetToDefaults()
    {
        _settings = CreateDefaultSettings();
        Log.Debug("Settings reset to defaults");
    }

    public async Task UpdateSettingsAsync(AppSettings newSettings)
    {
        if (newSettings == null)
            throw new ArgumentNullException(nameof(newSettings));

        _settings = newSettings;
        await SaveAsync();

        Log.Debug("Settings updated and saved");
    }

    public string GetSettingsFilePath()
    {
        return _settingsFilePath;
    }

    private AppSettings CreateDefaultSettings()
    {
        var defaultSettings = new AppSettings();

        // Load AutoUpdate defaults from configuration if available
        if (_configuration != null)
        {
            var autoUpdateSection = _configuration.GetSection("AutoUpdate");
            if (autoUpdateSection.Exists())
            {
                defaultSettings.AutoUpdate = new AutoUpdateSettings
                {
                    Enabled = autoUpdateSection.GetValue<bool>("Enabled", true),
                    NotifyBeforeInstall = autoUpdateSection.GetValue<bool>("NotifyBeforeInstall", true),
                    UsePreReleases = autoUpdateSection.GetValue<bool>("UsePreReleases", false),
                    RepositoryOwner = autoUpdateSection.GetValue<string>("RepositoryOwner") ?? "AGIBuild",
                    RepositoryName = autoUpdateSection.GetValue<string>("RepositoryName") ?? "AGI.Kapster"
                };
            }
        }

        return defaultSettings;
    }
}
