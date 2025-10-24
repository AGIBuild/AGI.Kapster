using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace AGI.Kapster.Desktop.Overlays;

/// <summary>
/// Simple host canvas to attach layer visuals and manage Z-order.
/// </summary>
public class LayerHost : Canvas, ILayerHost
{
    public LayerHost()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        ClipToBounds = true;
        Background = null;
        IsHitTestVisible = true;
    }

    public void Attach(Control visual, int zIndex)
    {
        if (!Children.Contains(visual))
        {
            Children.Add(visual);
        }
        SetZIndex(visual, zIndex);
    }

    public void Detach(Control visual)
    {
        if (Children.Contains(visual))
        {
            Children.Remove(visual);
        }
    }

    public void SetZIndex(Control visual, int zIndex)
    {
        visual.ZIndex = zIndex;
    }
}
