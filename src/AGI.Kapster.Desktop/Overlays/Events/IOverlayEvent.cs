using Avalonia;
using AGI.Kapster.Desktop.Overlays.Layers;
using AGI.Kapster.Desktop.Services.ElementDetection;

namespace AGI.Kapster.Desktop.Overlays.Events;

/// <summary>
/// Base interface for all overlay events
/// </summary>
public interface IOverlayEvent
{
}

/// <summary>
/// Event raised when overlay mode changes
/// </summary>
public record ModeChangedEvent(OverlayMode OldMode, OverlayMode NewMode) : IOverlayEvent;

/// <summary>
/// Event raised when selection rect changes
/// </summary>
public record SelectionChangedEvent(Rect Selection) : IOverlayEvent;

/// <summary>
/// Event raised when selection is confirmed
/// </summary>
public record SelectionConfirmedEvent(Rect Selection, DetectedElement? Element) : IOverlayEvent;

/// <summary>
/// Event raised when element is highlighted
/// </summary>
public record ElementHighlightedEvent(DetectedElement? Element, Rect HighlightRect) : IOverlayEvent;

/// <summary>
/// Event raised when mask cutout changes
/// </summary>
public record CutoutChangedEvent(Rect OldCutout, Rect NewCutout) : IOverlayEvent;

/// <summary>
/// Event raised when a layer is activated
/// </summary>
public record LayerActivatedEvent(string LayerId) : IOverlayEvent;

/// <summary>
/// Event raised when a layer is deactivated
/// </summary>
public record LayerDeactivatedEvent(string LayerId) : IOverlayEvent;

