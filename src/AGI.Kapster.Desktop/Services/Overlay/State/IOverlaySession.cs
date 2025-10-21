using System;
using System.Collections.Generic;
using Avalonia.Controls;

namespace AGI.Kapster.Desktop.Services.Overlay.State;

/// <summary>
/// Represents a single screenshot session with scoped state management
/// Session owns and manages all overlay windows created during the screenshot operation
/// Automatically cleaned up on Dispose
/// </summary>
public interface IOverlaySession : IDisposable
{
    /// <summary>
    /// Add a window to this session (establishes ownership)
    /// </summary>
    void AddWindow(Window window);

    /// <summary>
    /// Remove a window from this session
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

