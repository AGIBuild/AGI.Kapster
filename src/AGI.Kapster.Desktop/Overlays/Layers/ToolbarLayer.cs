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
    private IOverlayLayerManager? _layerManager; // Phase 3: State management integration
    
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
    
    public bool IsInteractive { get; set; } = true; // Toolbar is always interactive when visible
    
    public ToolbarLayer(
        IOverlayEventBus eventBus,
        IToolbarPositionCalculator positionCalculator)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _positionCalculator = positionCalculator ?? throw new ArgumentNullException(nameof(positionCalculator));
        
        // Create own NewAnnotationToolbar visual
        _toolbar = new NewAnnotationToolbar();
        _toolbar.SizeChanged += (_, __) => UpdatePositionFromContext();
        
        // Subscribe to EventBus events for backward compatibility
        _eventBus.Subscribe<SelectionChangedEvent>(OnSelectionChangedFromBus);
        _eventBus.Subscribe<SelectionFinishedEvent>(OnSelectionFinished);
        _eventBus.Subscribe<OverlayContextChangedEvent>(OnOverlayContextChanged);
        _eventBus.Subscribe<ToolChangedEvent>(OnToolChanged);
        _eventBus.Subscribe<StyleChangedEvent>(OnStyleChanged);
        
        Log.Debug("ToolbarLayer created with self-owned visual");
    }
    
    /// <summary>
    /// Phase 3: Set LayerManager reference for state management integration
    /// Called by Orchestrator after layer creation
    /// </summary>
    internal void SetLayerManager(IOverlayLayerManager layerManager)
    {
        _layerManager = layerManager ?? throw new ArgumentNullException(nameof(layerManager));
        
        // Subscribe to LayerManager.SelectionChanged (primary data source)
        _layerManager.SelectionChanged += OnSelectionChangedFromManager;
        
        // Initialize with current selection
        var currentSelection = _layerManager.CurrentSelection;
        if (_layerManager.HasValidSelection)
        {
            _currentSelection = currentSelection;
            UpdatePositionFromContext();
            Log.Debug("ToolbarLayer: Initialized with current selection from LayerManager");
        }
        
        Log.Debug("ToolbarLayer: LayerManager reference set for state management");
    }
    
    public void OnActivate()
    {
        IsVisible = true;
        
        // P1 Fix: Immediately position toolbar when activated
        // Ensure toolbar moves from off-screen position to visible position
        if (_layerManager?.HasValidSelection == true)
        {
            _currentSelection = _layerManager.CurrentSelection;
            UpdatePositionFromContext(); // Calculate and apply position immediately
            Log.Debug("ToolbarLayer activated with selection: {Selection}", _currentSelection);
        }
        else
        {
            // No valid selection - keep toolbar off-screen but don't fail activation
            HideToolbar();
            Log.Debug("ToolbarLayer activated but no valid selection, toolbar hidden");
        }
        
        Log.Debug("ToolbarLayer activated");
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
        
        // Measure toolbar if needed
        if (_toolbar.DesiredSize.Width == 0 || _toolbar.DesiredSize.Height == 0)
        {
            _toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }
        
        var context = new ToolbarPositionContext(
            Selection: selection,
            ToolbarSize: _toolbar.DesiredSize,
            OverlayPosition: windowPosition,
            Screens: null); // Screens parameter is not used in current implementation
        
        var position = _positionCalculator.CalculatePosition(context);
        
        // Use Margin for positioning (works without Canvas parent)
        _toolbar.Margin = new Thickness(position.X, position.Y, 0, 0);
        _toolbar.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        _toolbar.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        
        Log.Debug("ToolbarLayer position updated: {Position}", position);
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
        _toolbar.Margin = new Thickness(-10000, -10000, 0, 0);
        Log.Debug("ToolbarLayer hidden");
    }
    
    // === Event Handlers ===
    
    private void OnSelectionChangedFromManager(object? sender, EventArgs e)
    {
        // Phase 3: Primary handler - pull data from LayerManager
        var selection = _layerManager?.CurrentSelection ?? default;
        _currentSelection = selection;
        UpdatePositionFromContext();
        Log.Debug("ToolbarLayer: Updated position from LayerManager selection: {Selection}", selection);
    }
    
    private void OnSelectionChangedFromBus(SelectionChangedEvent e)
    {
        // Backward compatibility: Update position from EventBus if LayerManager not set
        if (_layerManager == null)
        {
            _currentSelection = e.Selection;
            UpdatePositionFromContext();
            Log.Debug("ToolbarLayer: Updated position from EventBus (fallback): {Selection}", e.Selection);
        }
        // If LayerManager is set, ignore EventBus (already handled by manager)
    }
    
    private void OnSelectionFinished(SelectionFinishedEvent e)
    {
        // Keep for additional coordination logic (not for data)
        // Position update is already handled by OnSelectionChangedFromManager
        Log.Debug("ToolbarLayer: SelectionFinished event received");
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
        // Recompute toolbar position when window size/position/screens change
        if (_currentSelection != default)
        {
            UpdatePosition(
                _currentSelection,
                e.OverlaySize,
                e.OverlayPosition,
                e.Screens);
        }
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
        
        Log.Debug("ToolbarLayer attached to host");
    }
    
    public void Detach()
    {
        if (_host != null)
        {
            _host.Detach(_toolbar);
            _host = null;
            _context = null;
            Log.Debug("ToolbarLayer detached from host");
        }
    }
}

