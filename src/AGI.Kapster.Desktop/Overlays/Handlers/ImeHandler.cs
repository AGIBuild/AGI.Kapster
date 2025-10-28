using AGI.Kapster.Desktop.Services.Input;
using Avalonia.Controls;
using Serilog;
using System;

namespace AGI.Kapster.Desktop.Overlays.Handlers;

/// <summary>
/// Manages IME (Input Method Editor) lifecycle for overlay window
/// Disables IME during overlay display to prevent interference with keyboard shortcuts
/// Enables IME during text annotation editing
/// </summary>
internal sealed class ImeHandler
{
    private readonly Window _window;
    private readonly IImeController _imeController;

    public ImeHandler(Window window, IImeController imeController)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _imeController = imeController ?? throw new ArgumentNullException(nameof(imeController));
    }

    /// <summary>
    /// Disable IME for overlay window to prevent input method interference with shortcuts
    /// </summary>
    public void DisableIme()
    {
        if (!_imeController.IsSupported)
            return;

        try
        {
            var handle = _window.TryGetPlatformHandle()?.Handle ?? nint.Zero;
            if (handle != nint.Zero)
            {
                _imeController.DisableIme(handle);
                Log.Debug("IME disabled for overlay window");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to disable IME for overlay window");
        }
    }

    /// <summary>
    /// Enable IME for overlay window (called when text editing starts)
    /// </summary>
    public void EnableIme()
    {
        if (!_imeController.IsSupported)
            return;

        try
        {
            var handle = _window.TryGetPlatformHandle()?.Handle ?? nint.Zero;
            if (handle != nint.Zero)
            {
                _imeController.EnableIme(handle);
                Log.Debug("IME enabled for overlay window");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to enable IME for overlay window");
        }
    }
}
