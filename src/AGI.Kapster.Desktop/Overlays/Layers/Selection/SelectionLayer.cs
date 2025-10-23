using System;
using Avalonia;
using Avalonia.Input;
using Serilog;
using AGI.Kapster.Desktop.Services.ElementDetection;
using AGI.Kapster.Desktop.Overlays.Events;

namespace AGI.Kapster.Desktop.Overlays.Layers.Selection;

/// <summary>
/// Selection layer that manages both free and element selection strategies
/// </summary>
public class SelectionLayer : ISelectionLayer
{
    private readonly IFreeSelectionStrategy _freeStrategy;
    private readonly IElementSelectionStrategy _elementStrategy;
    private readonly IOverlayEventBus _eventBus;
    
    private ISelectionStrategy _currentStrategy;
    private SelectionMode _currentMode = SelectionMode.Free;
    
    public string LayerId => "Selection";
    public int ZIndex { get; set; } = 10; // Above mask layer
    public bool IsVisible { get; set; } = true;
    public bool IsInteractive { get; set; } = true;
    
    public SelectionMode CurrentMode => _currentMode;
    
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<SelectionConfirmedEventArgs>? SelectionConfirmed;

    public SelectionLayer(
        IFreeSelectionStrategy freeStrategy,
        IElementSelectionStrategy elementStrategy,
        IOverlayEventBus eventBus)
    {
        _freeStrategy = freeStrategy ?? throw new ArgumentNullException(nameof(freeStrategy));
        _elementStrategy = elementStrategy ?? throw new ArgumentNullException(nameof(elementStrategy));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        
        // Start with free selection
        _currentStrategy = _freeStrategy;
        
        // Wire up strategy events
        _freeStrategy.SelectionChanged += OnStrategySelectionChanged;
        _freeStrategy.SelectionConfirmed += OnStrategySelectionConfirmed;
        _elementStrategy.SelectionChanged += OnStrategySelectionChanged;
        _elementStrategy.SelectionConfirmed += OnStrategySelectionConfirmed;
    }

    public void SwitchMode(SelectionMode mode)
    {
        if (_currentMode == mode)
            return;
        
        var oldMode = _currentMode;
        
        // Deactivate current strategy
        _currentStrategy.Deactivate();
        
        // Switch to new strategy
        _currentMode = mode;
        _currentStrategy = mode switch
        {
            SelectionMode.Free => _freeStrategy,
            SelectionMode.Element => _elementStrategy,
            _ => _freeStrategy
        };
        
        // Activate new strategy
        _currentStrategy.Activate();
        
        Log.Debug("Selection mode switched: {OldMode} -> {NewMode}", oldMode, mode);
    }

    public Rect? GetCurrentSelection()
    {
        return _currentStrategy.GetSelection();
    }

    public DetectedElement? GetSelectedElement()
    {
        return _currentStrategy.GetSelectedElement();
    }

    public void OnActivate()
    {
        IsVisible = true;
        IsInteractive = true;
        _currentStrategy.Activate();
        
        Log.Debug("Selection layer activated with mode: {Mode}", _currentMode);
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
        // Selection layer doesn't handle keyboard events directly
        // Mode switching is handled by OverlayWindow
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

    private void OnStrategySelectionConfirmed(object? sender, SelectionConfirmedEventArgs e)
    {
        // Forward to our own event
        SelectionConfirmed?.Invoke(this, e);
        
        // Publish to event bus
        _eventBus.Publish(new SelectionConfirmedEvent(e.Selection, e.Element));
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

