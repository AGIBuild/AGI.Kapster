using AGI.Kapster.Desktop.Overlays.Layers;

namespace AGI.Kapster.Desktop.Overlays.Events;

/// <summary>
/// Event published when a layer requests the overlay manager to switch modes
/// Used to synchronize SelectionMode (session-level) with OverlayMode (window-level)
/// </summary>
public record OverlayModeChangeRequestedEvent(OverlayMode RequestedMode) : IOverlayEvent;

