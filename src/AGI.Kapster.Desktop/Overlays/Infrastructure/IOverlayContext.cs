using System.Collections.Generic;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AGI.Kapster.Desktop.Services.Input;

namespace AGI.Kapster.Desktop.Overlays;

/// <summary>
/// Read-only context exposed to overlay layers for window-related data.
/// </summary>
public interface IOverlayContext
{
    /// <summary>
    /// Current overlay logical size.
    /// </summary>
    Size OverlaySize { get; }

    /// <summary>
    /// Current overlay window position (pixels).
    /// </summary>
    PixelPoint OverlayPosition { get; }

    /// <summary>
    /// Available screens for positioning logic.
    /// </summary>
    IReadOnlyList<Screen> Screens { get; }

    /// <summary>
    /// Frozen background for capture composition.
    /// </summary>
    Bitmap? FrozenBackground { get; }

    /// <summary>
    /// IME controller for text input lifecycle.
    /// </summary>
    IImeController Ime { get; }

    /// <summary>
    /// UI thread dispatcher.
    /// </summary>
    Avalonia.Threading.Dispatcher Dispatcher { get; }
}
