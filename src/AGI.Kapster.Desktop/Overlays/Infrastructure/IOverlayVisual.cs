namespace AGI.Kapster.Desktop.Overlays;

/// <summary>
/// Implemented by layers that own a visual and can attach/detach to a host.
/// </summary>
public interface IOverlayVisual
{
    void AttachTo(ILayerHost host, IOverlayContext context);
    void Detach();
}
