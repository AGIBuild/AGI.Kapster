using System;
using System.Collections.Generic;
using Avalonia.Controls;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.ElementDetection;
using SelectionMode = AGI.Kapster.Desktop.Overlays.Layers.Selection.SelectionMode;

namespace AGI.Kapster.Desktop.Services.Overlay.State;

/// <summary>
/// Represents a single screenshot session with scoped state management
/// Session owns and manages all overlay windows created during the screenshot operation
/// Acts as the central coordinator for:
/// - Window lifecycle management (multi-screen support)
/// - Selection state coordination (preventing conflicts between windows)
/// - Element highlight coordination (single highlight across all windows)
/// - Session-level selection mode (Free vs Element detection)
/// Automatically cleaned up on Dispose
/// </summary>
public interface IOverlaySession : IDisposable
{
    // ============================================================
    // Window Lifecycle Management
    // ============================================================
    
    /// <summary>
    /// Create a builder for adding a window to this session
    /// Returns a fluent builder for window configuration
    /// </summary>
    IOverlayWindowBuilder CreateWindowBuilder();
    
    /// <summary>
    /// Add a window to this session (establishes ownership)
    /// Internal: Only called by OverlayWindowBuilder during Build()
    /// </summary>
    void AddWindow(Window window);
    
    /// <summary>
    /// Event raised when any window in this session reports region selection
    /// Unified event handler for all windows in the session
    /// </summary>
    event EventHandler<RegionSelectedEventArgs>? RegionSelected;

    /// <summary>
    /// Get all windows in this session
    /// </summary>
    IReadOnlyList<Window> Windows { get; }

    /// <summary>
    /// Show all windows in this session
    /// </summary>
    void ShowAll();

    /// <summary>
    /// Close this session and all associated windows
    /// This is the unified cleanup entry point
    /// </summary>
    void Close();
    
    /// <summary>
    /// Event fired when the session is closed (all windows closed)
    /// Used by ScreenshotService to clean up _currentSession
    /// </summary>
    event Action? Closed;

    // ============================================================
    // Selection State Coordination (Multi-Window)
    // ============================================================
    
    /// <summary>
    /// Check if a window can start a new selection
    /// Prevents multiple windows from having selections simultaneously
    /// </summary>
    bool CanStartSelection(object window);

    /// <summary>
    /// Set selection for a window (marks this window as having active selection)
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

    // ============================================================
    // Element Highlight Coordination (Multi-Window)
    // Replaces GlobalElementHighlightState singleton
    // ============================================================
    
    /// <summary>
    /// Sets the currently highlighted element for a window
    /// Only allows one highlight across all windows in the session
    /// </summary>
    /// <param name="element">Element to highlight (null to clear)</param>
    /// <param name="owner">The overlay window requesting the highlight</param>
    /// <returns>True if this owner should show the highlight, false if another window owns it</returns>
    bool SetHighlightedElement(DetectedElement? element, object owner);
    
    /// <summary>
    /// Checks if the given window currently owns the element highlight
    /// </summary>
    bool IsHighlightOwner(object owner);
    
    /// <summary>
    /// Gets the currently highlighted element (across all windows in session)
    /// </summary>
    DetectedElement? CurrentHighlightedElement { get; }
    
    /// <summary>
    /// Clears any highlight owned by the specified window
    /// </summary>
    void ClearHighlightOwner(object owner);

    // ============================================================
    // Session-Level Selection Mode
    // Unified mode across all windows in the session
    // ============================================================
    
    /// <summary>
    /// Current selection mode for this session (Free vs Element detection)
    /// When user presses Ctrl, all windows in session switch to Element mode together
    /// </summary>
    SelectionMode CurrentSelectionMode { get; set; }
    
    /// <summary>
    /// Event fired when selection mode changes
    /// All windows in session should respond to this event
    /// </summary>
    event Action<SelectionMode>? SelectionModeChanged;
}

