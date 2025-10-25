using System;
using System.Collections.Generic;
using AGI.Kapster.Desktop.Overlays.Events;
using AGI.Kapster.Desktop.Services.UI;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Serilog;

namespace AGI.Kapster.Desktop.Overlays.Layers;

/// <summary>
/// ToolbarLayer implementation - wraps NewAnnotationToolbar
/// Plan A: Now self-owns NewAnnotationToolbar visual
/// </summary>
public class ToolbarLayer : IToolbarLayer, IOverlayVisual
{
    private readonly NewAnnotationToolbar _toolbar;
    private readonly IOverlayEventBus _eventBus;
    private readonly IToolbarPositionCalculator _positionCalculator;
    private readonly IOverlayLayerManager _layerManager;
    
    private ILayerHost? _host;
    private IOverlayContext? _context;
    private IAnnotationLayer? _annotationLayer;
    private Rect _currentSelection;
    
    public string LayerId => LayerIds.Toolbar;
    public int ZIndex { get; set; } = 30;
    
    public bool IsVisible 
    { 
        get => _toolbar.IsVisible; 
        set => _toolbar.IsVisible = value; 
    }
    
    public bool IsInteractive { get; set; } = true;
    
    public ToolbarLayer(
        IOverlayEventBus eventBus,
        IToolbarPositionCalculator positionCalculator,
        IOverlayLayerManager layerManager)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _positionCalculator = positionCalculator ?? throw new ArgumentNullException(nameof(positionCalculator));
        _layerManager = layerManager ?? throw new ArgumentNullException(nameof(layerManager));
        
        // Create toolbar
        _toolbar = new NewAnnotationToolbar
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        
        // Subscribe to toolbar size changes to reposition when content changes (e.g., switching to text tool)
        _toolbar.SizeChanged += OnToolbarSizeChanged;
        
        // Subscribe to EventBus events
        _eventBus.Subscribe<SelectionFinishedEvent>(OnSelectionFinished);
        _eventBus.Subscribe<OverlayContextChangedEvent>(OnOverlayContextChanged);
        _eventBus.Subscribe<ToolChangedEvent>(OnToolChanged);
        _eventBus.Subscribe<StyleChangedEvent>(OnStyleChanged);
        
        Log.Debug("ToolbarLayer created");
    }
    
    private void OnToolbarSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // When toolbar size changes (e.g., switching to text tool adds font size controls),
        // recalculate position to prevent overflow
        if (IsVisible && _currentSelection != default)
        {
            Log.Debug("ToolbarLayer: Size changed from {OldSize} to {NewSize}, recalculating position", 
                e.PreviousSize, e.NewSize);
            UpdatePositionFromContext();
        }
    }
    
    public void OnActivate()
    {
        // Performance optimization: Keep toolbar hidden until selection is finished
        // This eliminates immediate positioning calculation on activation
        IsVisible = false;
        HideToolbar();
        
        // Toolbar will be shown and positioned when SelectionFinished event fires
        Log.Debug("ToolbarLayer activated (hidden until selection finished)");
    }
    
    public void OnDeactivate()
    {
        IsVisible = false;
        HideToolbar();
        Log.Debug("ToolbarLayer deactivated");
    }
    
    public bool HandlePointerEvent(PointerEventArgs e)
    {
        // Toolbar handles its own pointer events through Avalonia's event system
        return false;
    }
    
    public bool HandleKeyEvent(KeyEventArgs e)
    {
        // Toolbar doesn't handle keyboard events directly
        return false;
    }
    
    public bool CanHandle(OverlayMode mode)
    {
        return mode == OverlayMode.Annotation;
    }
    
    // === IToolbarLayer Implementation ===
    
    public void UpdatePosition(Rect selection, Size canvasSize, PixelPoint windowPosition, IReadOnlyList<Screen>? screens)
    {
        if (selection.Width <= 0 || selection.Height <= 0)
        {
            HideToolbar();
            return;
        }
        
        // Force measurement if toolbar hasn't been measured yet
        if ((_toolbar.Bounds.Width == 0 || _toolbar.Bounds.Height == 0) && 
            (_toolbar.DesiredSize.Width == 0 || _toolbar.DesiredSize.Height == 0))
        {
            // Force measure to get actual toolbar size
            _toolbar.Measure(canvasSize);
        }
        
        // Get toolbar's actual size (prefer Bounds, fallback to DesiredSize)
        var toolbarSize = _toolbar.Bounds.Width > 0 && _toolbar.Bounds.Height > 0
            ? _toolbar.Bounds.Size 
            : _toolbar.DesiredSize;
        
        var context = new ToolbarPositionContext(
            Selection: selection,
            ToolbarSize: toolbarSize,
            OverlayPosition: windowPosition,
            Screens: screens);
        
        var position = _positionCalculator.CalculatePosition(context);
        
        // Use Canvas absolute positioning
        Canvas.SetLeft(_toolbar, position.X);
        Canvas.SetTop(_toolbar, position.Y);
    }
    
    public void ShowColorPicker()
    {
        try
        {
            _toolbar.ShowColorPicker();
            Log.Debug("Color picker opened from ToolbarLayer");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show color picker from ToolbarLayer");
        }
    }
    
    public void SetAnnotationLayer(IAnnotationLayer? annotationLayer)
    {
        _annotationLayer = annotationLayer;

        // If the concrete layer exposes the underlying overlay, set toolbar target once
        if (annotationLayer is AnnotationLayer concrete)
        {
            _toolbar.SetTarget(concrete.GetOverlay());
            Log.Debug("ToolbarLayer target overlay set via AnnotationLayer");
        }
        else
        {
            Log.Debug("ToolbarLayer annotation layer reference updated (no direct overlay available)");
        }
    }
    
    // === Private Helpers ===
    
    private void HideToolbar()
    {
        // Move toolbar off-screen
        Canvas.SetLeft(_toolbar, -10000);
        Canvas.SetTop(_toolbar, -10000);
    }
    
    // === Event Handlers ===
    
    // OnSelectionChangedFromManager and OnSelectionChangedFromBus removed
    // Performance optimization: Toolbar no longer updates during selection dragging
    // Only updates once when selection is complete (OnSelectionFinished)
    
    private void OnSelectionFinished(SelectionFinishedEvent e)
    {
        // Performance optimization: This is the ONLY place toolbar updates position
        // Selection is complete, now show and position toolbar once
        _currentSelection = e.Selection;
        IsVisible = true;
        UpdatePositionFromContext();
    }
    
    private void OnToolChanged(ToolChangedEvent e)
    {
        // Toolbar updates itself via two-way binding with annotator
        // This handler is for future enhancements
    }
    
    private void OnStyleChanged(StyleChangedEvent e)
    {
        // Toolbar updates itself via two-way binding with annotator
        // This handler is for future enhancements
    }
    
    /// <summary>
    /// Update toolbar position using stored context and current selection
    /// Phase 3: Enhanced with fallback positioning
    /// </summary>
    private void UpdatePositionFromContext()
    {
        // P3 Fix: Use unified validation logic
        if (!SelectionValidator.IsValid(_currentSelection))
        {
            Log.Debug("ToolbarLayer: Invalid selection, keeping hidden");
            return;
        }
        
        // Phase 3: Fallback positioning when context is null
        if (_context == null)
        {
            Log.Warning("ToolbarLayer: Context is null, using fallback positioning");
            
            // Simple fallback: position below selection
            _toolbar.Margin = new Thickness(
                _currentSelection.X,
                _currentSelection.Bottom + 10,
                0, 0);
            
            Log.Debug("ToolbarLayer: Positioned using fallback at ({X}, {Y})", 
                _currentSelection.X, _currentSelection.Bottom + 10);
            return;
        }
        
        UpdatePosition(
            _currentSelection,
            _context.OverlaySize,
            _context.OverlayPosition,
            _context.Screens);
    }

    private void OnOverlayContextChanged(OverlayContextChangedEvent e)
    {
        // Only update position if toolbar is visible and selection is valid
        if (!IsVisible || !SelectionValidator.IsValid(_currentSelection))
        {
            return;
        }
        
        // Recompute toolbar position when window size/position/screens change
        UpdatePosition(_currentSelection, e.OverlaySize, e.OverlayPosition, e.Screens);
    }
    
    // === IOverlayVisual Implementation (Plan A) ===
    
    public void AttachTo(ILayerHost host, IOverlayContext context)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        
        // Attach visual to host
        host.Attach(_toolbar, this.ZIndex);
        
        // Initialize as hidden
        HideToolbar();
    }
    
    public void Detach()
    {
        if (_host != null)
        {
            _host.Detach(_toolbar);
            _host = null;
            _context = null;
        }
    }
}

