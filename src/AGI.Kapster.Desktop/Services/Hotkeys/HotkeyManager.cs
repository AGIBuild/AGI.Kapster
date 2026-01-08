using System;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using AGI.Kapster.Desktop.Services.Settings;
using AGI.Kapster.Desktop.Services.Update;
using AGI.Kapster.Desktop.Views;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Hotkeys;

/// <summary>
/// Hotkey manager - uses dependency injection for platform-specific provider and resolver
/// </summary>
public class HotkeyManager : IHotkeyManager
{
    private readonly IHotkeyProvider _hotkeyProvider;
    private readonly ISettingsService _settingsService;
    private readonly IOverlayCoordinator _overlayCoordinator;
    private readonly IKeyboardLayoutMonitor? _layoutMonitor;
    private bool _escHotkeyRegistered = false;

    public HotkeyManager(
        IHotkeyProvider hotkeyProvider,
        ISettingsService settingsService,
        IOverlayCoordinator overlayCoordinator,
        IKeyboardLayoutMonitor? layoutMonitor = null)
    {
        _hotkeyProvider = hotkeyProvider ?? throw new ArgumentNullException(nameof(hotkeyProvider));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _overlayCoordinator = overlayCoordinator ?? throw new ArgumentNullException(nameof(overlayCoordinator));
        _layoutMonitor = layoutMonitor;

        // Subscribe to settings changes
        _settingsService.SettingsChanged += OnSettingsChanged;

        // Subscribe to keyboard layout changes for character-stable hotkeys
        // Note: Monitoring will only start if character-based hotkeys are registered
        if (_layoutMonitor != null)
        {
            _layoutMonitor.LayoutChanged += OnLayoutChanged;
        }

        // Intentionally keep constructor logging minimal; hotkey registration logs are more actionable.
    }

    private async void OnLayoutChanged(object? sender, EventArgs e)
    {
        // Only re-register character-based hotkeys (layout changes don't affect named keys)
        Log.Information("Keyboard layout changed, re-registering character-based hotkeys");
        await ReloadCharacterHotkeysAsync();
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


            // Carbon hotkeys don't require special permissions, but check anyway
            if (!_hotkeyProvider.HasPermissions)
            {
                Log.Warning("No permissions for hotkey registration - HasPermissions: {HasPermissions}", _hotkeyProvider.HasPermissions);
                // Don't return early - Carbon provider may still work
            }

            await ReloadHotkeysAsync();
            
            // Start monitoring keyboard layout changes ONLY if character-based hotkeys exist
            // This avoids unnecessary monitoring overhead when only named keys are used
            if (_layoutMonitor != null && HasCharacterHotkeys())
            {
                _layoutMonitor.StartMonitoring();
            }
            
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

            // Load hotkey configurations from settings using new HotkeyGesture model
            RegisterCaptureRegionHotkey(settings.Hotkeys.CaptureRegion);
            RegisterOpenSettingsHotkey(settings.Hotkeys.OpenSettings);

            // Start/stop layout monitoring based on whether character hotkeys exist
            if (_layoutMonitor != null)
            {
                if (HasCharacterHotkeys())
                {
                    if (!_layoutMonitor.IsMonitoring)
                    {
                        _layoutMonitor.StartMonitoring();
                    }
                }
                else
                {
                    if (_layoutMonitor.IsMonitoring)
                    {
                        _layoutMonitor.StopMonitoring();
                    }
                }
            }

            Log.Debug("Hotkeys reloaded from settings: CaptureRegion={CaptureRegion}, OpenSettings={OpenSettings}",
                settings.Hotkeys.CaptureRegion?.ToDisplayString(), settings.Hotkeys.OpenSettings?.ToDisplayString());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reload hotkeys");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reload only character-based hotkeys (used when keyboard layout changes)
    /// </summary>
    private Task ReloadCharacterHotkeysAsync()
    {
        try
        {
            var settings = _settingsService.Settings;

            // Only re-register character-based hotkeys
            if (settings.Hotkeys.CaptureRegion?.KeySpec is CharKeySpec)
            {
                RegisterCaptureRegionHotkey(settings.Hotkeys.CaptureRegion);
            }

            if (settings.Hotkeys.OpenSettings?.KeySpec is CharKeySpec)
            {
                RegisterOpenSettingsHotkey(settings.Hotkeys.OpenSettings);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reload character hotkeys");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Check if any registered hotkeys are character-based (require layout monitoring)
    /// </summary>
    private bool HasCharacterHotkeys()
    {
        var settings = _settingsService.Settings;
        return settings.Hotkeys.CaptureRegion?.KeySpec is CharKeySpec ||
               settings.Hotkeys.OpenSettings?.KeySpec is CharKeySpec;
    }

    private async Task StartCaptureSessionAsync()
    {
        await _overlayCoordinator.StartSessionAsync();
        RegisterEscapeHotkey();
    }
    private void RegisterCaptureRegionHotkey(HotkeyGesture? gesture)
    {
        if (gesture == null)
        {
            Log.Warning("Capture region hotkey gesture is null, using default");
            gesture = HotkeyGesture.FromChar(HotkeyModifiers.Alt, 'A');
        }

        var success = _hotkeyProvider.RegisterHotkey("capture_region", gesture, () =>
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
                    // Fire and forget - capture session starts asynchronously
                    _ = StartCaptureSessionAsync();
                }
                else
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        // Fire and forget - capture session starts asynchronously
                        _ = StartCaptureSessionAsync();
                    });
                }
        });

        if (success)
        {
            Log.Debug("Successfully registered capture region hotkey: {Gesture}", gesture.ToDisplayString());
        }
        else
        {
            Log.Warning("Failed to register capture region hotkey: {Gesture}", gesture.ToDisplayString());
        }
    }

    private void RegisterOpenSettingsHotkey(HotkeyGesture? gesture)
    {
        if (gesture == null)
        {
            Log.Warning("Open settings hotkey gesture is null, using default");
            gesture = HotkeyGesture.FromChar(HotkeyModifiers.Alt, 'S');
        }

        var success = _hotkeyProvider.RegisterHotkey("open_settings", gesture, () =>
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
            Log.Debug("Successfully registered open settings hotkey: {Gesture}", gesture.ToDisplayString());
        }
        else
        {
            Log.Warning("Failed to register open settings hotkey: {Gesture}", gesture.ToDisplayString());
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


    /// <summary>
    /// Register ESC hotkey for closing screenshot overlay
    /// </summary>
    public void RegisterEscapeHotkey()
    {
        if (_escHotkeyRegistered || !_hotkeyProvider.IsSupported)
            return;

        var gesture = HotkeyGesture.FromNamedKey(HotkeyModifiers.None, NamedKey.Escape);
        var success = _hotkeyProvider.RegisterHotkey("overlay_escape", gesture, () =>
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
        _layoutMonitor?.StopMonitoring();
        _layoutMonitor?.Dispose();
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

    public bool RegisterHotkey(string id, HotkeyGesture gesture, Action callback)
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
