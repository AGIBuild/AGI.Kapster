using System;
using System.Threading.Tasks;

using Serilog;

using AGI.Kapster.Desktop.Services.Hotkeys;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using AGI.Kapster.Desktop.Services.Settings;
using AGI.Kapster.Desktop.Services.Update;
using AGI.Kapster.Desktop.Views;

namespace AGI.Kapster.Desktop.Services.Hotkeys;

/// <summary>
/// Hotkey manager - uses dependency injection for platform-specific provider
/// </summary>
public class HotkeyManager : IHotkeyManager
{
    private readonly IHotkeyProvider _hotkeyProvider;
    private readonly ISettingsService _settingsService;
    private readonly IOverlayCoordinator _overlayCoordinator;
    private bool _escHotkeyRegistered = false;

    public HotkeyManager(
        IHotkeyProvider hotkeyProvider,
        ISettingsService settingsService,
        IOverlayCoordinator overlayCoordinator)
    {
        _hotkeyProvider = hotkeyProvider ?? throw new ArgumentNullException(nameof(hotkeyProvider));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _overlayCoordinator = overlayCoordinator ?? throw new ArgumentNullException(nameof(overlayCoordinator));

        // Subscribe to settings changes
        _settingsService.SettingsChanged += OnSettingsChanged;

        Log.Debug("HotkeyManager constructor completed with provider: {Type}", _hotkeyProvider.GetType().Name);
    }

    private async void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        // Reload hotkeys when settings change
        Log.Information("Settings changed, reloading hotkeys");
        await ReloadHotkeysAsync();
    }

    public async Task InitializeAsync()
    {
        try
        {
            Log.Debug("Initializing hotkey manager...");

            if (!_hotkeyProvider.IsSupported)
            {
                Log.Warning("Hotkeys not supported on this platform");
                return;
            }


            if (OperatingSystem.IsMacOS())
            {
                // Delay permission check (macOS accessibility).
                await Task.Delay(500);

                // Re-check permissions (macOS .app accessibility may need time).
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
            }
            else
            {
                if (!_hotkeyProvider.HasPermissions)
                {
                    Log.Warning("No permissions for hotkey registration - HasPermissions: {HasPermissions}", _hotkeyProvider.HasPermissions);
                    return;
                }
            }

            if (_hotkeyProvider is WindowsHotkeyProvider windowsProvider)
            {
                await windowsProvider.WaitUntilReadyAsync(TimeSpan.FromSeconds(2));
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

            // Use injected singleton settings service to get latest settings
            var settings = _settingsService.Settings;

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

    private async Task StartCaptureSessionAsync()
    {
        await _overlayCoordinator.StartSessionAsync();
        RegisterEscapeHotkey();
    }
    private void RegisterCaptureRegionHotkey(string combination)
    {
        if (ParseHotkeyString(combination, out var modifiers, out var keyCode))
        {
            var success = _hotkeyProvider.RegisterHotkey("capture_region", modifiers, keyCode, () =>
            {
                Log.Debug("Capture region hotkey triggered");
                // Prevent reentry if a screenshot is already active
                if (_overlayCoordinator.HasActiveSession)
                {
                    Log.Debug("Capture hotkey ignored - screenshot is already active");
                    return;
                }
                

                // Ensure the capture session starts on the UI thread.
                if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
                {
                    _ = StartCaptureSessionAsync();
                }
                else
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(async () => await StartCaptureSessionAsync());
                }
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

                // Check if screenshot is currently in progress
                if (_overlayCoordinator.HasActiveSession)
                {
                    Log.Debug("Settings hotkey ignored - screenshot is active");
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
            // App.ShowSettingsWindow() now handles UI thread dispatch internally
            App.ShowSettingsWindow();
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
                var lower = trimmed.ToLowerInvariant();
                switch (lower)
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
                        // Named keys
                        keyCode = MapKeyNameToVk(lower);
                        if (keyCode == 0 && trimmed.Length == 1)
                        {
                            // Single character (letter/digit)
                            keyCode = (uint)char.ToUpperInvariant(trimmed[0]);
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

    private static uint MapKeyNameToVk(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return 0;

        // Function keys F1-F24
        if (name.Length >= 2 && name[0] == 'f' && int.TryParse(name.AsSpan(1), out var fnum) && fnum >= 1 && fnum <= 24)
            return (uint)(0x70 + (fnum - 1));

        // Arrows
        switch (name)
        {
            case "space": return 0x20;
            case "enter": return 0x0D;
            case "esc":
            case "escape": return 0x1B;
            case "tab": return 0x09;
            case "backspace":
            case "bksp": return 0x08;
            case "delete":
            case "del": return 0x2E;
            case "insert":
            case "ins": return 0x2D;
            case "home": return 0x24;
            case "end": return 0x23;
            case "pageup":
            case "pgup": return 0x21;
            case "pagedown":
            case "pgdn": return 0x22;
            case "up": return 0x26;
            case "down": return 0x28;
            case "left": return 0x25;
            case "right": return 0x27;

            // Numpad digits and operations
            case "numpad0": return 0x60;
            case "numpad1": return 0x61;
            case "numpad2": return 0x62;
            case "numpad3": return 0x63;
            case "numpad4": return 0x64;
            case "numpad5": return 0x65;
            case "numpad6": return 0x66;
            case "numpad7": return 0x67;
            case "numpad8": return 0x68;
            case "numpad9": return 0x69;
            case "numpad+":
            case "add": return 0x6B;
            case "numpad-":
            case "sub":
            case "subtract": return 0x6D;
            case "numpad*":
            case "mul":
            case "multiply": return 0x6A;
            case "numpad/":
            case "div":
            case "divide": return 0x6F;
            case "numpad.":
            case "decimal": return 0x6E;

            // OEM symbols (US layout)
            case "-": return 0xBD; // VK_OEM_MINUS
            case "=": return 0xBB; // VK_OEM_PLUS
            case "[": return 0xDB; // VK_OEM_4
            case "]": return 0xDD; // VK_OEM_6
            case "\\": return 0xDC; // VK_OEM_5
            case ";": return 0xBA; // VK_OEM_1
            case "'": return 0xDE; // VK_OEM_7
            case ",": return 0xBC; // VK_OEM_COMMA
            case ".": return 0xBE; // VK_OEM_PERIOD
            case "/": return 0xBF; // VK_OEM_2
            case "`": return 0xC0; // VK_OEM_3
        }

        return 0;
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
            Log.Debug("ESC hotkey triggered, cancelling screenshot");
            _overlayCoordinator.CloseCurrentSession();

            // Unregister ESC hotkey after cancelling screenshot
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
    /// Unregister ESC hotkey
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
/// Hotkey provider for unsupported platforms
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
