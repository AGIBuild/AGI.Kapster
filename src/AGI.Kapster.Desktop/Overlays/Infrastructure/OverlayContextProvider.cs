using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AGI.Kapster.Desktop.Services.Input;
using Serilog;

namespace AGI.Kapster.Desktop.Overlays.Infrastructure;

/// <summary>
/// Provides and updates IOverlayContext from window state
/// Owns frozen background lifetime
/// </summary>
public class OverlayContextProvider : IDisposable
{
    private readonly IImeController _imeController;
    private Bitmap? _frozenBackground;
    private IReadOnlyList<Screen>? _screens;

    /// <summary>
    /// Get the frozen background bitmap for image capture
    /// </summary>
    public Bitmap? FrozenBackground => _frozenBackground;

    public OverlayContextProvider(IImeController imeController)
    {
        _imeController = imeController ?? throw new ArgumentNullException(nameof(imeController));
    }

    /// <summary>
    /// Build initial context from window and environment
    /// </summary>
    public IOverlayContext BuildContext(
        TopLevel window,
        Bitmap? frozenBackground,
        IReadOnlyList<Screen>? screens)
    {
        _frozenBackground = frozenBackground;
        _screens = screens;

        var context = new OverlayContext(
            overlaySize: window.Bounds.Size,
            overlayPosition: window is Window w ? w.Position : new PixelPoint(0, 0),
            screens: screens ?? Array.Empty<Screen>(),
            frozenBackground: frozenBackground,
            ime: _imeController,
            dispatcher: Avalonia.Threading.Dispatcher.UIThread);

        Log.Debug("OverlayContextProvider: Built context");
        return context;
    }

    /// <summary>
    /// Update context from current window state
    /// </summary>
    public IOverlayContext UpdateContext(Size overlaySize, PixelPoint overlayPosition, IReadOnlyList<Screen>? screens)
    {
        if (screens != null)
        {
            _screens = screens;
        }

        var context = new OverlayContext(
            overlaySize: overlaySize,
            overlayPosition: overlayPosition,
            screens: _screens ?? Array.Empty<Screen>(),
            frozenBackground: _frozenBackground,
            ime: _imeController,
            dispatcher: Avalonia.Threading.Dispatcher.UIThread);

        Log.Debug("OverlayContextProvider: Updated context");
        return context;
    }

    /// <summary>
    /// Set frozen background
    /// </summary>
    public void SetFrozenBackground(Bitmap? background)
    {
        _frozenBackground = background;
    }

    /// <summary>
    /// Set screens
    /// </summary>
    public void SetScreens(IReadOnlyList<Screen>? screens)
    {
        _screens = screens;
    }

    public void Dispose()
    {
        _frozenBackground?.Dispose();
        _frozenBackground = null;
        Log.Debug("OverlayContextProvider: Frozen background disposed");
    }
}

