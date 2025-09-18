using System;
using Avalonia.Media;
using System.Text.Json.Serialization;

namespace AGI.Captor.Desktop.Models;

/// <summary>
/// Application settings model for persistence
/// </summary>
public class AppSettings
{
    /// <summary>
    /// General application settings
    /// </summary>
    public GeneralSettings General { get; set; } = new();
    
    /// <summary>
    /// Hotkey configuration
    /// </summary>
    public HotkeySettings Hotkeys { get; set; } = new();
    
    /// <summary>
    /// Default annotation styles
    /// </summary>
    public DefaultStyleSettings DefaultStyles { get; set; } = new();
    
    /// <summary>
    /// Auto-update settings
    /// </summary>
    public AutoUpdateSettings? AutoUpdate { get; set; } = new();
}

/// <summary>
/// General application settings
/// </summary>
public class GeneralSettings
{
    /// <summary>
    /// Start with Windows
    /// </summary>
    public bool StartWithWindows { get; set; } = true;
    
    /// <summary>
    /// Minimize to system tray
    /// </summary>
    public bool MinimizeToTray { get; set; } = true;
    
    /// <summary>
    /// Show notifications
    /// </summary>
    public bool ShowNotifications { get; set; } = true;
    
    /// <summary>
    /// Default save format
    /// </summary>
    public string DefaultSaveFormat { get; set; } = "PNG";
    
    /// <summary>
    /// Auto copy to clipboard after capture
    /// </summary>
    public bool AutoCopyToClipboard { get; set; } = false;
    
    /// <summary>
    /// Play sound when capturing
    /// </summary>
    public bool PlaySoundOnCapture { get; set; } = true;
    
}

/// <summary>
/// Hotkey configuration settings
/// </summary>
public class HotkeySettings
{
    /// <summary>
    /// Capture region hotkey
    /// </summary>
    public string CaptureRegion { get; set; } = "Alt+A";
    
    /// <summary>
    /// Open settings hotkey
    /// </summary>
    public string OpenSettings { get; set; } = "Alt+S";
}

/// <summary>
/// Default annotation style settings
/// </summary>
public class DefaultStyleSettings
{
    /// <summary>
    /// Text annotation settings
    /// </summary>
    public TextStyleSettings Text { get; set; } = new();
    
    /// <summary>
    /// Shape annotation settings
    /// </summary>
    public ShapeStyleSettings Shape { get; set; } = new();
    
    /// <summary>
    /// Export quality settings
    /// </summary>
    public ExportQualitySettings Export { get; set; } = new();
    
    /// <summary>
    /// Advanced settings
    /// </summary>
    public AdvancedSettings Advanced { get; set; } = new();
}

/// <summary>
/// Text annotation style settings
/// </summary>
public class TextStyleSettings
{
    /// <summary>
    /// Font size
    /// </summary>
    public int FontSize { get; set; } = 16;
    
    /// <summary>
    /// Font family name
    /// </summary>
    public string FontFamily { get; set; } = "Segoe UI";
    
    /// <summary>
    /// Text color (ARGB format)
    /// </summary>
    [JsonIgnore]
    public Color Color { get; set; } = Colors.Black;
    
    /// <summary>
    /// Text color for JSON serialization
    /// </summary>
    public string ColorHex
    {
        get => $"#{Color.A:X2}{Color.R:X2}{Color.G:X2}{Color.B:X2}";
        set
        {
            if (string.IsNullOrEmpty(value)) return;
            try
            {
                Color = Color.Parse(value);
            }
            catch
            {
                Color = Colors.Black;
            }
        }
    }
    
    /// <summary>
    /// Font weight
    /// </summary>
    public string FontWeight { get; set; } = "Normal";
    
    /// <summary>
    /// Font style
    /// </summary>
    public string FontStyle { get; set; } = "Normal";
}

/// <summary>
/// Shape annotation style settings
/// </summary>
public class ShapeStyleSettings
{
    /// <summary>
    /// Stroke thickness
    /// </summary>
    public double StrokeThickness { get; set; } = 2;
    
    /// <summary>
    /// Stroke color (ARGB format)
    /// </summary>
    [JsonIgnore]
    public Color StrokeColor { get; set; } = Colors.Red;
    
    /// <summary>
    /// Stroke color for JSON serialization
    /// </summary>
    public string StrokeColorHex
    {
        get => $"#{StrokeColor.A:X2}{StrokeColor.R:X2}{StrokeColor.G:X2}{StrokeColor.B:X2}";
        set
        {
            if (string.IsNullOrEmpty(value)) return;
            try
            {
                StrokeColor = Color.Parse(value);
            }
            catch
            {
                StrokeColor = Colors.Red;
            }
        }
    }
    
    /// <summary>
    /// Fill mode
    /// </summary>
    public string FillMode { get; set; } = "None";
    
    /// <summary>
    /// Fill color (ARGB format)
    /// </summary>
    [JsonIgnore]
    public Color FillColor { get; set; } = Colors.Transparent;
    
    /// <summary>
    /// Fill color for JSON serialization
    /// </summary>
    public string FillColorHex
    {
        get => $"#{FillColor.A:X2}{FillColor.R:X2}{FillColor.G:X2}{FillColor.B:X2}";
        set
        {
            if (string.IsNullOrEmpty(value)) return;
            try
            {
                FillColor = Color.Parse(value);
            }
            catch
            {
                FillColor = Colors.Transparent;
            }
        }
    }
}

/// <summary>
/// Export quality settings
/// </summary>
public class ExportQualitySettings
{
    /// <summary>
    /// JPEG quality (10-100)
    /// </summary>
    public double JpegQuality { get; set; } = 90;
    
    /// <summary>
    /// PNG compression level (0-9)
    /// </summary>
    public double PngCompression { get; set; } = 6;
}

/// <summary>
/// Advanced application settings
/// </summary>
public class AdvancedSettings
{
    /// <summary>
    /// Performance settings
    /// </summary>
    public PerformanceSettings Performance { get; set; } = new();
    
    
    /// <summary>
    /// Security and privacy settings
    /// </summary>
    public SecuritySettings Security { get; set; } = new();
    
}

/// <summary>
/// Performance-related settings
/// </summary>
public class PerformanceSettings
{
    /// <summary>
    /// Enable hardware acceleration for rendering
    /// </summary>
    public bool EnableHardwareAcceleration { get; set; } = true;
    
    /// <summary>
    /// Limit frame rate during annotation drawing
    /// </summary>
    public bool LimitFrameRate { get; set; } = true;
    
    /// <summary>
    /// Render quality level (Low, Medium, High)
    /// </summary>
    public string RenderQuality { get; set; } = "Medium";
}


/// <summary>
/// Security and privacy settings
/// </summary>
public class SecuritySettings
{
    /// <summary>
    /// Allow anonymous usage data collection
    /// </summary>
    public bool AllowTelemetry { get; set; } = false;
}

/// <summary>
/// Auto-update settings
/// </summary>
public class AutoUpdateSettings
{
    /// <summary>
    /// Whether automatic updates are enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Frequency of update checks in hours
    /// </summary>
    public int CheckFrequencyHours { get; set; } = 24;

    /// <summary>
    /// Whether to install updates automatically
    /// </summary>
    public bool InstallAutomatically { get; set; } = true;

    /// <summary>
    /// Whether to notify user before installing
    /// </summary>
    public bool NotifyBeforeInstall { get; set; } = false;

    /// <summary>
    /// Whether to include pre-release versions
    /// </summary>
    public bool UsePreReleases { get; set; } = false;

    /// <summary>
    /// Last successful update check time
    /// </summary>
    public DateTime LastCheckTime { get; set; } = DateTime.MinValue;
}

