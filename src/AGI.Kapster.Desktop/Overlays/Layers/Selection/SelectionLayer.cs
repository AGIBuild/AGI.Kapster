using System;
using Avalonia;
using Avalonia.Input;
using Serilog;
using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Services.Overlay.State;
using AGI.Kapster.Desktop.Overlays.Events;

namespace AGI.Kapster.Desktop.Overlays.Layers.Selection;

/// <summary>
/// Selection layer that manages both free and element selection strategies
/// Plan A: Now self-owns SelectionOverlay and ElementHighlightOverlay visuals and creates strategies internally
/// </summary>
public class SelectionLayer : ISelectionLayer, IOverlayVisual
{
    private readonly SelectionOverlay _selectionOverlay;
    private readonly ElementHighlightOverlay? _highlightOverlay;
    private readonly IFreeSelectionStrategy _freeStrategy;
    private readonly IElementSelectionStrategy? _elementStrategy;
    private readonly IOverlayEventBus _eventBus;
    
    private ILayerHost? _host;
    private IOverlayContext? _context;
    private IOverlaySession? _session; // Session for mode coordination
    private ISelectionStrategy _currentStrategy;
    private SelectionMode _currentMode = SelectionMode.Free;
    
    public string LayerId => LayerIds.Selection;
    public int ZIndex { get; set; } = 10; // Above mask layer
    
    public bool IsVisible 
    { 
        get => _selectionOverlay.IsVisible; 
        set 
        {
            _selectionOverlay.IsVisible = value;
            if (_highlightOverlay != null)
                _highlightOverlay.IsVisible = value;
        }
    }
    
    public bool IsInteractive { get; set; } = true;
    
    public SelectionMode CurrentMode => _currentMode;
    
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<SelectionConfirmedEventArgs>? SelectionConfirmed;

    /// <summary>
    /// Constructor for SelectionLayer with element detection support
    /// </summary>
    public SelectionLayer(
        IElementDetector elementDetector,
        IMaskLayer maskLayer,
        Avalonia.Controls.Window overlayWindow,
        IOverlayEventBus eventBus,
        IOverlayLayerManager layerManager,
        IOverlaySession session)
        : this(eventBus, layerManager, session)
    {
        // Create ElementHighlightOverlay and ElementSelectionStrategy
        _highlightOverlay = new ElementHighlightOverlay(elementDetector);
        _elementStrategy = new ElementSelectionStrategyAdapter(
            elementDetector,
            _highlightOverlay,
            maskLayer,
            overlayWindow,
            eventBus);
        
        // Wire up element strategy events
        _elementStrategy.SelectionChanged += OnStrategySelectionChanged;
        _elementStrategy.SelectionFinished += OnStrategySelectionFinished;
        _elementStrategy.SelectionConfirmed += OnStrategySelectionConfirmed;
        
        // Pass session to ElementHighlightOverlay for highlight coordination
        _highlightOverlay.SetSession(session);
        
        Log.Debug("SelectionLayer created with element detection support");
    }

    /// <summary>
    /// Constructor for SelectionLayer with only free selection (no element detection)
    /// </summary>
    public SelectionLayer(IOverlayEventBus eventBus, IOverlayLayerManager layerManager, IOverlaySession session)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        
        // Create own SelectionOverlay visual
        _selectionOverlay = new SelectionOverlay(layerManager);
        _selectionOverlay.SetSession(session);
        
        // Create FreeSelectionStrategy
        _freeStrategy = new FreeSelectionStrategyAdapter(_selectionOverlay);
        
        // Start with free selection
        _currentStrategy = _freeStrategy;
        
        // Wire up free strategy events
        _freeStrategy.SelectionChanged += OnStrategySelectionChanged;
        _freeStrategy.SelectionFinished += OnStrategySelectionFinished;
        _freeStrategy.SelectionConfirmed += OnStrategySelectionConfirmed;
        
        // Subscribe to session's SelectionMode changes for synchronized mode switching
        _session.SelectionModeChanged += OnSessionSelectionModeChanged;
        
        // Sync current mode from session
        var sessionMode = _session.CurrentSelectionMode;
        if (sessionMode != _currentMode)
        {
            SwitchMode(sessionMode);
        }
        
        Log.Debug("SelectionLayer created with free selection only, mode synchronized to {Mode}", _currentMode);
    }
    
    private void OnSessionSelectionModeChanged(SelectionMode newMode)
    {
        Log.Debug("SelectionLayer: Received session mode change event: {NewMode}", newMode);
        SwitchMode(newMode);
    }

    public void SwitchMode(SelectionMode mode)
    {
        if (_currentMode == mode)
            return;
        
        var oldMode = _currentMode;
        
        // Deactivate current strategy
        _currentStrategy?.Deactivate();
        
        // Switch to new strategy
        _currentMode = mode;
        _currentStrategy = mode switch
        {
            SelectionMode.Free => _freeStrategy,
            SelectionMode.Element => (_elementStrategy as ISelectionStrategy) ?? _freeStrategy,
            _ => _freeStrategy
        };
        
        // Activate new strategy
        _currentStrategy?.Activate();
        
        // Notify LayerManager to switch OverlayMode for proper event routing and layer coordination
        var overlayMode = mode switch
        {
            SelectionMode.Element => OverlayMode.ElementPicker,
            SelectionMode.Free => OverlayMode.FreeSelection,
            _ => OverlayMode.FreeSelection
        };
        _eventBus.Publish(new OverlayModeChangeRequestedEvent(overlayMode));
        
        Log.Debug("Selection mode switched: {OldMode} -> {NewMode}, requesting OverlayMode: {OverlayMode}", 
            oldMode, mode, overlayMode);
    }

    public Rect? GetCurrentSelection()
    {
        var rect = _currentStrategy?.GetSelection();
        if (rect.HasValue && (rect.Value.Width <= 0 || rect.Value.Height <= 0))
        {
            return null;
        }
        return rect;
    }

    public DetectedElement? GetSelectedElement()
    {
        return _currentStrategy.GetSelectedElement();
    }

    public void OnActivate()
    {
        IsVisible = true;
        IsInteractive = true;
        
        _currentStrategy?.Activate();
        
        Log.Debug("SelectionLayer activated: Mode={Mode}", _currentMode);
    }

    public void OnDeactivate()
    {
        _currentStrategy.Deactivate();
        IsVisible = false;
        IsInteractive = false;
        
        Log.Debug("Selection layer deactivated");
    }

    public bool HandlePointerEvent(PointerEventArgs e)
    {
        if (!IsVisible || !IsInteractive)
            return false;
        
        return _currentStrategy.HandlePointerEvent(e);
    }

    public bool HandleKeyEvent(KeyEventArgs e)
    {
        // Handle Ctrl press/release to toggle element selection mode
        // Update session's CurrentSelectionMode to synchronize across all windows
        if (e.RoutedEvent == InputElement.KeyDownEvent)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                if (_currentMode == SelectionMode.Free)
                {
                    if (_session != null)
                    {
                        // Update session mode - will trigger OnSessionSelectionModeChanged
                        _session.CurrentSelectionMode = SelectionMode.Element;
                    }
                    else
                    {
                        // Fallback if session not set
                        SwitchMode(SelectionMode.Element);
                    }
                    Log.Debug("SelectionLayer: Entered element selection mode via Ctrl down");
                    return true;
                }
            }
        }
        else if (e.RoutedEvent == InputElement.KeyUpEvent)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                if (_currentMode == SelectionMode.Element)
                {
                    if (_session != null)
                    {
                        // Update session mode - will trigger OnSessionSelectionModeChanged
                        _session.CurrentSelectionMode = SelectionMode.Free;
                    }
                    else
                    {
                        // Fallback if session not set
                        SwitchMode(SelectionMode.Free);
                    }
                    Log.Debug("SelectionLayer: Returned to free selection mode via Ctrl up");
                    return true;
                }
            }
        }

        return false;
    }

    public bool CanHandle(OverlayMode mode)
    {
        // Selection layer is active in FreeSelection and ElementPicker modes
        return mode is OverlayMode.FreeSelection or OverlayMode.ElementPicker;
    }

    private void OnStrategySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Forward to our own event
        SelectionChanged?.Invoke(this, e);
        
        // Publish to event bus
        _eventBus.Publish(new SelectionChangedEvent(e.Selection));
    }
    
    private void OnStrategySelectionFinished(object? sender, SelectionFinishedEventArgs e)
    {
        // Publish to event bus
        _eventBus.Publish(new SelectionFinishedEvent(e.Selection, e.IsEditableSelection));
        
        Log.Debug("SelectionLayer: Selection finished, isEditable={IsEditable}", e.IsEditableSelection);
    }

    private void OnStrategySelectionConfirmed(object? sender, SelectionConfirmedEventArgs e)
    {
        // Forward to our own event
        SelectionConfirmed?.Invoke(this, e);
        
        // Publish to event bus
        _eventBus.Publish(new SelectionConfirmedEvent(e.Selection, e.Element));
    }
    
    // === IOverlayVisual Implementation (Plan A) ===
    
    public void AttachTo(ILayerHost host, IOverlayContext context)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        
        // Attach SelectionOverlay to host
        host.Attach(_selectionOverlay, this.ZIndex);
        
        // Attach ElementHighlightOverlay if available (with higher Z-index for visibility)
        if (_highlightOverlay != null)
        {
            host.Attach(_highlightOverlay, this.ZIndex + 1);
        }
        
        Log.Debug("SelectionLayer attached to host");
    }
    
    public void Detach()
    {
        if (_host != null)
        {
            _host.Detach(_selectionOverlay);
            
            if (_highlightOverlay != null)
            {
                _host.Detach(_highlightOverlay);
            }
            
            _host = null;
            _context = null;
            Log.Debug("SelectionLayer detached from host");
        }
    }
}

/// <summary>
/// Type aliases for clarity
/// </summary>
public interface IFreeSelectionStrategy : ISelectionStrategy { }
public interface IElementSelectionStrategy : ISelectionStrategy { }

/// <summary>
/// Implement type aliases
/// </summary>
public class FreeSelectionStrategyAdapter : FreeSelectionStrategy, IFreeSelectionStrategy
{
    public FreeSelectionStrategyAdapter(SelectionOverlay selectionOverlay) 
        : base(selectionOverlay)
    {
    }
}

public class ElementSelectionStrategyAdapter : ElementSelectionStrategy, IElementSelectionStrategy
{
    public ElementSelectionStrategyAdapter(
        IElementDetector detector,
        ElementHighlightOverlay highlight,
        IMaskLayer maskLayer,
        Avalonia.Controls.Window overlayWindow,
        IOverlayEventBus eventBus)
        : base(detector, highlight, maskLayer, overlayWindow, eventBus)
    {
    }
}

