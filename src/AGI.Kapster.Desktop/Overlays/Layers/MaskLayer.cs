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
/// </summary>
public class MaskLayer : IMaskLayer
{
    private readonly Path _maskPath;
    private readonly IOverlayEventBus _eventBus;
    
    private Size _maskSize;
    private Rect _currentCutout;
    private double _opacity = 0.25; // 25% opacity (40FFFFFF in hex is ~25%)
    private Color _color = Colors.White;
    
    public string LayerId => "Mask";
    public int ZIndex { get; set; } = 0; // Bottom layer
    public bool IsVisible { get; set; } = true;
    public bool IsInteractive { get; set; } = false; // Mask doesn't handle events
    
    public event EventHandler<CutoutChangedEventArgs>? CutoutChanged;

    public MaskLayer(Path maskPath, IOverlayEventBus eventBus)
    {
        _maskPath = maskPath ?? throw new ArgumentNullException(nameof(maskPath));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
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
        Log.Debug("Mask layer activated");
    }

    public void OnDeactivate()
    {
        // Mask is always visible, even when "deactivated"
        // Just clear cutout when deactivating
        ClearCutout();
        Log.Debug("Mask layer deactivated");
    }

    public bool HandlePointerEvent(PointerEventArgs e)
    {
        // Mask doesn't handle pointer events, always returns false
        return false;
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
}

