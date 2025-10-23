using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Serilog;

namespace AGI.Kapster.Desktop.Services;

/// <summary>
/// Hidden monitor window that serves as application MainWindow
/// Implements IScreenMonitorService to provide real-time screen information
/// This window remains hidden but active throughout application lifetime
/// </summary>
public partial class ScreenMonitorWindow : Window, IScreenMonitorService
{
    private IReadOnlyList<Screen> _cachedScreens = Array.Empty<Screen>();
    private readonly object _lock = new();
    private bool _isAppExiting;
    
    public event EventHandler<ScreensChangedEventArgs>? ScreensChanged;
    
    public ScreenMonitorWindow()
    {
        InitializeComponent();
        
        // Subscribe to window lifecycle events
        this.Opened += OnWindowOpened;
        this.Closing += OnWindowClosing;
    }
    
    private void OnWindowOpened(object? sender, EventArgs e)
    {
        // Initial screen cache
        UpdateScreensCache();
        
        // Subscribe to screen changes
        if (this.Screens != null)
        {
            this.Screens.Changed += OnScreensChanged;
            Log.Debug("Subscribed to Screens.Changed event");
        }
        else
        {
            Log.Warning("Screens property is null, cannot subscribe to changes");
        }
        
        Log.Information("ScreenMonitorWindow initialized as MainWindow with {Count} screen(s)", 
            _cachedScreens.Count);
    }
    
    private void OnScreensChanged(object? sender, EventArgs e)
    {
        Log.Information("Screen configuration changed, updating cache");
        UpdateScreensCache();
        
        // Notify subscribers on UI thread
        Dispatcher.UIThread.Post(() =>
        {
            ScreensChanged?.Invoke(this, new ScreensChangedEventArgs(_cachedScreens));
        });
    }
    
    private void UpdateScreensCache()
    {
        lock (_lock)
        {
            var screensList = this.Screens?.All?.ToList();
            _cachedScreens = screensList ?? new List<Screen>();
            
            if (_cachedScreens.Count > 0)
            {
                Log.Debug("Updated screen cache: {Screens}", 
                    string.Join(", ", _cachedScreens.Select(s => 
                        $"{s.Bounds.Width}x{s.Bounds.Height}@{s.Scaling:F2}")));
            }
            else
            {
                Log.Warning("Screen cache is empty after update");
            }
        }
    }
    
    public IReadOnlyList<Screen> GetCurrentScreens()
    {
        lock (_lock)
        {
            return _cachedScreens;
        }
    }
    
    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // Don't allow closing unless it's application exit
        if (!_isAppExiting)
        {
            e.Cancel = true;
            Log.Debug("ScreenMonitorWindow close prevented (not app exit)");
        }
        else
        {
            // Unsubscribe from events during shutdown
            if (this.Screens != null)
            {
                this.Screens.Changed -= OnScreensChanged;
            }
            Log.Information("ScreenMonitorWindow closing for application exit");
        }
    }
    
    public void RequestAppExit()
    {
        Log.Information("Application exit requested via ScreenMonitorWindow");
        _isAppExiting = true;
        
        // Close monitor window on UI thread (will trigger app exit)
        Dispatcher.UIThread.Post(() => this.Close());
    }
}

