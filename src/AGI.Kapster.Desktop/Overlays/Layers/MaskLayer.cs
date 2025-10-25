using System;
using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Serilog;
using AGI.Kapster.Desktop.Overlays.Events;

namespace AGI.Kapster.Desktop.Overlays.Layers;

/// <summary>
/// Mask layer implementation for overlay semi-transparent background with cutout support
/// Now self-owns its visual (Path) for Plan A architecture
/// </summary>
public class MaskLayer : IMaskLayer, IOverlayVisual
{
    private readonly Path _maskPath;
    private readonly Path _borderPath; // Border for cutout area
    private readonly IOverlayEventBus _eventBus;
    private readonly IOverlayLayerManager _layerManager;
    
    private ILayerHost? _host;
    private IOverlayContext? _context;
    private Size _maskSize;
    private Rect _currentCutout;
    private double _opacity = 0.25; // 25% opacity (40FFFFFF in hex is ~25%)
    private Color _color = Colors.White;
    
    public string LayerId => LayerIds.Mask;
    public int ZIndex { get; set; } = 0; // Bottom layer
    
    public bool IsVisible 
    { 
        get => _maskPath.IsVisible; 
        set
        {
            _maskPath.IsVisible = value;
            _borderPath.IsVisible = value;
        }
    }
    
    public bool IsInteractive { get; set; } = false;
    
    public event EventHandler<CutoutChangedEventArgs>? CutoutChanged;

    public MaskLayer(IOverlayEventBus eventBus, IOverlayLayerManager layerManager)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _layerManager = layerManager ?? throw new ArgumentNullException(nameof(layerManager));
        
        // Create own Path visual for mask
        _maskPath = new Path
        {
            Fill = Brushes.Transparent,
            IsHitTestVisible = false, // Will be enabled in Annotation mode
            ZIndex = this.ZIndex
        };
        
        // Create border Path for cutout area (shown in Annotation mode)
        _borderPath = new Path
        {
            Stroke = Brushes.DeepSkyBlue, // Match SelectionOverlay border color
            StrokeThickness = 2,
            IsHitTestVisible = false,
            IsVisible = false, // Hidden by default, shown in Annotation mode
            ZIndex = this.ZIndex + 1 // Above mask
        };
        
        // Subscribe to pointer events for double-click detection
        _maskPath.PointerPressed += OnMaskPointerPressed;
        
        // Subscribe to LayerManager selection changes
        _layerManager.SelectionChanged += OnSelectionChanged;
        
        Log.Debug("MaskLayer created");
    }
    
    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        var selection = _layerManager.CurrentSelection;
        SetCutout(selection);
        Log.Debug("MaskLayer: Updated cutout: {Selection}", selection);
    }

    public void SetMaskSize(Size size)
    {
        if (_maskSize == size)
            return;
        
        _maskSize = size;
        UpdateMaskGeometry();
        
        Log.Debug("Mask size set to: {Width}x{Height}", size.Width, size.Height);
    }

    public void SetMaskOpacity(double opacity)
    {
        _opacity = Math.Clamp(opacity, 0.0, 1.0);
        UpdateMaskFill();
        
        Log.Debug("Mask opacity set to: {Opacity}", _opacity);
    }

    public void SetMaskColor(Color color)
    {
        _color = color;
        UpdateMaskFill();
        
        Log.Debug("Mask color set to: {Color}", color);
    }

    public void SetCutout(Rect rect)
    {
        if (_currentCutout == rect)
            return; // No change
        
        var oldCutout = _currentCutout;
        _currentCutout = rect;
        
        UpdateMaskGeometry();
        
        // Raise events
        CutoutChanged?.Invoke(this, new CutoutChangedEventArgs(oldCutout, rect));
        _eventBus.Publish(new CutoutChangedEvent(oldCutout, rect));
        
        Log.Debug("Mask cutout updated: {Cutout}", rect);
    }

    public void ClearCutout()
    {
        SetCutout(default);
    }

    public Rect GetCurrentCutout()
    {
        return _currentCutout;
    }

    public bool IsPointInCutout(Point point)
    {
        return _currentCutout != default && _currentCutout.Contains(point);
    }

    public void OnActivate()
    {
        IsVisible = true;
        
        // Sync cutout from LayerManager when activated
        if (_layerManager?.HasValidSelection == true)
        {
            var selection = _layerManager.CurrentSelection;
            SetCutout(selection);
            
            // In Annotation mode: enable hit testing and show border
            if (_layerManager.CurrentMode == OverlayMode.Annotation)
            {
                _maskPath.IsHitTestVisible = true;
                _borderPath.IsVisible = true; // Show border in Annotation mode
                Log.Debug("MaskLayer activated with cutout: {Selection}, IsHitTestVisible=True, Border=Visible (Annotation mode)", selection);
            }
            else
            {
                _maskPath.IsHitTestVisible = false;
                _borderPath.IsVisible = false; // Hide border in other modes
                Log.Debug("MaskLayer activated with cutout: {Selection}, IsHitTestVisible=False, Border=Hidden", selection);
            }
        }
        else
        {
            // No valid selection - show full mask without cutout
            ClearCutout();
            _maskPath.IsHitTestVisible = false;
            _borderPath.IsVisible = false;
            Log.Debug("MaskLayer activated with no cutout (no valid selection)");
        }
        
        Log.Debug("Mask layer activated");
    }

    public void OnDeactivate()
    {
        // Mask is always visible, even when "deactivated"
        // Just clear cutout when deactivating
        ClearCutout();
        // Disable hit testing and hide border when deactivated
        _maskPath.IsHitTestVisible = false;
        _borderPath.IsVisible = false;
        Log.Debug("Mask layer deactivated");
    }

    public bool HandlePointerEvent(PointerEventArgs e)
    {
        // Mask handles pointer events via direct subscription to _maskPath.PointerPressed
        // This method is for manual event routing (not used for mask)
        return false;
    }
    
    /// <summary>
    /// Handle pointer pressed on mask to detect double-click outside selection for confirmation
    /// </summary>
    private void OnMaskPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(_maskPath);
        var position = point.Position;
        
        Log.Debug("MaskLayer: PointerPressed at {Position}, ClickCount={ClickCount}, HasCutout={HasCutout}", 
            position, e.ClickCount, _currentCutout != default);
        
        // Only handle events in Annotation mode (when mask has a cutout)
        if (_currentCutout == default || _currentCutout.Width <= 0 || _currentCutout.Height <= 0)
        {
            Log.Debug("MaskLayer: Ignoring click - no valid cutout");
            return;
        }
        
        // Check if click is outside the cutout (selection area)
        var isOutsideCutout = !_currentCutout.Contains(position);
        
        if (!isOutsideCutout)
        {
            Log.Debug("MaskLayer: Click inside cutout, ignoring");
            return;
        }
        
        // Handle double-click outside cutout for confirmation
        if (e.ClickCount == 2)
        {
            Log.Debug("MaskLayer: Double-click outside selection detected at {Position}, publishing SelectionConfirmedEvent", position);
            
            // Publish confirmation event
            _eventBus.Publish(new SelectionConfirmedEvent(_currentCutout, null));
            
            e.Handled = true;
        }
        else if (e.ClickCount == 1)
        {
            Log.Debug("MaskLayer: Single click outside selection at {Position}", position);
            // Single click - could be used for deselection or ignored
            // For now, just log it
        }
    }

    public bool HandleKeyEvent(KeyEventArgs e)
    {
        // Mask doesn't handle keyboard events
        return false;
    }

    public bool CanHandle(OverlayMode mode)
    {
        // Mask is visible in all modes
        return true;
    }

    /// <summary>
    /// Update mask geometry with Even-Odd fill rule to create cutout effect
    /// </summary>
    private void UpdateMaskGeometry()
    {
        if (_maskSize.Width <= 0 || _maskSize.Height <= 0)
        {
            Log.Warning("Invalid mask size: {Size}", _maskSize);
            return;
        }
        
        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        
        // Full mask rectangle
        group.Children.Add(new RectangleGeometry(
            new Rect(0, 0, _maskSize.Width, _maskSize.Height)));
        
        // Cutout rectangle (if valid)
        if (_currentCutout != default && 
            _currentCutout.Width > 0 && 
            _currentCutout.Height > 0)
        {
            group.Children.Add(new RectangleGeometry(_currentCutout));
            
            // Update border geometry to show cutout outline
            _borderPath.Data = new RectangleGeometry(_currentCutout);
        }
        else
        {
            // No cutout, hide border
            _borderPath.Data = null;
        }
        
        _maskPath.Data = group;
    }

    /// <summary>
    /// Update mask fill brush with current color and opacity
    /// </summary>
    private void UpdateMaskFill()
    {
        var colorWithOpacity = Color.FromArgb(
            (byte)(_opacity * 255),
            _color.R,
            _color.G,
            _color.B);
        
        _maskPath.Fill = new SolidColorBrush(colorWithOpacity);
    }
    
    // === IOverlayVisual Implementation (Plan A) ===
    
    public void AttachTo(ILayerHost host, IOverlayContext context)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        
        // Attach mask visual to host
        host.Attach(_maskPath, this.ZIndex);
        // Attach border visual to host (above mask)
        host.Attach(_borderPath, this.ZIndex + 1);
        
        // Initialize with context size if not set
        if (_maskSize == default && context.OverlaySize != default)
        {
            SetMaskSize(context.OverlaySize);
        }
        
        // Initialize fill
        UpdateMaskFill();
        
        Log.Debug("MaskLayer attached to host with border");
    }

    public void Detach()
    {
        if (_host != null)
        {
            _host.Detach(_maskPath);
            _host.Detach(_borderPath);
            _host = null;
            _context = null;
            Log.Debug("MaskLayer detached from host");
        }
    }
}

