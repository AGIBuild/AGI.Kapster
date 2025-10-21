using AGI.Kapster.Desktop.Models;
using System;
using System.Threading.Tasks;

namespace AGI.Kapster.Desktop.Services.Settings;

/// <summary>
/// Event arguments for settings change notifications
/// </summary>
public class SettingsChangedEventArgs : EventArgs
{
    public AppSettings OldSettings { get; }
    public AppSettings NewSettings { get; }

    /// <summary>
    /// Sections that changed (e.g., "Hotkeys", "AutoUpdate", "Annotations")
    /// Empty means all settings might have changed
    /// </summary>
    public string[] ChangedSections { get; }

    public SettingsChangedEventArgs(AppSettings oldSettings, AppSettings newSettings, params string[] changedSections)
    {
        OldSettings = oldSettings;
        NewSettings = newSettings;
        ChangedSections = changedSections;
    }
}

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
    /// Event raised when settings are changed
    /// </summary>
    event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

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
