using AGI.Captor.Desktop.Models;
using Serilog;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AGI.Captor.Desktop.Services.Serialization;
using System.Threading.Tasks;

namespace AGI.Captor.Desktop.Services.Settings;

/// <summary>
/// Settings service implementation with JSON file persistence
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly IFileSystemService _fileSystemService;
    private AppSettings _settings;
    
    private static readonly JsonSerializerOptions JsonOptions = AppJsonContext.Default.Options;
    
    public AppSettings Settings => _settings;
    
    public SettingsService() : this(new FileSystemService())
    {
    }
    
    public SettingsService(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        
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
                    Log.Debug("Settings loaded successfully from {FilePath}", _settingsFilePath);
                }
                else
                {
                    Log.Warning("Failed to deserialize settings, using defaults");
                    _settings = new AppSettings();
                }
            }
            else
            {
                Log.Debug("Settings file not found, using default settings");
                _settings = new AppSettings();
                // Save default settings synchronously
                SaveAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load settings from {FilePath}, using defaults", _settingsFilePath);
            _settings = new AppSettings();
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
}
