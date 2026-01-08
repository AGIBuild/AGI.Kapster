using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using AGI.Kapster.Desktop.Overlays;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Overlay.State;

/// <summary>
/// Scoped overlay session for a single screenshot operation
/// Manages window lifecycle and forwards window events to subscribers
/// Automatically cleans up on disposal
/// </summary>
public class OverlaySession : IOverlaySession
{
    private readonly List<Window> _windows = new();
    private readonly object _lock = new();
    private bool _disposed = false;

    // Event forwarding (session aggregates all window events)
    public event Action<RegionSelectedEventArgs>? RegionSelected;
    public event Action<OverlayCancelledEventArgs>? Cancelled;

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

    public void AddWindow(Window window)
    {
        ThrowIfDisposed();
        
        lock (_lock)
        {
            _windows.Add(window);
            Log.Debug("[OverlaySession] Window added, total: {Count}", _windows.Count);
        }
        
        // Subscribe to window events (outside lock to prevent deadlock)
        SubscribeToWindowEvents(window);
    }

    public void RemoveWindow(Window window)
    {
        if (_disposed) return;

        // Unsubscribe from window events (before removing from list)
        UnsubscribeFromWindowEvents(window);
        
        lock (_lock)
        {
            _windows.Remove(window);
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

    public void Dispose()
    {
        if (_disposed) return;

        // Clear event handlers to prevent race conditions
        RegionSelected = null;
        Cancelled = null;

        // Unsubscribe from all windows
        Window[] windowsToCleanup;
        lock (_lock)
        {
            windowsToCleanup = _windows.ToArray();
        }
        
        foreach (var window in windowsToCleanup)
        {
            UnsubscribeFromWindowEvents(window);
        }

        // Close all windows
        CloseAll();

        lock (_lock)
        {
            _disposed = true;
            Log.Debug("[OverlaySession] Disposed");
        }
    }
    
    /// <summary>
    /// Subscribe to window events for event forwarding
    /// </summary>
    private void SubscribeToWindowEvents(Window window)
    {
        if (window is IOverlayWindow overlayWindow)
        {
            overlayWindow.RegionSelected += OnWindowRegionSelected;
            overlayWindow.Cancelled += OnWindowCancelled;
            
            Log.Debug("[OverlaySession] Subscribed to window events");
        }
    }
    
    /// <summary>
    /// Unsubscribe from window events
    /// </summary>
    private void UnsubscribeFromWindowEvents(Window window)
    {
        if (window is IOverlayWindow overlayWindow)
        {
            overlayWindow.RegionSelected -= OnWindowRegionSelected;
            overlayWindow.Cancelled -= OnWindowCancelled;
            
            Log.Debug("[OverlaySession] Unsubscribed from window events");
        }
    }
    
    /// <summary>
    /// Forward RegionSelected event from window to session subscribers
    /// Also locks selection on other windows when an editable selection is created
    /// </summary>
    private void OnWindowRegionSelected(object? sender, RegionSelectedEventArgs e)
    {
        // When a selection is completed (editable), lock all other windows to prevent new selections
        if (e.IsEditableSelection && !_disposed)
        {
            LockOtherWindows(sender as Window);
        }

        // Capture event handler to prevent race with Dispose()
        var handler = RegionSelected;
        if (handler != null)
        {
            handler.Invoke(e);
            Log.Debug("[OverlaySession] RegionSelected event forwarded (IsEditable: {IsEditable})", e.IsEditableSelection);
        }
    }
    
    /// <summary>
    /// Forward Cancelled event from window to session subscribers
    /// </summary>
    private void OnWindowCancelled(object? sender, OverlayCancelledEventArgs e)
    {
        // Capture event handler to prevent race with Dispose()
        var handler = Cancelled;
        if (handler != null)
        {
            handler.Invoke(e);
            Log.Debug("[OverlaySession] Cancelled event forwarded");
        }
    }

    /// <summary>
    /// Lock selection capability on all windows except the specified one
    /// This ensures only one selection exists at any time across all screens
    /// </summary>
    private void LockOtherWindows(Window? activeWindow)
    {
        Window[] allWindows;
        lock (_lock)
        {
            allWindows = _windows.ToArray();
        }

        foreach (var window in allWindows)
        {
            if (window is IOverlayWindow overlayWindow)
            {
                try
                {
                    // Lock all windows except the one with active selection
                    var shouldLock = window != activeWindow;
                    overlayWindow.SetSelectionLocked(shouldLock);
                    Log.Debug("[OverlaySession] Window selection locked: {Locked}", shouldLock);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[OverlaySession] Failed to set selection lock on window");
                }
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(OverlaySession));
        }
    }
}

