using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.ElementDetection;
using SelectionMode = AGI.Kapster.Desktop.Overlays.Layers.Selection.SelectionMode;

namespace AGI.Kapster.Desktop.Services.Overlay.State;

/// <summary>
/// Scoped overlay session for a single screenshot operation
/// Central coordinator for window lifecycle, selection state, element highlighting, and selection mode
/// Automatically cleans up state on disposal
/// </summary>
public class OverlaySession : IOverlaySession
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<Window> _windows = new();
    private readonly object _lock = new();
    private bool _disposed = false;
    
    // Selection State
    private bool _hasSelection = false;
    private object? _activeSelectionWindow = null;
    
    // Element Highlight State (replaces GlobalElementHighlightState)
    private DetectedElement? _currentHighlightedElement = null;
    private object? _currentHighlightOwner = null;
    
    // Selection Mode State (session-level)
    private SelectionMode _currentSelectionMode = SelectionMode.Free;

    public event Action<bool>? SelectionStateChanged;
    public event Action<SelectionMode>? SelectionModeChanged;
    
    // Unified event for all windows in this session
    public event EventHandler<RegionSelectedEventArgs>? RegionSelected;

    public OverlaySession(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IReadOnlyList<Window> Windows
    {
        get
        {
            lock (_lock)
            {
                return _windows.AsReadOnly();
            }
        }
    }

    public bool HasSelection
    {
        get
        {
            lock (_lock)
            {
                return _hasSelection;
            }
        }
    }

    public object? ActiveSelectionWindow
    {
        get
        {
            lock (_lock)
            {
                return _activeSelectionWindow;
            }
        }
    }

    public IOverlayWindowBuilder CreateWindowBuilder()
    {
        ThrowIfDisposed();
        return new OverlayWindowBuilder(this, _serviceProvider);
    }

    public void AddWindow(Window window)
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            _windows.Add(window);
            Log.Debug("[OverlaySession] Window added, total: {Count}", _windows.Count);
        }
    }
    
    /// <summary>
    /// Internal: Called by OverlayWindowBuilder to notify region selection
    /// Forwards to session-level event
    /// </summary>
    internal void NotifyRegionSelected(object? sender, RegionSelectedEventArgs e)
    {
        RegionSelected?.Invoke(sender, e);
    }

    public event Action? Closed;
    private bool _isClosing = false;
    
    /// <summary>
    /// Close this session and all associated windows
    /// Unified cleanup entry point - calling Close() on any window closes all windows
    /// </summary>
    public void Close()
    {
        if (_disposed || _isClosing) return;
        
        _isClosing = true;
        
        try
        {
            Window[] windowsToClose;
            lock (_lock)
            {
                windowsToClose = _windows.ToArray();
                _windows.Clear();
                ClearSelectionInternal();
            }
            
            Log.Debug("[OverlaySession] Closing session with {Count} window(s)", windowsToClose.Length);
            
            // Close all windows
            foreach (var window in windowsToClose)
            {
                try
                {
                    window.Close();
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "[OverlaySession] Error closing window");
                }
            }
            
            Log.Debug("[OverlaySession] Session closed, firing Closed event");
            
            // Fire event to notify ScreenshotService
            Closed?.Invoke();
        }
        finally
        {
            _isClosing = false;
        }
    }

    public void ShowAll()
    {
        ThrowIfDisposed();
        
        Window[] windowsToShow;
        lock (_lock)
        {
            windowsToShow = _windows.ToArray();
        }
        
        foreach (var window in windowsToShow)
        {
            try
            {
                window.Show();
                Log.Debug("[OverlaySession] Window shown");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[OverlaySession] Failed to show window");
            }
        }
    }

    public bool CanStartSelection(object window)
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            return !_hasSelection || _activeSelectionWindow == window;
        }
    }

    public void SetSelection(object window)
    {
        ThrowIfDisposed();
        
        Action<bool>? handlerCopy = null;
        
        lock (_lock)
        {
            if (_hasSelection && _activeSelectionWindow == window)
                return; // No change

            _hasSelection = true;
            _activeSelectionWindow = window;
            
            // Capture event handler inside lock to prevent race with Dispose()
            handlerCopy = SelectionStateChanged;
            
            Log.Debug("[OverlaySession] Selection set for window");
        }
        
        // Invoke event outside lock to avoid deadlocks
        // Using captured handler ensures thread safety even if Dispose() runs concurrently
        handlerCopy?.Invoke(true);
    }

    public void ClearSelection(object? window = null)
    {
        if (_disposed) return;
        
        Action<bool>? handlerCopy = null;
        
        lock (_lock)
        {
            // If a specific window is provided, only clear if it's the active one
            if (window != null && _activeSelectionWindow != window)
                return;

            if (_hasSelection)
            {
                ClearSelectionInternal();
                
                // Capture event handler inside lock to prevent race with Dispose()
                handlerCopy = SelectionStateChanged;
            }
        }
        
        // Invoke event outside lock to avoid deadlocks
        // Using captured handler ensures thread safety even if Dispose() runs concurrently
        handlerCopy?.Invoke(false);
    }

    // ============================================================
    // Element Highlight Coordination Implementation
    // ============================================================
    
    public bool SetHighlightedElement(DetectedElement? element, object owner)
    {
        ThrowIfDisposed();
        
        lock (_lock)
        {
            if (element == null)
            {
                // Clear highlight from this owner
                if (_currentHighlightOwner == owner)
                {
                    _currentHighlightedElement = null;
                    _currentHighlightOwner = null;
                    Log.Debug("[OverlaySession] Cleared element highlight from owner: {Owner}", owner.GetHashCode());
                    return true; // Owner should clear its highlight
                }
                return false; // No change needed for non-owners
            }

            // Check if this is the same element (avoid unnecessary updates)
            if (_currentHighlightedElement != null && AreElementsEqual(_currentHighlightedElement, element))
            {
                // Same element, but check if this is a different owner trying to highlight
                if (_currentHighlightOwner != owner)
                {
                    Log.Debug("[OverlaySession] Different overlay tried to highlight same element - blocking. Current owner: {Current}, New owner: {New}",
                        _currentHighlightOwner?.GetHashCode(), owner.GetHashCode());
                    return false; // Block this owner from showing highlight
                }
                return true; // Same owner, same element - continue showing
            }

            // New element - check if someone else owns the highlight
            if (_currentHighlightOwner != null && _currentHighlightOwner != owner)
            {
                Log.Debug("[OverlaySession] New element from different owner - taking over highlight. Previous: {Previous}, New: {New}",
                    _currentHighlightOwner.GetHashCode(), owner.GetHashCode());
            }

            // Update session state
            _currentHighlightedElement = element;
            _currentHighlightOwner = owner;

            Log.Debug("[OverlaySession] Updated element highlight: {Name} ({ClassName}) by owner {Owner}",
                element.Name, element.ClassName, owner.GetHashCode());

            return true; // This owner should show the highlight
        }
    }
    
    public bool IsHighlightOwner(object owner)
    {
        lock (_lock)
        {
            return _currentHighlightOwner == owner;
        }
    }
    
    public DetectedElement? CurrentHighlightedElement
    {
        get
        {
            lock (_lock)
            {
                return _currentHighlightedElement;
            }
        }
    }
    
    public void ClearHighlightOwner(object owner)
    {
        if (_disposed) return;
        
        lock (_lock)
        {
            if (_currentHighlightOwner == owner)
            {
                _currentHighlightedElement = null;
                _currentHighlightOwner = null;
                Log.Debug("[OverlaySession] Cleared highlight owner: {Owner}", owner.GetHashCode());
            }
        }
    }
    
    private static bool AreElementsEqual(DetectedElement a, DetectedElement b)
    {
        return a.WindowHandle == b.WindowHandle &&
               a.ClassName == b.ClassName &&
               Math.Abs(a.Bounds.X - b.Bounds.X) < 5 &&
               Math.Abs(a.Bounds.Y - b.Bounds.Y) < 5 &&
               Math.Abs(a.Bounds.Width - b.Bounds.Width) < 5 &&
               Math.Abs(a.Bounds.Height - b.Bounds.Height) < 5;
    }
    
    // ============================================================
    // Selection Mode Management Implementation
    // ============================================================
    
    public SelectionMode CurrentSelectionMode
    {
        get
        {
            lock (_lock)
            {
                return _currentSelectionMode;
            }
        }
        set
        {
            ThrowIfDisposed();
            
            Action<SelectionMode>? handlerCopy = null;
            SelectionMode newMode;
            
            lock (_lock)
            {
                if (_currentSelectionMode == value)
                    return; // No change
                
                var oldMode = _currentSelectionMode;
                _currentSelectionMode = value;
                newMode = value;
                
                // Capture event handler inside lock
                handlerCopy = SelectionModeChanged;
                
                Log.Debug("[OverlaySession] Selection mode changed: {OldMode} -> {NewMode}", oldMode, newMode);
            }
            
            // Invoke event outside lock to avoid deadlocks
            handlerCopy?.Invoke(newMode);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Atomically clear event handlers to prevent race conditions
        // This ensures no new handlers are invoked after disposal starts
        Interlocked.Exchange(ref SelectionStateChanged, null);
        Interlocked.Exchange(ref SelectionModeChanged, null);

        // Close all windows first
        Close();

        lock (_lock)
        {
            _disposed = true;
            _hasSelection = false;
            _activeSelectionWindow = null;
            _currentHighlightedElement = null;
            _currentHighlightOwner = null;
            _currentSelectionMode = SelectionMode.Free;
            
            Log.Debug("[OverlaySession] Disposed");
        }
    }

    private void ClearSelectionInternal()
    {
        _hasSelection = false;
        _activeSelectionWindow = null;
        Log.Debug("[OverlaySession] Selection cleared");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(OverlaySession));
        }
    }
}

