using AGI.Captor.Desktop.Models;
using Serilog;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AGI.Captor.Desktop.Services.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace AGI.Captor.Desktop.Services.Settings;

/// <summary>
/// Settings service implementation with JSON file persistence
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly IFileSystemService _fileSystemService;
    private readonly IConfiguration? _configuration;
    private AppSettings _settings;
    
    private static readonly JsonSerializerOptions JsonOptions = AppJsonContext.Default.Options;
    
    public AppSettings Settings => _settings;
    
    public SettingsService() : this(new FileSystemService(), null)
    {
    }
    
    public SettingsService(IFileSystemService fileSystemService, IConfiguration? configuration = null)
    {
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        _configuration = configuration;
        
        var appFolder = _fileSystemService.GetApplicationDataPath();
        
        // Ensure application folder exists
        _fileSystemService.EnsureDirectoryExists(appFolder);
        
        _settingsFilePath = Path.Combine(appFolder, "settings.json");
        _settings = new AppSettings();
        
        // Load settings immediately in constructor
        LoadSettings();
        
        Log.Debug("SettingsService initialized with settings file: {FilePath}", _settingsFilePath);
    }
    
    private void LoadSettings()
    {
        try
        {
            if (_fileSystemService.FileExists(_settingsFilePath))
            {
                var json = _fileSystemService.ReadAllText(_settingsFilePath);
                var loadedSettings = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings);
                
                if (loadedSettings != null)
                {
                    _settings = loadedSettings;
                    // Merge with configuration defaults if settings don't have AutoUpdate configured
                    MergeWithConfigurationDefaults();
                    Log.Debug("Settings loaded successfully from {FilePath}", _settingsFilePath);
                }
                else
                {
                    Log.Warning("Failed to deserialize settings, using defaults");
                    _settings = CreateDefaultSettings();
                }
            }
            else
            {
                Log.Debug("Settings file not found, using default settings");
                _settings = CreateDefaultSettings();
                // Save default settings synchronously
                SaveAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load settings from {FilePath}, using defaults", _settingsFilePath);
            _settings = CreateDefaultSettings();
        }
    }
    
    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, AppJsonContext.Default.AppSettings);
            
            await _fileSystemService.WriteAllTextAsync(_settingsFilePath, json);
            
            Log.Information("Settings saved successfully to {FilePath}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings to {FilePath}", _settingsFilePath);
            throw;
        }
    }
    
    public void ResetToDefaults()
    {
        _settings = new AppSettings();
        Log.Information("Settings reset to defaults");
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
                    CheckFrequencyHours = autoUpdateSection.GetValue<int>("CheckFrequencyHours", 24),
                    InstallAutomatically = autoUpdateSection.GetValue<bool>("InstallAutomatically", true),
                    NotifyBeforeInstall = autoUpdateSection.GetValue<bool>("NotifyBeforeInstall", false),
                    UsePreReleases = autoUpdateSection.GetValue<bool>("UsePreReleases", false),
                    LastCheckTime = DateTime.MinValue
                };
            }
        }
        
        return defaultSettings;
    }

    private void MergeWithConfigurationDefaults()
    {
        if (_configuration == null) return;
        
        // If AutoUpdate settings don't exist in user settings, use configuration defaults
        if (_settings.AutoUpdate == null)
        {
            var autoUpdateSection = _configuration.GetSection("AutoUpdate");
            if (autoUpdateSection.Exists())
            {
                _settings.AutoUpdate = new AutoUpdateSettings
                {
                    Enabled = autoUpdateSection.GetValue<bool>("Enabled", true),
                    CheckFrequencyHours = autoUpdateSection.GetValue<int>("CheckFrequencyHours", 24),
                    InstallAutomatically = autoUpdateSection.GetValue<bool>("InstallAutomatically", true),
                    NotifyBeforeInstall = autoUpdateSection.GetValue<bool>("NotifyBeforeInstall", false),
                    UsePreReleases = autoUpdateSection.GetValue<bool>("UsePreReleases", false),
                    LastCheckTime = DateTime.MinValue
                };
            }
        }
    }
}
