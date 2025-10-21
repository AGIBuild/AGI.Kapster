using System;
using Avalonia.Controls;

namespace AGI.Kapster.Desktop.Services.Overlay.State;

/// <summary>
/// Represents a single screenshot session with scoped state management
/// Each ShowAll() creates a new session, automatically cleaned up on Dispose
/// </summary>
public interface IOverlaySession : IDisposable
{
    /// <summary>
    /// Register a window in this session
    /// </summary>
    void RegisterWindow(Window window);

    /// <summary>
    /// Unregister a window from this session
    /// </summary>
    void UnregisterWindow(Window window);

    /// <summary>
    /// Check if a window can start a new selection
    /// </summary>
    bool CanStartSelection(object window);

    /// <summary>
    /// Set selection for a window
    /// </summary>
    void SetSelection(object window);

    /// <summary>
    /// Clear selection (optionally for specific window)
    /// </summary>
    void ClearSelection(object? window = null);

    /// <summary>
    /// Event fired when selection state changes
    /// </summary>
    event Action<bool>? SelectionStateChanged;

    /// <summary>
    /// Gets whether any window in this session has a selection
    /// </summary>
    bool HasSelection { get; }

    /// <summary>
    /// Gets the window that currently has the active selection
    /// </summary>
    object? ActiveSelectionWindow { get; }
}

