using System.Collections.Generic;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AGI.Kapster.Desktop.Services.Input;

namespace AGI.Kapster.Desktop.Overlays;

public sealed class OverlayContext : IOverlayContext
{
    public Size OverlaySize { get; }
    public PixelPoint OverlayPosition { get; }
    public IReadOnlyList<Screen> Screens { get; }
    public Bitmap? FrozenBackground { get; }
    public IImeController Ime { get; }
    public Avalonia.Threading.Dispatcher Dispatcher { get; }

    public OverlayContext(
        Size overlaySize,
        PixelPoint overlayPosition,
        IReadOnlyList<Screen> screens,
        Bitmap? frozenBackground,
        IImeController ime,
        Avalonia.Threading.Dispatcher dispatcher)
    {
        OverlaySize = overlaySize;
        OverlayPosition = overlayPosition;
        Screens = screens;
        FrozenBackground = frozenBackground;
        Ime = ime;
        Dispatcher = dispatcher;
    }
}
