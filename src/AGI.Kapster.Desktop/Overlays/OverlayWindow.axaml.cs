using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Serilog;

namespace AGI.Kapster.Desktop.Overlays;

public partial class OverlayWindow : Window, IOverlayWindow
{
    // Cached control references
    private Image? _backgroundImage;
    private LayerHost? _layerHost;

    // Mask size is set by overlay controller based on platform strategy
    private Size _maskSize;

    // Pure UI container - no Session dependency
    // Session subscribes to our events via AddHandler in NotifyWindowReady
    public OverlayWindow()
    {
        // Fast initialization: only XAML parsing
        InitializeComponent();

        // Cache control references immediately after InitializeComponent
        CacheControlReferences();

        // Minimal setup for immediate display
        this.Cursor = new Cursor(StandardCursorType.Cross);
        
        Log.Debug("OverlayWindow: Initialized");
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
    /// Set pre-captured Avalonia bitmap for UI display
    /// Session will separately call SetFrozenBackground on Orchestrator for image capture
    /// </summary>
    public void SetPrecapturedAvaloniaBitmap(Bitmap? bitmap)
    {
        // Apply background to UI on UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (bitmap != null && _backgroundImage != null)
            {
                _backgroundImage.Source = bitmap;
                Log.Debug("OverlayWindow: Background image applied");
            }
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// Set mask size for platform-specific overlay strategies
    /// Called by WindowBuilder before Show()
    /// </summary>
    public void SetMaskSize(double width, double height)
    {
        _maskSize = new Size(width, height);
        Log.Debug("OverlayWindow: Mask size set to {Width}x{Height}", width, height);
    }
    
    /// <summary>
    /// Get LayerHost for Orchestrator initialization
    /// </summary>
    public ILayerHost? GetLayerHost() => _layerHost;
    
    /// <summary>
    /// Get mask size for Orchestrator initialization
    /// </summary>
    public Size GetMaskSize() => _maskSize;
    
    /// <summary>
    /// Get this window as TopLevel for Orchestrator initialization
    /// </summary>
    public TopLevel AsTopLevel() => (TopLevel)this;
    
    /// <summary>
    /// Get the underlying Window instance (implements IOverlayWindow)
    /// Required for IOverlaySession.AddWindow(Window) compatibility
    /// </summary>
    public Window AsWindow() => this;
}

