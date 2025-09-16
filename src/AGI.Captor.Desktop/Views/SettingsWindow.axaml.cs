using AGI.Captor.Desktop.Models;
using AGI.Captor.Desktop.Services;
using AGI.Captor.Desktop.Services.Hotkeys;
using AGI.Captor.Desktop.Dialogs;
using Avalonia;
using Avalonia.Controls;
using static Avalonia.Controls.Design;
using Avalonia.Interactivity;
using Avalonia.Media;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AGI.Captor.Desktop.Views;

/// <summary>
/// Settings window for configuring application preferences
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly ISettingsService _settingsService;
    private readonly IApplicationController? _applicationController;
    private AppSettings _originalSettings;
    private AppSettings _currentSettings;
    public bool DialogResult { get; private set; }

    // Design-time constructor - DO NOT USE IN RUNTIME
    public SettingsWindow() : this(new DesignTimeSettingsService())
    {
        if (!Design.IsDesignMode)
        {
            throw new InvalidOperationException("Default constructor should only be used in design mode. Use SettingsWindow(ISettingsService, IApplicationController) instead.");
        }
    }

    public SettingsWindow(ISettingsService settingsService, IApplicationController? applicationController = null)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _applicationController = applicationController;
        
        InitializeComponent();
        
        // Clone settings to avoid modifying the original until user confirms
        _originalSettings = CloneSettings(_settingsService.Settings);
        _currentSettings = CloneSettings(_originalSettings);
        
        InitializeEvents();
        LoadCurrentSettings();
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    private void InitializeEvents()
    {
            
        // Dialog buttons
        if (this.FindControl<Button>("OkButton") is { } okButton)
        {
            okButton.Click += OnOkClick;
        }
        
        if (this.FindControl<Button>("CancelButton") is { } cancelButton)
        {
            cancelButton.Click += OnCancelClick;
        }
        
        if (this.FindControl<Button>("ResetButton") is { } resetButton)
        {
            resetButton.Click += OnResetClick;
        }
        
        

        // Hotkey text boxes with key capture
        if (this.FindControl<TextBox>("CaptureRegionHotkeyTextBox") is { } captureRegionText)
        {
            captureRegionText.TextChanged += OnCaptureRegionHotkeyChanged;
            captureRegionText.KeyDown += OnHotkeyTextBoxKeyDown;
            captureRegionText.GotFocus += OnHotkeyTextBoxGotFocus;
        }
        
        if (this.FindControl<TextBox>("OpenSettingsHotkeyTextBox") is { } openSettingsText)
        {
            openSettingsText.TextChanged += OnOpenSettingsHotkeyChanged;
            openSettingsText.KeyDown += OnHotkeyTextBoxKeyDown;
            openSettingsText.GotFocus += OnHotkeyTextBoxGotFocus;
        }

        // Color preview click handlers
        
        if (this.FindControl<Border>("ShapeColorPreview") is { } shapeColorPreview)
        {
            shapeColorPreview.PointerPressed += OnShapeColorClick;
        }

        // Slider value changed handlers
        if (this.FindControl<Slider>("TextFontSizeSlider") is { } textFontSizeSlider)
        {
            textFontSizeSlider.ValueChanged += OnTextFontSizeChanged;
        }
        
        if (this.FindControl<Slider>("ShapeThicknessSlider") is { } shapeThicknessSlider)
        {
            shapeThicknessSlider.ValueChanged += OnShapeThicknessChanged;
        }
        
        if (this.FindControl<Slider>("JpegQualitySlider") is { } jpegQualitySlider)
        {
            jpegQualitySlider.ValueChanged += OnJpegQualityChanged;
        }
        
        if (this.FindControl<Slider>("PngCompressionSlider") is { } pngCompressionSlider)
        {
            pngCompressionSlider.ValueChanged += OnPngCompressionChanged;
        }
        
        
        // Advanced settings button handlers
        if (this.FindControl<Button>("OpenLogsDirectoryButton") is { } openLogsButton)
        {
            openLogsButton.Click += OnOpenLogsDirectoryClick;
        }

        // Handle window closing
        Closing += (s, e) =>
        {
            if (!DialogResult)
            {
                DialogResult = false;
            }
        };
    }

    private void LoadCurrentSettings()
    {
        try
        {
            // General settings
            if (this.FindControl<CheckBox>("StartWithSystemCheckBox") is { } startWithSystem)
            {
                startWithSystem.IsChecked = _currentSettings.General.StartWithWindows;
            }
            
            
            
            // Default format
            if (this.FindControl<ComboBox>("DefaultFormatComboBox") is { } formatCombo)
            {
                var formatItems = formatCombo.Items;
                for (int i = 0; i < formatItems.Count; i++)
                {
                    if (formatItems[i] is ComboBoxItem item && 
                        item.Content?.ToString() == _currentSettings.General.DefaultSaveFormat)
                    {
                        formatCombo.SelectedIndex = i;
                        break;
                    }
                }
            }
            
            // Hotkeys
            if (this.FindControl<TextBox>("CaptureRegionHotkeyTextBox") is { } captureRegionText)
            {
                captureRegionText.Text = _currentSettings.Hotkeys.CaptureRegion;
            }
            
            if (this.FindControl<TextBox>("OpenSettingsHotkeyTextBox") is { } openSettingsText)
            {
                openSettingsText.Text = _currentSettings.Hotkeys.OpenSettings;
            }
            
            // Update macOS permission status
            UpdateMacPermissionStatus();
            
            // Style settings
            if (this.FindControl<Slider>("TextFontSizeSlider") is { } textFontSize)
            {
                textFontSize.Value = _currentSettings.DefaultStyles.Text.FontSize;
                if (this.FindControl<TextBlock>("TextFontSizeValueText") is { } textFontSizeValue)
                {
                    textFontSizeValue.Text = ((int)textFontSize.Value).ToString();
                }
            }
            
            if (this.FindControl<Slider>("ShapeThicknessSlider") is { } shapeThickness)
            {
                shapeThickness.Value = _currentSettings.DefaultStyles.Shape.StrokeThickness;
                if (this.FindControl<TextBlock>("ShapeThicknessValueText") is { } shapeThicknessValue)
                {
                    shapeThicknessValue.Text = ((int)shapeThickness.Value).ToString();
                }
            }
            
            if (this.FindControl<Slider>("JpegQualitySlider") is { } jpegQuality)
            {
                jpegQuality.Value = _currentSettings.DefaultStyles.Export.JpegQuality;
                if (this.FindControl<TextBlock>("JpegQualityValueText") is { } jpegQualityValue)
                {
                    jpegQualityValue.Text = ((int)jpegQuality.Value).ToString();
                }
            }
            
            if (this.FindControl<Slider>("PngCompressionSlider") is { } pngCompression)
            {
                pngCompression.Value = _currentSettings.DefaultStyles.Export.PngCompression;
                if (this.FindControl<TextBlock>("PngCompressionValueText") is { } pngCompressionValue)
                {
                    pngCompressionValue.Text = ((int)pngCompression.Value).ToString();
                }
            }
            
            // Advanced settings
            LoadAdvancedSettings();
            
            UpdateColorPreviews();
            
            Log.Information("Settings loaded successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load current settings");
        }
    }

    private void UpdateColorPreviews()
    {
        
        if (this.FindControl<Border>("ShapeColorPreview") is { } shapeColorPreview)
        {
            shapeColorPreview.Background = new SolidColorBrush(_currentSettings.DefaultStyles.Shape.StrokeColor);
        }
    }

    private void OnShapeColorClick(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Simple color picker - cycle through common colors
        var colors = new[] { Colors.Red, Colors.Blue, Colors.Green, Colors.Black, Colors.Yellow, Colors.Orange, Colors.Purple, Colors.Brown };
        var currentColor = _currentSettings.DefaultStyles.Shape.StrokeColor;
        var currentIndex = Array.IndexOf(colors, currentColor);
        var nextIndex = (currentIndex + 1) % colors.Length;
        
        _currentSettings.DefaultStyles.Shape.StrokeColor = colors[nextIndex];
        UpdateColorPreviews();
    }

    private void OnTextFontSizeChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _currentSettings.DefaultStyles.Text.FontSize = (int)e.NewValue;
        if (this.FindControl<TextBlock>("TextFontSizeValueText") is { } valueText)
        {
            valueText.Text = ((int)e.NewValue).ToString();
        }
    }
    
    private void OnShapeThicknessChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _currentSettings.DefaultStyles.Shape.StrokeThickness = e.NewValue;
        if (this.FindControl<TextBlock>("ShapeThicknessValueText") is { } valueText)
        {
            valueText.Text = ((int)e.NewValue).ToString();
        }
    }
    
    private void OnJpegQualityChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _currentSettings.DefaultStyles.Export.JpegQuality = e.NewValue;
        if (this.FindControl<TextBlock>("JpegQualityValueText") is { } valueText)
        {
            valueText.Text = ((int)e.NewValue).ToString();
        }
    }
    
    private void OnPngCompressionChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _currentSettings.DefaultStyles.Export.PngCompression = e.NewValue;
        if (this.FindControl<TextBlock>("PngCompressionValueText") is { } valueText)
        {
            valueText.Text = ((int)e.NewValue).ToString();
        }
    }
    
    
    private void LoadAdvancedSettings()
    {
        // Performance settings
        if (this.FindControl<CheckBox>("EnableHardwareAccelerationCheckBox") is { } hwAccel)
        {
            hwAccel.IsChecked = _currentSettings.DefaultStyles.Advanced.Performance.EnableHardwareAcceleration;
        }
        
        if (this.FindControl<CheckBox>("LimitFrameRateCheckBox") is { } limitFps)
        {
            limitFps.IsChecked = _currentSettings.DefaultStyles.Advanced.Performance.LimitFrameRate;
        }
        
        if (this.FindControl<ComboBox>("RenderQualityComboBox") is { } renderQuality)
        {
            var qualityValue = _currentSettings.DefaultStyles.Advanced.Performance.RenderQuality;
            foreach (ComboBoxItem item in renderQuality.Items.OfType<ComboBoxItem>())
            {
                if (item.Content?.ToString() == qualityValue)
                {
                    renderQuality.SelectedItem = item;
                    break;
                }
            }
        }
        
        
        // Security settings
        if (this.FindControl<CheckBox>("AllowTelemetryCheckBox") is { } telemetry)
        {
            telemetry.IsChecked = _currentSettings.DefaultStyles.Advanced.Security.AllowTelemetry;
        }
        
        
        // Debug settings
        if (this.FindControl<CheckBox>("EnableDebugLoggingCheckBox") is { } debugLogging)
        {
            debugLogging.IsChecked = _currentSettings.DefaultStyles.Advanced.Debug.EnableDebugLogging;
        }
        
        if (this.FindControl<CheckBox>("ShowDeveloperInfoCheckBox") is { } showDevInfo)
        {
            showDevInfo.IsChecked = _currentSettings.DefaultStyles.Advanced.Debug.ShowDeveloperInfo;
        }
        
        if (this.FindControl<ComboBox>("LogLevelComboBox") is { } logLevel)
        {
            var logLevelValue = _currentSettings.DefaultStyles.Advanced.Debug.LogLevel;
            foreach (ComboBoxItem item in logLevel.Items.OfType<ComboBoxItem>())
            {
                if (item.Content?.ToString() == logLevelValue)
                {
                    logLevel.SelectedItem = item;
                    break;
                }
            }
        }
    }
    
    private void OnOpenLogsDirectoryClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var logsPath = System.IO.Path.Combine(AppContext.BaseDirectory, "logs");
            if (!System.IO.Directory.Exists(logsPath))
            {
                System.IO.Directory.CreateDirectory(logsPath);
            }
            
            // Open logs directory in explorer
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = logsPath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(startInfo);
            
            Log.Debug("Opened logs directory: {LogsPath}", logsPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open logs directory");
        }
    }
    
    private void SaveAdvancedSettings()
    {
        // Performance settings
        if (this.FindControl<CheckBox>("EnableHardwareAccelerationCheckBox") is { } hwAccel)
        {
            _currentSettings.DefaultStyles.Advanced.Performance.EnableHardwareAcceleration = hwAccel.IsChecked ?? true;
        }
        
        if (this.FindControl<CheckBox>("LimitFrameRateCheckBox") is { } limitFps)
        {
            _currentSettings.DefaultStyles.Advanced.Performance.LimitFrameRate = limitFps.IsChecked ?? true;
        }
        
        if (this.FindControl<ComboBox>("RenderQualityComboBox") is { } renderQuality &&
            renderQuality.SelectedItem is ComboBoxItem selectedQuality)
        {
            _currentSettings.DefaultStyles.Advanced.Performance.RenderQuality = selectedQuality.Content?.ToString() ?? "Medium";
        }
        
        
        // Security settings
        if (this.FindControl<CheckBox>("AllowTelemetryCheckBox") is { } telemetry)
        {
            _currentSettings.DefaultStyles.Advanced.Security.AllowTelemetry = telemetry.IsChecked ?? false;
        }
        
        
        // Debug settings
        if (this.FindControl<CheckBox>("EnableDebugLoggingCheckBox") is { } debugLogging)
        {
            _currentSettings.DefaultStyles.Advanced.Debug.EnableDebugLogging = debugLogging.IsChecked ?? false;
        }
        
        if (this.FindControl<CheckBox>("ShowDeveloperInfoCheckBox") is { } showDevInfo)
        {
            _currentSettings.DefaultStyles.Advanced.Debug.ShowDeveloperInfo = showDevInfo.IsChecked ?? false;
        }
        
        if (this.FindControl<ComboBox>("LogLevelComboBox") is { } logLevel &&
            logLevel.SelectedItem is ComboBoxItem selectedLogLevel)
        {
            _currentSettings.DefaultStyles.Advanced.Debug.LogLevel = selectedLogLevel.Content?.ToString() ?? "Warning";
        }
    }

    private async void OnOkClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            SaveCurrentSettings();
            
            
            await _settingsService.UpdateSettingsAsync(_currentSettings);
            
            // Apply startup settings if ApplicationController is available
            if (_applicationController != null)
            {
                var startupEnabled = _currentSettings.General.StartWithWindows;
                var currentlyEnabled = await _applicationController.IsStartupWithWindowsEnabledAsync();
                
                if (startupEnabled != currentlyEnabled)
                {
                    var success = await _applicationController.SetStartupWithWindowsAsync(startupEnabled);
                    if (!success)
                    {
                        Log.Warning("Failed to update startup with Windows setting");
                        // Revert the setting in UI
                        _currentSettings.General.StartWithWindows = currentlyEnabled;
                        if (this.FindControl<CheckBox>("StartWithSystemCheckBox") is { } startWithSystemCheckBox)
                        {
                            startWithSystemCheckBox.IsChecked = currentlyEnabled;
                        }
                    }
                }
            }
            
            // Reload hotkeys if settings changed
            try
            {
                var hotkeyManager = App.Services?.GetService(typeof(IHotkeyManager)) as IHotkeyManager;
                if (hotkeyManager != null)
                {
                    Log.Debug("Reloading hotkeys after settings saved: CaptureRegion={CaptureRegion}, OpenSettings={OpenSettings}", 
                        _currentSettings.Hotkeys.CaptureRegion, _currentSettings.Hotkeys.OpenSettings);
                    await hotkeyManager.ReloadHotkeysAsync();
                    Log.Debug("Hotkeys reloaded successfully after settings change");
                }
                else
                {
                    Log.Error("HotkeyManager service not found - hotkeys will not be reloaded");
                }
            }
            catch (Exception hotkeyEx)
            {
                Log.Error(hotkeyEx, "Failed to reload hotkeys after settings change");
            }
            
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings");
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            _currentSettings = new AppSettings();
            LoadCurrentSettings();
            Log.Debug("Settings reset to defaults");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reset settings");
        }
    }

    private void SaveCurrentSettings()
    {
        try
        {
            // General settings
            if (this.FindControl<CheckBox>("StartWithSystemCheckBox") is { } startWithSystem)
            {
                _currentSettings.General.StartWithWindows = startWithSystem.IsChecked ?? false;
            }
            
            
            
            // Save format
            if (this.FindControl<ComboBox>("DefaultFormatComboBox") is { } formatCombo && 
                formatCombo.SelectedItem is ComboBoxItem selectedFormat)
            {
                _currentSettings.General.DefaultSaveFormat = selectedFormat.Content?.ToString() ?? "PNG";
            }
            
            // Save hotkeys from text boxes
            if (this.FindControl<TextBox>("CaptureRegionHotkeyTextBox") is { } captureRegionText)
            {
                _currentSettings.Hotkeys.CaptureRegion = captureRegionText.Text ?? "Alt+A";
            }
            
            if (this.FindControl<TextBox>("OpenSettingsHotkeyTextBox") is { } openSettingsText)
            {
                _currentSettings.Hotkeys.OpenSettings = openSettingsText.Text ?? "Alt+S";
            }
            
            // Save advanced settings
            SaveAdvancedSettings();
            
            // Style settings are already updated in real-time through sliders and color clicks
            
            Log.Debug("Current settings saved from UI");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save current settings from UI");
        }
    }

    private static AppSettings CloneSettings(AppSettings source)
    {
        try
        {
            // Simple deep clone using JSON serialization
            var json = System.Text.Json.JsonSerializer.Serialize(source);
            return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }
    
    
    
    private void OnCaptureRegionHotkeyChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            _currentSettings.Hotkeys.CaptureRegion = textBox.Text ?? "Alt+A";
        }
    }
    
    
    private void OnOpenSettingsHotkeyChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            _currentSettings.Hotkeys.OpenSettings = textBox.Text ?? "Alt+S";
        }
    }
    
    private void OnHotkeyTextBoxGotFocus(object? sender, Avalonia.Input.GotFocusEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.SelectAll();
            textBox.Background = new SolidColorBrush(Color.FromArgb(80, 100, 150, 255)); // Light blue highlight
        }
    }
    
    private void OnHotkeyTextBoxKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        
        e.Handled = true; // Prevent default text input
        
        // Handle ESC to cancel
        if (e.Key == Avalonia.Input.Key.Escape)
        {
            textBox.Background = new SolidColorBrush(Color.FromArgb(64, 34, 68, 102)); // Reset background
            return;
        }
        
        // Ignore modifier keys by themselves
        if (IsModifierKey(e.Key))
        {
            return;
        }
        
        // Build hotkey string with platform-specific mapping
        var modifiers = new List<string>();
        
        if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))
            modifiers.Add("Ctrl");
        if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Alt))
        {
            // On macOS, Alt key is Option key, use consistent naming
            modifiers.Add("Alt");
        }
        if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift))
            modifiers.Add("Shift");
        if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Meta))
        {
            // On macOS, Meta is Command key, but we use Win for consistency
            if (OperatingSystem.IsMacOS())
                modifiers.Add("Cmd");
            else
                modifiers.Add("Win");
        }
            
        // Add the main key
        var keyName = GetKeyDisplayName(e.Key);
        if (!string.IsNullOrEmpty(keyName))
        {
            modifiers.Add(keyName);
        }
        
        if (modifiers.Count > 0)
        {
            var hotkeyString = string.Join("+", modifiers);
            textBox.Text = hotkeyString;
            
            // Update settings immediately
            if (textBox.Name == "CaptureRegionHotkeyTextBox")
            {
                _currentSettings.Hotkeys.CaptureRegion = hotkeyString;
            }
            else if (textBox.Name == "OpenSettingsHotkeyTextBox")
            {
                _currentSettings.Hotkeys.OpenSettings = hotkeyString;
            }
            
            textBox.Background = new SolidColorBrush(Color.FromArgb(64, 34, 68, 102)); // Reset background
        }
    }
    
    private static bool IsModifierKey(Avalonia.Input.Key key)
    {
        return key is Avalonia.Input.Key.LeftCtrl or Avalonia.Input.Key.RightCtrl or
                     Avalonia.Input.Key.LeftAlt or Avalonia.Input.Key.RightAlt or
                     Avalonia.Input.Key.LeftShift or Avalonia.Input.Key.RightShift or
                     Avalonia.Input.Key.LWin or Avalonia.Input.Key.RWin;
    }
    
    private static string GetKeyDisplayName(Avalonia.Input.Key key)
    {
        return key switch
        {
            Avalonia.Input.Key.A => "A", Avalonia.Input.Key.B => "B", Avalonia.Input.Key.C => "C",
            Avalonia.Input.Key.D => "D", Avalonia.Input.Key.E => "E", Avalonia.Input.Key.F => "F",
            Avalonia.Input.Key.G => "G", Avalonia.Input.Key.H => "H", Avalonia.Input.Key.I => "I",
            Avalonia.Input.Key.J => "J", Avalonia.Input.Key.K => "K", Avalonia.Input.Key.L => "L",
            Avalonia.Input.Key.M => "M", Avalonia.Input.Key.N => "N", Avalonia.Input.Key.O => "O",
            Avalonia.Input.Key.P => "P", Avalonia.Input.Key.Q => "Q", Avalonia.Input.Key.R => "R",
            Avalonia.Input.Key.S => "S", Avalonia.Input.Key.T => "T", Avalonia.Input.Key.U => "U",
            Avalonia.Input.Key.V => "V", Avalonia.Input.Key.W => "W", Avalonia.Input.Key.X => "X",
            Avalonia.Input.Key.Y => "Y", Avalonia.Input.Key.Z => "Z",
            Avalonia.Input.Key.D0 => "0", Avalonia.Input.Key.D1 => "1", Avalonia.Input.Key.D2 => "2",
            Avalonia.Input.Key.D3 => "3", Avalonia.Input.Key.D4 => "4", Avalonia.Input.Key.D5 => "5",
            Avalonia.Input.Key.D6 => "6", Avalonia.Input.Key.D7 => "7", Avalonia.Input.Key.D8 => "8",
            Avalonia.Input.Key.D9 => "9",
            Avalonia.Input.Key.F1 => "F1", Avalonia.Input.Key.F2 => "F2", Avalonia.Input.Key.F3 => "F3",
            Avalonia.Input.Key.F4 => "F4", Avalonia.Input.Key.F5 => "F5", Avalonia.Input.Key.F6 => "F6",
            Avalonia.Input.Key.F7 => "F7", Avalonia.Input.Key.F8 => "F8", Avalonia.Input.Key.F9 => "F9",
            Avalonia.Input.Key.F10 => "F10", Avalonia.Input.Key.F11 => "F11", Avalonia.Input.Key.F12 => "F12",
            Avalonia.Input.Key.Space => "Space",
            Avalonia.Input.Key.Enter => "Enter",
            Avalonia.Input.Key.Tab => "Tab",
            _ => key.ToString()
        };
    }
    
    /// <summary>
    /// Update macOS permission status display
    /// </summary>
    private void UpdateMacPermissionStatus()
    {
        try
        {
            var permissionPanel = this.FindControl<Border>("MacPermissionPanel");
            var statusIcon = this.FindControl<TextBlock>("PermissionStatusIcon");
            var statusText = this.FindControl<TextBlock>("PermissionStatusText");
            
            if (permissionPanel == null || statusIcon == null || statusText == null)
                return;
            
            // Only show on macOS
            if (!OperatingSystem.IsMacOS())
            {
                permissionPanel.IsVisible = false;
                return;
            }
            
            var hasPermission = MacHotkeyProvider.HasAccessibilityPermissions;
            permissionPanel.IsVisible = true;
            
            if (hasPermission)
            {
                statusIcon.Text = "[✓]";
                statusText.Text = "macOS Accessibility Permission: Granted";
                statusText.Foreground = Brushes.Green;
            }
            else
            {
                statusIcon.Text = "[!]";
                statusText.Text = "macOS Accessibility Permission: Not Granted";
                statusText.Foreground = Brushes.DarkOrange;
            }
            
            Log.Debug("Updated macOS permission status: {HasPermission}", hasPermission);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update macOS permission status");
        }
    }
    
    /// <summary>
    /// Show permission guide dialog
    /// </summary>
    private async void ShowPermissionGuide(object? sender, RoutedEventArgs e)
    {
        try
        {
            await AccessibilityPermissionDialog.ShowAsync(this);
            
            // Refresh status after dialog closes
            UpdateMacPermissionStatus();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show permission guide");
        }
    }
    
    /// <summary>
    /// Refresh permission status
    /// </summary>
    private void RefreshPermissionStatus(object? sender, RoutedEventArgs e)
    {
        UpdateMacPermissionStatus();
    }
    
    /// <summary>
    /// Show debug info for permission troubleshooting
    /// </summary>
    private async void ShowPermissionDebugInfo(object? sender, RoutedEventArgs e)
    {
        try
        {
            var pathInfo = MacHotkeyProvider.GetCurrentApplicationPath();
            var hasPermission = MacHotkeyProvider.HasAccessibilityPermissions;
            
            var debugInfo = $"Accessibility Permission Status: {(hasPermission ? "Granted" : "Not Granted")}\n\n" +
                           $"Current Application Path Information:\n{pathInfo}\n\n" +
                           $"Please ensure that the path added in System Preferences > Security & Privacy > Accessibility matches the currently running application path.";
            
            Log.Debug("Permission debug info: {DebugInfo}", debugInfo);
            
            // Show debug info dialog
            var dialog = new Window
            {
                Title = "Permission Debug Info",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = true
            };
            
            var scrollViewer = new ScrollViewer
            {
                Margin = new Thickness(10),
                Content = new TextBlock
                {
                    Text = debugInfo,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = "Consolas,Monaco,monospace",
                    FontSize = 12
                }
            };
            
            dialog.Content = scrollViewer;
            await dialog.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show permission debug info");
        }
    }
}

// Design-time settings service for XAML preview
internal class DesignTimeSettingsService : ISettingsService
{
    public AppSettings Settings { get; } = new AppSettings();
    
    public Task SaveAsync() => Task.CompletedTask;
    public void ResetToDefaults() { }
    public Task UpdateSettingsAsync(AppSettings newSettings) => Task.CompletedTask;
    public string GetSettingsFilePath() => "design-time-settings.json";
}