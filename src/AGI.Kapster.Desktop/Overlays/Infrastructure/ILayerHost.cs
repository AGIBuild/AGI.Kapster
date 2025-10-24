using Avalonia.Controls;

namespace AGI.Kapster.Desktop.Overlays;

/// <summary>
/// Host surface that manages visual children and their z-order for layers.
/// </summary>
public interface ILayerHost
{
    void Attach(Control visual, int zIndex);
    void Detach(Control visual);
    void SetZIndex(Control visual, int zIndex);
}
