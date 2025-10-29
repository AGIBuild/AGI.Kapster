using System;
using System.Collections.Generic;
using Avalonia.Controls;
using AGI.Kapster.Desktop.Overlays;

namespace AGI.Kapster.Desktop.Services.Overlay.State;

/// <summary>
/// Represents a single screenshot session - manages overlay window lifecycle and events
/// Session owns and manages all overlay windows created during the screenshot operation
/// Automatically cleaned up on Dispose
/// </summary>
public interface IOverlaySession : IDisposable
{
    // --- Window Lifecycle Management ---
    
    /// <summary>
    /// Add a window to this session (establishes ownership and subscribes to events)
    /// </summary>
    void AddWindow(Window window);

    /// <summary>
    /// Remove a window from this session (unsubscribes from events)
    /// </summary>
    void RemoveWindow(Window window);

    /// <summary>
    /// Get all windows in this session
    /// </summary>
    IReadOnlyList<Window> Windows { get; }

    /// <summary>
    /// Show all windows in this session
    /// </summary>
    void ShowAll();

    /// <summary>
    /// Close all windows in this session
    /// </summary>
    void CloseAll();
    
    // --- Event Forwarding (Session aggregates all window events) ---
    
    /// <summary>
    /// Fired when any window in this session completes a region selection
    /// </summary>
    event Action<RegionSelectedEventArgs>? RegionSelected;
    
    /// <summary>
    /// Fired when any window in this session is cancelled
    /// </summary>
    event Action<OverlayCancelledEventArgs>? Cancelled;
}

