using System;

namespace AGI.Kapster.Desktop.Overlays.Events;

/// <summary>
/// Event bus for overlay layer communication
/// Provides pub/sub pattern for decoupled layer interaction
/// </summary>
public interface IOverlayEventBus
{
    /// <summary>
    /// Publish an event to all subscribers
    /// </summary>
    void Publish<T>(T eventData) where T : IOverlayEvent;
    
    /// <summary>
    /// Subscribe to events of a specific type
    /// </summary>
    void Subscribe<T>(Action<T> handler) where T : IOverlayEvent;
    
    /// <summary>
    /// Unsubscribe from events of a specific type
    /// </summary>
    void Unsubscribe<T>(Action<T> handler) where T : IOverlayEvent;
    
    /// <summary>
    /// Clear all subscriptions
    /// </summary>
    void ClearAll();
}

