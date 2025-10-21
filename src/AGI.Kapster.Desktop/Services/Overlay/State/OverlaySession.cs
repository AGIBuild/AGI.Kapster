using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Overlay.State;

/// <summary>
/// Scoped overlay session for a single screenshot operation
/// Automatically cleans up state on disposal
/// </summary>
public class OverlaySession : IOverlaySession
{
    private readonly List<Window> _windows = new();
    private bool _hasSelection = false;
    private object? _activeSelectionWindow = null;
    private readonly object _lock = new();
    private bool _disposed = false;

    public event Action<bool>? SelectionStateChanged;

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

    public void AddWindow(Window window)
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            _windows.Add(window);
            Log.Debug("[OverlaySession] Window added, total: {Count}", _windows.Count);
        }
    }

    public void RemoveWindow(Window window)
    {
        if (_disposed) return;

        lock (_lock)
        {
            _windows.Remove(window);
            
            // Clear selection if this window has it
            if (_activeSelectionWindow == window)
            {
                ClearSelectionInternal();
            }
            
            Log.Debug("[OverlaySession] Window removed, remaining: {Count}", _windows.Count);
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

    public void CloseAll()
    {
        if (_disposed) return;
        
        Window[] windowsToClose;
        lock (_lock)
        {
            windowsToClose = _windows.ToArray();
        }
        
        foreach (var window in windowsToClose)
        {
            try
            {
                window.Close();
                Log.Debug("[OverlaySession] Window closed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[OverlaySession] Failed to close window");
            }
        }
        
        lock (_lock)
        {
            _windows.Clear();
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
        
        bool shouldInvoke = false;
        
        lock (_lock)
        {
            if (_hasSelection && _activeSelectionWindow == window)
                return; // No change

            _hasSelection = true;
            _activeSelectionWindow = window;
            shouldInvoke = true;
            
            Log.Debug("[OverlaySession] Selection set for window");
        }
        
        // Invoke event outside lock to avoid deadlocks
        if (shouldInvoke)
        {
            SelectionStateChanged?.Invoke(true);
        }
    }

    public void ClearSelection(object? window = null)
    {
        if (_disposed) return;
        
        bool shouldInvoke = false;
        
        lock (_lock)
        {
            // If a specific window is provided, only clear if it's the active one
            if (window != null && _activeSelectionWindow != window)
                return;

            if (_hasSelection)
            {
                ClearSelectionInternal();
                shouldInvoke = true;
            }
        }
        
        // Invoke event outside lock to avoid deadlocks
        if (shouldInvoke)
        {
            SelectionStateChanged?.Invoke(false);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Close all windows first
        CloseAll();

        lock (_lock)
        {
            _disposed = true;
            _hasSelection = false;
            _activeSelectionWindow = null;
            SelectionStateChanged = null;
            
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

