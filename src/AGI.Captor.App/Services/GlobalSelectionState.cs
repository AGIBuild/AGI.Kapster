using System;

namespace AGI.Captor.App.Services;

/// <summary>
/// Global state manager for selection across multiple overlay windows
/// </summary>
public static class GlobalSelectionState
{
    private static bool _hasSelection = false;
    private static object? _activeSelectionWindow = null;

    /// <summary>
    /// Event fired when selection state changes
    /// </summary>
    public static event Action<bool>? SelectionStateChanged;

    /// <summary>
    /// Gets whether any overlay window currently has an active selection
    /// </summary>
    public static bool HasSelection => _hasSelection;

    /// <summary>
    /// Gets the window that currently has the active selection
    /// </summary>
    public static object? ActiveSelectionWindow => _activeSelectionWindow;

    /// <summary>
    /// Sets that a window has started a selection
    /// </summary>
    /// <param name="window">The window that has the selection</param>
    public static void SetSelection(object window)
    {
        if (_hasSelection && _activeSelectionWindow == window)
            return; // No change

        _hasSelection = true;
        _activeSelectionWindow = window;
        SelectionStateChanged?.Invoke(true);
    }

    /// <summary>
    /// Clears the selection state
    /// </summary>
    /// <param name="window">The window clearing the selection (optional, for validation)</param>
    public static void ClearSelection(object? window = null)
    {
        // If a specific window is provided, only clear if it's the active one
        if (window != null && _activeSelectionWindow != window)
            return;

        if (!_hasSelection)
            return; // No change

        _hasSelection = false;
        _activeSelectionWindow = null;
        SelectionStateChanged?.Invoke(false);
    }

    /// <summary>
    /// Checks if the given window is allowed to start a new selection
    /// </summary>
    /// <param name="window">The window requesting to start selection</param>
    /// <returns>True if allowed, false if another window has selection</returns>
    public static bool CanStartSelection(object window)
    {
        return !_hasSelection || _activeSelectionWindow == window;
    }
}
