using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Overlays.Infrastructure;
using AGI.Kapster.Desktop.Services.Overlay.State;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AGI.Kapster.Desktop.Overlays;

public partial class OverlayWindow : Window, IOverlayWindow
{
    // Orchestrator facade - single entry point for overlay subsystems
    private readonly IOverlayOrchestrator _orchestrator;
    
    // Cached control references
    private Image? _backgroundImage;
    private LayerHost? _layerHost;

    // Mask size is set by overlay controller based on platform strategy
    private Size _maskSize;
    
    // Session for this overlay (scoped state management)
    private IOverlaySession? _session;
    private IReadOnlyList<Screen>? _screens;

    // Public events for external consumers
    public event EventHandler<RegionSelectedEventArgs>? RegionSelected;

    // Property to check element detection support
    public bool ElementDetectionEnabled { get; set; }

    public OverlayWindow(IOverlayOrchestrator orchestrator)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        
        // Fast initialization: only XAML parsing
        InitializeComponent();

        // Cache control references immediately after InitializeComponent
        CacheControlReferences();

        // Minimal setup for immediate display
        this.Cursor = new Cursor(StandardCursorType.Cross);

        // Wire orchestrator events to window events for backward compatibility
        _orchestrator.RegionSelected += (s, e) => RegionSelected?.Invoke(this, e);

        // Set up input event handlers - delegate to orchestrator
        this.PointerPressed += OnOverlayPointerPressed;
        this.PointerMoved += OnOverlayPointerMoved;

		// Opened event: initialize UI components
		this.Opened += async (_, __) =>
		{
		    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
		    {
		        InitializeHeavyComponents();
		    });
		};

		// Set focus when window is loaded
		this.Loaded += OnOverlayWindowLoaded;
    }
    
    /// <summary>
    /// Cache control references to avoid repeated FindControl<>() calls
    /// </summary>
    private void CacheControlReferences()
    {
        _backgroundImage = this.FindControl<Image>("BackgroundImage");
        _layerHost = this.FindControl<LayerHost>("LayerHost");
    }

    /// <summary>
    /// Initialize heavy UI components and layers after background is visible
    /// </summary>
    private void InitializeHeavyComponents()
    {
        if (_layerHost == null)
        {
            Log.Error("OverlayWindow: LayerHost not found, cannot initialize orchestrator");
            return;
        }

        // Initialize orchestrator with window and host
        _orchestrator.Initialize(this, _layerHost, _maskSize);
        _orchestrator.BuildLayers();

        // Wire dynamic context updates for layers (size/position/screens)
        this.PropertyChanged += (_, e) =>
        {
            if (e.Property == BoundsProperty)
            {
                _orchestrator.PublishContextChanged(this.Bounds.Size, this.Position, _screens);
            }
        };
        
        // Setup cleanup handlers
        SetupCleanupHandlers();

        Log.Debug("OverlayWindow: Heavy components initialized via orchestrator");
    }

    private void OnOverlayWindowLoaded(object? sender, EventArgs e)
    {
        // Ensure window has focus for keyboard events
        this.Focus();
        Log.Debug("OverlayWindow: Window loaded and focused");
    }

    /// <summary>
    /// Set pre-captured Avalonia bitmap and apply to UI if window is already initialized
    /// Can be called before or after Show() - will update UI automatically
    /// </summary>
    public void SetPrecapturedAvaloniaBitmap(Bitmap? bitmap)
    {
        // Apply background immediately on UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (bitmap != null && _backgroundImage != null)
            {
                _backgroundImage.Source = bitmap;
                _orchestrator.SetFrozenBackground(bitmap);
            }
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// Set mask size for platform-specific overlay strategies (called by controller before Show())
    /// </summary>
    /// <param name="width">Mask width (logical pixels)</param>
    /// <param name="height">Mask height (logical pixels)</param>
    public void SetMaskSize(double width, double height)
    {
        _maskSize = new Size(width, height);
        Log.Debug("OverlayWindow: Mask size set to {Width}x{Height}", width, height);
    }
    
    /// <summary>
    /// Set the overlay session for this window (called by controller before Show())
    /// </summary>
    public void SetSession(IOverlaySession? session)
    {
        _session = session;
        
        // Pass session to orchestrator for element highlight coordination
        if (session != null)
        {
            _orchestrator.SetSession(session);
        }
        
        Log.Debug("OverlayWindow: Overlay session set and passed to orchestrator");
    }

    public void SetScreens(IReadOnlyList<Screen>? screens)
    {
        _screens = screens;
        _orchestrator.SetScreens(screens);
        Log.Debug("OverlayWindow: Screens set - {Count} screen(s)", screens?.Count ?? 0);
    }
    
    /// <summary>
    /// Get the underlying Window instance (implements IOverlayWindow)
    /// Required for IOverlaySession.AddWindow(Window) compatibility
    /// </summary>
    public Window AsWindow() => (Window)this;
    
    /// <summary>
    /// Get the current overlay session
    /// </summary>
    internal IOverlaySession? GetSession() => _session;

    /// <summary>
    /// Setup cleanup handlers for window lifecycle
    /// </summary>
    private void SetupCleanupHandlers()
    {
        // Clean up resources when this window closes
        this.Closing += (sender, e) =>
        {
            Log.Debug("OverlayWindow: Window closing, cleaning up resources");
            
            // CRITICAL: Close entire session when any window closes
            // This ensures all windows close together (multi-screen support)
            try
            {
                _session?.Close();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error closing session");
            }
            
            try
            {
                _orchestrator?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error disposing orchestrator");
            }
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_orchestrator.RouteKeyEvent(e))
        {
            e.Handled = true;
            return;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (_orchestrator.RouteKeyEvent(e))
        {
            e.Handled = true;
        }
    }

    private void OnOverlayPointerMoved(object? sender, PointerEventArgs e)
    {
        _orchestrator.RoutePointerEvent(e);
    }

    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _orchestrator.RoutePointerEvent(e);
    }

    /// <summary>
    /// Get full screen screenshot for color sampling
    /// </summary>
    public async Task<Bitmap?> GetFullScreenScreenshotAsync()
    {
        return await _orchestrator.GetFullScreenScreenshotAsync(this.Bounds.Size);
    }

}

