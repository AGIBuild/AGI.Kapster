using Avalonia.Controls;
using System.Collections.Generic;
using System.Linq;

namespace AGI.Kapster.Desktop.Services.Overlay.State;

/// <summary>
/// Unified global overlay state management
/// Replaces GlobalSelectionState and GlobalElementHighlightState
/// </summary>
public static class OverlayState
{
    private static readonly HashSet<Window> _activeOverlayWindows = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Register an active overlay window
    /// </summary>
    public static void RegisterWindow(Window window)
    {
        lock (_lock)
        {
            _activeOverlayWindows.Add(window);
        }
    }

    /// <summary>
    /// Unregister an overlay window
    /// </summary>
    public static void UnregisterWindow(Window window)
    {
        lock (_lock)
        {
            _activeOverlayWindows.Remove(window);
        }
    }

    /// <summary>
    /// Check if a window is registered as an active overlay
    /// </summary>
    public static bool IsRegistered(Window window)
    {
        lock (_lock)
        {
            return _activeOverlayWindows.Contains(window);
        }
    }

    /// <summary>
    /// Get all active overlay windows
    /// </summary>
    public static IReadOnlyList<Window> GetActiveWindows()
    {
        lock (_lock)
        {
            return _activeOverlayWindows.ToList();
        }
    }

    /// <summary>
    /// Clear all registered overlay windows
    /// </summary>
    public static void ClearAll()
    {
        lock (_lock)
        {
            _activeOverlayWindows.Clear();
        }
    }

    /// <summary>
    /// Get count of active overlay windows
    /// </summary>
    public static int Count
    {
        get
        {
            lock (_lock)
            {
                return _activeOverlayWindows.Count;
            }
        }
    }
}


