using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Overlays.Infrastructure;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Services.Export.Imaging;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
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
    private readonly IOverlayOrchestrator _orchestrator;  // Session owns Orchestrator
    private readonly IScreenCaptureStrategy _captureStrategy;  // Session owns capture capability
    private readonly IScreenCoordinateMapper _coordinateMapper;  // Session owns coordinate mapping
    private readonly List<Window> _windows = new();
    private readonly List<IOverlayWindow> _subscribedWindows = new();  // Track windows for event unsubscription
    private readonly Dictionary<IOverlayWindow, IReadOnlyList<Screen>> _windowScreens = new();  // Track screens for each window
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

    public OverlaySession(
        IServiceProvider serviceProvider,
        IScreenCaptureStrategy captureStrategy,
        IScreenCoordinateMapper coordinateMapper)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _captureStrategy = captureStrategy ?? throw new ArgumentNullException(nameof(captureStrategy));
        _coordinateMapper = coordinateMapper ?? throw new ArgumentNullException(nameof(coordinateMapper));
        
        // Session creates and owns the Orchestrator
        _orchestrator = serviceProvider.GetRequiredService<IOverlayOrchestrator>();
        
        // Register callbacks so Orchestrator can notify Session without reverse dependency
        _orchestrator.OnRegionSelected = (sender, e) => NotifyRegionSelected(sender, e);
        _orchestrator.OnCancelled = (reason) => Log.Debug("[OverlaySession] Orchestrator cancelled: {Reason}", reason);
        
        Log.Debug("[OverlaySession] Created with owned Orchestrator and capture capabilities");
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
    /// Notify that a region has been selected (called by orchestrator)
    /// Forwards to session-level event for all subscribers
    /// </summary>
    public void NotifyRegionSelected(object? sender, RegionSelectedEventArgs e)
    {
        RegionSelected?.Invoke(sender, e);
    }
    
    /// <summary>
    /// Notify that a window is ready (loaded and initialized)
    /// Session will initialize the Orchestrator and subscribe to window events
    /// </summary>
    public void NotifyWindowReady(IOverlayWindow window)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));
        
        var layerHost = window.GetLayerHost();
        var maskSize = window.GetMaskSize();
        var topLevel = window.AsTopLevel();
        
        if (layerHost == null)
        {
            Log.Error("[OverlaySession] Cannot initialize Orchestrator: LayerHost is null");
            return;
        }
        
        // Get screens for this window (if available)
        _windowScreens.TryGetValue(window, out var screens);
        
        // Initialize Orchestrator with window context, session reference, and screens
        _orchestrator.Initialize(topLevel, layerHost, maskSize, this, screens);
        _orchestrator.BuildLayers();
        
        // Subscribe to window events - Session handles all event routing
        SubscribeToWindowEvents(window);

        Log.Debug("[OverlaySession] Orchestrator initialized with {ScreenCount} screens and window events subscribed", screens?.Count ?? 0);
    }
    
    /// <summary>
    /// Subscribe to window input and lifecycle events
    /// Session owns event routing logic, Window is just a UI container
    /// </summary>
    private void SubscribeToWindowEvents(IOverlayWindow window)
    {
        var topLevel = window.AsTopLevel();
        
        // Subscribe to input events with handledEventsToo:true to receive events even after child controls handle them
        // This is critical for ElementPicker mode where SelectionOverlay handles pointer events but we still need
        // to route them to ElementSelectionStrategy for element detection
        topLevel.AddHandler(InputElement.PointerPressedEvent, OnWindowPointerPressed, handledEventsToo: true);
        topLevel.AddHandler(InputElement.PointerMovedEvent, OnWindowPointerMoved, handledEventsToo: true);
        topLevel.AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, handledEventsToo: true);
        topLevel.AddHandler(InputElement.KeyUpEvent, OnWindowKeyUp, handledEventsToo: true);
        
        // Subscribe to lifecycle events
        if (window is Window w)
        {
            w.Closing += OnWindowClosing;
        }
        
        // Track window for cleanup
        _subscribedWindows.Add(window);
        
        // Focus window for keyboard events
        topLevel.Focus();
        
        Log.Debug("[OverlaySession] Subscribed to window events");
    }
    
    /// <summary>
    /// Unsubscribe from window events to prevent memory leaks
    /// </summary>
    private void UnsubscribeFromWindowEvents()
    {
        foreach (var window in _subscribedWindows)
        {
            try
            {
                var topLevel = window.AsTopLevel();
                
                // Remove input event handlers
                topLevel.RemoveHandler(InputElement.PointerPressedEvent, OnWindowPointerPressed);
                topLevel.RemoveHandler(InputElement.PointerMovedEvent, OnWindowPointerMoved);
                topLevel.RemoveHandler(InputElement.KeyDownEvent, OnWindowKeyDown);
                topLevel.RemoveHandler(InputElement.KeyUpEvent, OnWindowKeyUp);
                
                // Remove lifecycle event handlers
                if (window is Window w)
                {
                    w.Closing -= OnWindowClosing;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[OverlaySession] Error unsubscribing from window events");
            }
        }
        
        _subscribedWindows.Clear();
        Log.Debug("[OverlaySession] Unsubscribed from all window events");
    }
    
    /// <summary>
    /// Handle pointer pressed event from window
    /// </summary>
    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _orchestrator.RoutePointerEvent(e);
    }
    
    /// <summary>
    /// Handle pointer moved event from window
    /// </summary>
    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        _orchestrator.RoutePointerEvent(e);
    }
    
    /// <summary>
    /// Handle key down event from window
    /// </summary>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_orchestrator.RouteKeyEvent(e))
        {
            e.Handled = true;
        }
    }
    
    /// <summary>
    /// Handle key up event from window
    /// </summary>
    private void OnWindowKeyUp(object? sender, KeyEventArgs e)
    {
        if (_orchestrator.RouteKeyEvent(e))
        {
            e.Handled = true;
        }
    }
    
    /// <summary>
    /// Handle window closing event
    /// When any window closes, close the entire session (multi-screen support)
    /// </summary>
    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        Log.Debug("[OverlaySession] Window closing detected, closing entire session");
        Close();
    }
    
    /// <summary>
    /// Route pointer event from Window to Orchestrator
    /// </summary>
    public void RoutePointerEvent(PointerEventArgs e)
    {
        if (e == null) return;
        _orchestrator.RoutePointerEvent(e);
    }
    
    /// <summary>
    /// Route key event from Window to Orchestrator
    /// </summary>
    public bool RouteKeyEvent(KeyEventArgs e)
    {
        if (e == null) return false;
        return _orchestrator.RouteKeyEvent(e);
    }
    
    /// <summary>
    /// Set frozen background bitmap for image capture
    /// </summary>
    public void SetFrozenBackground(Bitmap? background)
    {
        _orchestrator.SetFrozenBackground(background);
        Log.Debug("[OverlaySession] Frozen background set");
    }

    /// <summary>
    /// Create window with background in a single operation
    /// Flow: Capture → Create → Set (straight line, no loops)
    /// This is the high-level API for screenshot services
    /// </summary>
    public async Task<IOverlayWindow> CreateWindowWithBackgroundAsync(
        Rect bounds, 
        IReadOnlyList<Screen> screens)
    {
        // 1. First capture background (before window creation, to avoid window appearing in screenshot)
        var background = await CaptureBackgroundAsync(bounds, screens.FirstOrDefault() ?? throw new ArgumentException("Screens list is empty"));
        
        // 2. Create window
        var window = CreateWindowBuilder()
            .WithBounds(bounds)
            .WithScreens(screens)
            .Build();
        
        // 3. Store screens for this window (needed later for Orchestrator initialization)
        _windowScreens[window] = screens;
        
        // 4. Set background (on UI thread)
        if (background != null)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                window.SetPrecapturedAvaloniaBitmap(background);
                SetFrozenBackground(background);
                Log.Debug("[OverlaySession] Background captured and set for window at {Position}", bounds.Position);
            });
        }
        else
        {
            Log.Warning("[OverlaySession] Background capture failed, window created without background");
        }
        
        return window;
    }
    
    /// <summary>
    /// Internal method: Capture background for a region
    /// Encapsulates coordinate mapping, capture, and format conversion
    /// </summary>
    private async Task<Bitmap?> CaptureBackgroundAsync(Rect bounds, Screen screen)
    {
        try
        {
            var physicalBounds = _coordinateMapper.MapToPhysicalRect(bounds, screen);
            Log.Debug("[OverlaySession] Capturing background: Logical={Logical}, Physical={Physical}", bounds, physicalBounds);
            
            var skBitmap = await _captureStrategy.CaptureRegionAsync(physicalBounds);
            if (skBitmap == null)
            {
                Log.Warning("[OverlaySession] Screen capture returned null");
                return null;
            }
            
            var avaBitmap = BitmapConverter.ConvertToAvaloniaBitmapFast(skBitmap);
            Log.Debug("[OverlaySession] Background captured: {Width}x{Height}", skBitmap.Width, skBitmap.Height);
            
            return avaBitmap;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[OverlaySession] Failed to capture background");
            return null;
        }
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

        // Unsubscribe from window events to prevent memory leaks
        UnsubscribeFromWindowEvents();

        // Close all windows first
        Close();
        
        // Dispose owned Orchestrator
        try
        {
            _orchestrator?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[OverlaySession] Error disposing Orchestrator");
        }

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

