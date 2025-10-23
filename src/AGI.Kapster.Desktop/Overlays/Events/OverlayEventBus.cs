using System;
using System.Collections.Generic;
using Serilog;

namespace AGI.Kapster.Desktop.Overlays.Events;

/// <summary>
/// Simple in-memory event bus implementation for overlay layer communication
/// Thread-safe for UI thread usage
/// </summary>
public class OverlayEventBus : IOverlayEventBus
{
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();
    private readonly object _lock = new();

    public void Publish<T>(T eventData) where T : IOverlayEvent
    {
        if (eventData == null)
            throw new ArgumentNullException(nameof(eventData));

        List<Delegate>? handlers;
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(typeof(T), out handlers))
                return;

            // Create a copy to avoid modification during iteration
            handlers = new List<Delegate>(handlers);
        }

        foreach (var handler in handlers)
        {
            try
            {
                ((Action<T>)handler)(eventData);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error in event handler for {EventType}", typeof(T).Name);
            }
        }

        Log.Debug("Event published: {EventType}", typeof(T).Name);
    }

    public void Subscribe<T>(Action<T> handler) where T : IOverlayEvent
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        lock (_lock)
        {
            if (!_subscribers.TryGetValue(typeof(T), out var handlers))
            {
                handlers = new List<Delegate>();
                _subscribers[typeof(T)] = handlers;
            }

            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
                Log.Debug("Subscribed to {EventType}", typeof(T).Name);
            }
        }
    }

    public void Unsubscribe<T>(Action<T> handler) where T : IOverlayEvent
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        lock (_lock)
        {
            if (_subscribers.TryGetValue(typeof(T), out var handlers))
            {
                handlers.Remove(handler);
                Log.Debug("Unsubscribed from {EventType}", typeof(T).Name);

                if (handlers.Count == 0)
                {
                    _subscribers.Remove(typeof(T));
                }
            }
        }
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            _subscribers.Clear();
            Log.Debug("All event subscriptions cleared");
        }
    }
}

