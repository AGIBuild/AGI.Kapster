using AGI.Captor.Desktop.Models;
using System;
using System.Threading.Tasks;

namespace AGI.Captor.Desktop.Services.Settings;

/// <summary>
/// Settings service interface for managing application settings
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Current application settings (loaded at construction)
    /// </summary>
    AppSettings Settings { get; }
    
    /// <summary>
    /// Save current settings to storage
    /// </summary>
    Task SaveAsync();
    
    /// <summary>
    /// Reset settings to default values
    /// </summary>
    void ResetToDefaults();
    
    /// <summary>
    /// Update settings and save automatically
    /// </summary>
    Task UpdateSettingsAsync(AppSettings newSettings);
    
    /// <summary>
    /// Get settings file path
    /// </summary>
    string GetSettingsFilePath();
}
