using System;
using System.Threading.Tasks;
using AGI.Captor.Desktop.Services.Hotkeys;
using AGI.Captor.Desktop.Services.Overlay;
using AGI.Captor.Desktop.Views;
using Serilog;

namespace AGI.Captor.Desktop.Services;

/// <summary>
/// Hotkey manager - strategy-based provider per platform
/// </summary>
public class HotkeyManager : IHotkeyManager
{
    private IHotkeyProvider _hotkeyProvider;
    private readonly ISettingsService _settingsService;
    private readonly IOverlayController _overlayController;
    private bool _escHotkeyRegistered = false;

    public HotkeyManager(
        ISettingsService settingsService,
        IOverlayController overlayController)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _overlayController = overlayController ?? throw new ArgumentNullException(nameof(overlayController));
        
        // Defer provider creation to InitializeAsync
        _hotkeyProvider = null!; // Will be created in InitializeAsync
        
        Log.Debug("HotkeyManager constructor completed");
    }

    private static IHotkeyProvider CreateHotkeyProvider()
    {
        if (OperatingSystem.IsWindows())
        {
            Log.Debug("Creating Windows hotkey provider");
            return new WindowsHotkeyProvider();
        }
        else if (OperatingSystem.IsMacOS())
        {
            Log.Debug("Creating macOS hotkey provider");
            return new MacHotkeyProvider();
        }
        else
        {
            Log.Warning("Unsupported platform for hotkeys");
            return new UnsupportedHotkeyProvider();
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            Log.Debug("Initializing hotkey manager...");
            
            // Create hotkey provider
            _hotkeyProvider = CreateHotkeyProvider();
            Log.Debug("Hotkey provider created: {Type}", _hotkeyProvider.GetType().Name);

            if (!_hotkeyProvider.IsSupported)
            {
                Log.Warning("Hotkeys not supported on this platform");
                return;
            }

            // Delay permission check (macOS accessibility)
            await Task.Delay(500);
            
            // Re-check permissions (macOS .app accessibility may need time)
            if (!_hotkeyProvider.HasPermissions)
            {
                Log.Warning("No permissions for hotkey registration - HasPermissions: {HasPermissions}", _hotkeyProvider.HasPermissions);
                Log.Warning("Retrying permission check in 2 seconds...");
                
                // Delay and retry
                await Task.Delay(2000);
                
                if (!_hotkeyProvider.HasPermissions)
                {
                    Log.Error("Still no permissions after retry. Please check accessibility settings.");
                    return;
                }
                
                Log.Information("Permissions granted after retry");
            }
            
            await ReloadHotkeysAsync();
            Log.Debug("Hotkey manager initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize hotkey manager");
        }
    }

    public Task ReloadHotkeysAsync()
    {
        try
        {
            // Unregister all existing hotkeys
            _hotkeyProvider.UnregisterAll();

            // Create new settings service instance to get latest settings
            var settingsService = new SettingsService();
            var settings = settingsService.Settings;

            // Load hotkey configurations from settings
            RegisterCaptureRegionHotkey(settings.Hotkeys.CaptureRegion);
            RegisterOpenSettingsHotkey(settings.Hotkeys.OpenSettings);

            Log.Debug("Hotkeys reloaded from settings: CaptureRegion={CaptureRegion}, OpenSettings={OpenSettings}", 
                settings.Hotkeys.CaptureRegion, settings.Hotkeys.OpenSettings);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reload hotkeys");
        }
        
        return Task.CompletedTask;
    }

    private void RegisterCaptureRegionHotkey(string combination)
    {
        if (ParseHotkeyString(combination, out var modifiers, out var keyCode))
        {
            var success = _hotkeyProvider.RegisterHotkey("capture_region", modifiers, keyCode, () =>
            {
                Log.Debug("Capture region hotkey triggered");
                // Prevent reentry if an overlay session is already active
                if (_overlayController.IsActive)
                {
                    Log.Debug("Capture hotkey ignored - overlay is already active");
                    return;
                }
                _overlayController.ShowAll();
                
                RegisterEscapeHotkey();
            });

            if (success)
            {
                Log.Debug("Successfully registered capture region hotkey: {Combination}", combination);
            }
            else
            {
                Log.Warning("Failed to register capture region hotkey: {Combination}", combination);
            }
        }
    }

    private void RegisterOpenSettingsHotkey(string combination)
    {
        if (ParseHotkeyString(combination, out var modifiers, out var keyCode))
        {
            var success = _hotkeyProvider.RegisterHotkey("open_settings", modifiers, keyCode, () =>
            {
                Log.Debug("Open settings hotkey triggered");
                
                // Check if overlay windows are currently active (screenshot in progress)
                if (_overlayController.IsActive)
                {
                    Log.Debug("Settings hotkey ignored - screenshot overlay is active");
                    return;
                }
                
                ShowSettingsWindow();
            });

            if (success)
            {
                Log.Debug("Successfully registered open settings hotkey: {Combination}", combination);
            }
            else
            {
                Log.Warning("Failed to register open settings hotkey: {Combination}", combination);
            }
        }
    }

    private static void ShowSettingsWindow()
    {
        try
        {
            // Create proper service instances - avoid default constructor
            var settingsService = new SettingsService();
            var applicationController = App.Services?.GetService(typeof(IApplicationController)) as IApplicationController;
            
            var settingsWindow = new SettingsWindow(settingsService, applicationController);
            settingsWindow.Show();
            
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show settings window");
        }
    }

    private static bool ParseHotkeyString(string combination, out HotkeyModifiers modifiers, out uint keyCode)
    {
        modifiers = HotkeyModifiers.None;
        keyCode = 0;

        if (string.IsNullOrEmpty(combination))
            return false;

        try
        {
            var parts = combination.Split('+');
            
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                switch (trimmed.ToLowerInvariant())
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
                    case "cmd":
                    case "command":
                        modifiers |= HotkeyModifiers.Win;
                        break;
                    default:
                        // Try to parse as key code
                        if (trimmed.Length == 1)
                        {
                            keyCode = char.ToUpperInvariant(trimmed[0]);
                        }
                        break;
                }
            }

            return keyCode != 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to parse hotkey combination: {Combination}", combination);
            return false;
        }
    }

    /// <summary>
    /// Register ESC hotkey for closing screenshot overlay
    /// </summary>
    public void RegisterEscapeHotkey()
    {
        if (_escHotkeyRegistered || !_hotkeyProvider.IsSupported || !_hotkeyProvider.HasPermissions)
            return;

        var success = _hotkeyProvider.RegisterHotkey("overlay_escape", HotkeyModifiers.None, 0x1B, () =>
        {
            Log.Debug("ESC hotkey triggered, closing all overlays");
            _overlayController.CloseAll();
            
            // 关闭遮罩层后，注销ESC热键
            UnregisterEscapeHotkey();
        });

        if (success)
        {
            _escHotkeyRegistered = true;
            Log.Debug("ESC hotkey registered for overlay close");
        }
        else
        {
            Log.Warning("Failed to register ESC hotkey for overlay close");
        }
    }

    /// <summary>
    /// 注销ESC热键
    /// </summary>
    public void UnregisterEscapeHotkey()
    {
        if (!_escHotkeyRegistered)
            return;

        var success = _hotkeyProvider.UnregisterHotkey("overlay_escape");
        if (success)
        {
            _escHotkeyRegistered = false;
            Log.Debug("ESC hotkey unregistered");
        }
    }

    public void Dispose()
    {
        _hotkeyProvider?.Dispose();
        Log.Debug("HotkeyManager disposed");
    }
}

/// <summary>
/// 不支持平台的热键提供者
/// </summary>
internal class UnsupportedHotkeyProvider : IHotkeyProvider
{
    public bool IsSupported => false;
    public bool HasPermissions => false;

    public bool RegisterHotkey(string id, HotkeyModifiers modifiers, uint keyCode, Action callback)
    {
        Log.Debug("Hotkey registration ignored (unsupported platform): {Id}", id);
        return false;
    }

    public bool UnregisterHotkey(string id)
    {
        return false;
    }

    public void UnregisterAll()
    {
        // Nothing to do
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}