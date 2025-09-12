using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AGI.Captor.App.Services.Hotkeys;
using AGI.Captor.App.Services.Overlay;
using Serilog;

namespace AGI.Captor.App.Services;

/// <summary>
/// Hotkey manager for managing application hotkeys
/// </summary>
public class HotkeyManager : IHotkeyManager
{
    private readonly IHotkeyProvider _hotkeyProvider;
    private readonly ISettingsService _settingsService;
    private readonly IOverlayController _overlayController;
    private readonly Dictionary<string, string> _registeredHotkeys = new();

    public HotkeyManager(
        IHotkeyProvider hotkeyProvider,
        ISettingsService settingsService,
        IOverlayController overlayController)
    {
        _hotkeyProvider = hotkeyProvider ?? throw new ArgumentNullException(nameof(hotkeyProvider));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _overlayController = overlayController ?? throw new ArgumentNullException(nameof(overlayController));
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Settings should already be loaded by SettingsService
            await ReloadHotkeysAsync();
            Log.Debug("Hotkey manager initialized");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize hotkey manager");
        }
    }

    public async Task ReloadHotkeysAsync()
    {
        try
        {
            // Unregister all existing hotkeys
            UnregisterAllHotkeys();
            
            // Wait a bit to ensure unregistration is complete
            await Task.Delay(100);
            Log.Debug("Waited for hotkey unregistration to complete");
            
            var settings = _settingsService.Settings;
            Log.Debug("HotkeyManager.ReloadHotkeysAsync: Current settings - CaptureRegion={CaptureRegion}, OpenSettings={OpenSettings}", 
                settings.Hotkeys.CaptureRegion, settings.Hotkeys.OpenSettings);
            
            // Register capture region hotkey
            if (!string.IsNullOrEmpty(settings.Hotkeys.CaptureRegion))
            {
                Log.Debug("Registering capture region hotkey: {Hotkey}", settings.Hotkeys.CaptureRegion);
                var success = RegisterHotkey("capture_region", settings.Hotkeys.CaptureRegion, () => _overlayController.ShowAll());
                if (!success)
                {
                    Log.Debug("Failed to register capture region hotkey: {Hotkey}", settings.Hotkeys.CaptureRegion);
                }
                else
                {
                    Log.Debug("Successfully registered capture region hotkey: {Hotkey}", settings.Hotkeys.CaptureRegion);
                }
            }
            
            
            // Register open settings hotkey
            if (!string.IsNullOrEmpty(settings.Hotkeys.OpenSettings))
            {
                Log.Debug("Registering open settings hotkey: {Hotkey}", settings.Hotkeys.OpenSettings);
                var success = RegisterHotkey("open_settings", settings.Hotkeys.OpenSettings, () => OpenSettings());
                if (!success)
                {
                    Log.Debug("Failed to register open settings hotkey: {Hotkey}", settings.Hotkeys.OpenSettings);
                }
                else
                {
                    Log.Debug("Successfully registered open settings hotkey: {Hotkey}", settings.Hotkeys.OpenSettings);
                }
            }
            
            Log.Debug("Hotkeys reloaded from settings");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reload hotkeys");
        }
    }

    public bool RegisterHotkey(string id, string hotkeyString, Action callback)
    {
        try
        {
            // Always attempt to unregister first to ensure clean state
            Log.Debug("Attempting to unregister hotkey {Id} before registration", id);
            _hotkeyProvider.Unregister(id);
            
            var chord = ParseHotkeyString(hotkeyString);
            if (chord == null)
            {
                Log.Debug("Failed to parse hotkey string: {HotkeyString}", hotkeyString);
                return false;
            }

            Log.Debug("Attempting to register hotkey {Id} with chord {Chord}", id, chord.Value);
            var success = _hotkeyProvider.Register(id, chord.Value, callback);
            if (success)
            {
                _registeredHotkeys[id] = hotkeyString;
                Log.Debug("Successfully registered hotkey: {Id} -> {HotkeyString}", id, hotkeyString);
            }
            else
            {
                Log.Debug("Failed to register hotkey with provider: {Id} -> {HotkeyString}", id, hotkeyString);
            }

            return success;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error registering hotkey: {Id} -> {HotkeyString}", id, hotkeyString);
            return false;
        }
    }

    public void UnregisterHotkey(string id)
    {
        try
        {
            _hotkeyProvider.Unregister(id);
            _registeredHotkeys.Remove(id);
            Log.Debug("Hotkey unregistered: {Id}", id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error unregistering hotkey: {Id}", id);
        }
    }

    public void UnregisterAllHotkeys()
    {
        try
        {
            Log.Debug("Unregistering {Count} hotkeys: {Keys}", _registeredHotkeys.Count, string.Join(", ", _registeredHotkeys.Keys));
            foreach (var id in _registeredHotkeys.Keys.ToList())
            {
                Log.Debug("Unregistering hotkey: {Id}", id);
                _hotkeyProvider.Unregister(id);
                Log.Debug("Unregistered hotkey: {Id}", id);
            }
            _registeredHotkeys.Clear();
            Log.Debug("All hotkeys unregistered successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error unregistering all hotkeys");
        }
    }

    public HotkeyChord? ParseHotkeyString(string hotkeyString)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString))
            return null;

        try
        {
            var parts = hotkeyString.Split('+');
            if (parts.Length == 0)
                return null;

            var modifiers = HotkeyModifiers.None;
            string? keyPart = null;

            foreach (var part in parts)
            {
                var trimmedPart = part.Trim();
                switch (trimmedPart.ToLower())
                {
                    case "ctrl":
                    case "control":
                        modifiers |= HotkeyModifiers.Control;
                        break;
                    case "alt":
                        modifiers |= HotkeyModifiers.Alt;
                        break;
                    case "shift":
                        modifiers |= HotkeyModifiers.Shift;
                        break;
                    case "win":
                    case "windows":
                        modifiers |= HotkeyModifiers.Win;
                        break;
                    default:
                        keyPart = trimmedPart;
                        break;
                }
            }

            if (string.IsNullOrEmpty(keyPart))
                return null;

            // Convert key string to virtual key code
            var virtualKey = GetVirtualKeyCode(keyPart);
            if (virtualKey == 0)
                return null;

            return new HotkeyChord(modifiers, (uint)virtualKey);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to parse hotkey string: {HotkeyString}", hotkeyString);
            return null;
        }
    }

    private static int GetVirtualKeyCode(string key)
    {
        var upperKey = key.ToUpper();
        
        // Add debug logging for key parsing
        Log.Debug("Parsing virtual key code for: {Key} -> {UpperKey}", key, upperKey);
        
        var code = upperKey switch
        {
            "A" => 0x41, "B" => 0x42, "C" => 0x43, "D" => 0x44, "E" => 0x45, "F" => 0x46,
            "G" => 0x47, "H" => 0x48, "I" => 0x49, "J" => 0x4A, "K" => 0x4B, "L" => 0x4C,
            "M" => 0x4D, "N" => 0x4E, "O" => 0x4F, "P" => 0x50, "Q" => 0x51, "R" => 0x52,
            "S" => 0x53, "T" => 0x54, "U" => 0x55, "V" => 0x56, "W" => 0x57, "X" => 0x58,
            "Y" => 0x59, "Z" => 0x5A,
            "0" => 0x30, "1" => 0x31, "2" => 0x32, "3" => 0x33, "4" => 0x34,
            "5" => 0x35, "6" => 0x36, "7" => 0x37, "8" => 0x38, "9" => 0x39,
            "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73, "F5" => 0x74, "F6" => 0x75,
            "F7" => 0x76, "F8" => 0x77, "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
            "SPACE" => 0x20,
            "ENTER" => 0x0D,
            "ESC" => 0x1B,
            "TAB" => 0x09,
            _ => 0
        };
        
        Log.Debug("Virtual key code for {Key}: 0x{Code:X2}", upperKey, code);
        return code;
    }


    private void OpenSettings()
    {
        try
        {
            Log.Debug("Open settings requested via hotkey");
            
            // Use dispatcher to safely access UI thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    Log.Debug("Dispatcher: Attempting to open settings window");
                    var settingsService = App.Services?.GetService(typeof(ISettingsService)) as ISettingsService;
                    var applicationController = App.Services?.GetService(typeof(IApplicationController)) as IApplicationController;
                    
                    if (settingsService != null)
                    {
                        Log.Debug("Creating settings window");
                        var settingsWindow = new AGI.Captor.App.Views.SettingsWindow(settingsService, applicationController);
                        settingsWindow.Show();
                        settingsWindow.Activate();
                        Log.Debug("Settings window opened via hotkey");
                    }
                    else
                    {
                        Log.Error("SettingsService is null - cannot open settings");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to open settings window via hotkey");
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open settings via hotkey");
        }
    }
}
