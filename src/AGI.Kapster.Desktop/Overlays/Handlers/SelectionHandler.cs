using AGI.Kapster.Desktop.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Serilog;
using System;

namespace AGI.Kapster.Desktop.Overlays.Handlers;

/// <summary>
/// Manages selection mode, keyboard events, and cursor state for overlay window
/// Coordinates SelectionOverlay behavior and selection-related events
/// </summary>
internal sealed class SelectionHandler
{
    private readonly Window _window;
    private readonly SelectionOverlay _selector;
    private OverlaySelectionMode _selectionMode = OverlaySelectionMode.FreeSelection;

    // Events
    public event EventHandler<RegionSelectedEventArgs>? RegionSelected;
    public event EventHandler<OverlayCancelledEventArgs>? Cancelled;
    public event Action<Rect>? SelectionChanged;
    public event Action<Rect>? SelectionFinished;
    public event Action<Rect>? ConfirmRequested;

    public OverlaySelectionMode SelectionMode => _selectionMode;

    public SelectionHandler(Window window, SelectionOverlay selector)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));

        // Subscribe to selector events
        _selector.SelectionChanged += OnSelectorSelectionChanged;
        _selector.SelectionFinished += OnSelectorSelectionFinished;
        _selector.ConfirmRequested += OnSelectorConfirmRequested;

        // Show selection overlay by default
        _selector.IsVisible = true;
        _selector.IsHitTestVisible = true;
    }

    /// <summary>
    /// Handle ESC key - cancel and close overlay
    /// </summary>
    public void HandleEscapeKey()
    {
        Log.Information("ESC key pressed - exiting screenshot mode");
        Cancelled?.Invoke(this, new OverlayCancelledEventArgs("User pressed ESC"));
    }

    /// <summary>
    /// Handle Enter key - confirm selection
    /// </summary>
    public void HandleEnterKey()
    {
        var rect = _selector.SelectionRect;
        if (rect.Width > 0)
        {
            Log.Debug("Enter key pressed - confirming selection");
            RegionSelected?.Invoke(this, new RegionSelectedEventArgs(rect, false));
        }
    }

    /// <summary>
    /// Handle Tab key - toggle element picker mode
    /// </summary>
    public void HandleTabKey()
    {
        ToggleElementPickerMode();
    }

    /// <summary>
    /// Handle Ctrl key press - switch to element picker mode
    /// </summary>
    public void HandleCtrlKeyDown()
    {
        if (_selectionMode == OverlaySelectionMode.FreeSelection)
        {
            SwitchToElementPicker();
        }
    }

    /// <summary>
    /// Handle Ctrl key release - switch back to free selection mode
    /// </summary>
    public void HandleCtrlKeyUp()
    {
        if (_selectionMode == OverlaySelectionMode.ElementPicker)
        {
            SwitchToFreeSelection();
        }
    }

    /// <summary>
    /// Switch to element picker mode
    /// </summary>
    public void SwitchToElementPicker()
    {
        _selectionMode = OverlaySelectionMode.ElementPicker;

        // Hide selection overlay
        _selector.IsVisible = false;
        _selector.IsHitTestVisible = false;

        // Set cursor for element selection
        _window.Cursor = new Cursor(StandardCursorType.Hand);

        Log.Debug("Switched to element picker mode");
    }

    /// <summary>
    /// Switch to free selection mode
    /// </summary>
    public void SwitchToFreeSelection()
    {
        _selectionMode = OverlaySelectionMode.FreeSelection;

        // Show selection overlay
        _selector.IsVisible = true;
        _selector.IsHitTestVisible = true;

        // Set cursor for free selection
        _window.Cursor = new Cursor(StandardCursorType.Cross);

        Log.Debug("Switched to free selection mode");
    }

    /// <summary>
    /// Switch to editing mode (after selection is made)
    /// </summary>
    public void SwitchToEditingMode()
    {
        _selectionMode = OverlaySelectionMode.Editing;
        Log.Debug("Switched to editing mode");
    }

    /// <summary>
    /// Toggle between element picker and free selection modes
    /// </summary>
    public void ToggleElementPickerMode()
    {
        if (_selectionMode == OverlaySelectionMode.ElementPicker)
        {
            SwitchToFreeSelection();
        }
        else if (_selectionMode == OverlaySelectionMode.FreeSelection)
        {
            SwitchToElementPicker();
        }

        Log.Debug("Selection mode toggled to: {Mode}", _selectionMode);
    }

    /// <summary>
    /// Set element picker mode programmatically
    /// </summary>
    public void SetElementPickerMode(bool enabled)
    {
        var newMode = enabled ? OverlaySelectionMode.ElementPicker : OverlaySelectionMode.FreeSelection;
        if (_selectionMode != newMode)
        {
            if (enabled)
            {
                SwitchToElementPicker();
            }
            else
            {
                SwitchToFreeSelection();
            }
        }
    }

    /// <summary>
    /// Get current selection rectangle
    /// </summary>
    public Rect GetSelectionRect()
    {
        return _selector.SelectionRect;
    }

    /// <summary>
    /// Show and enable selection overlay
    /// </summary>
    public void ShowSelector()
    {
        _selector.IsVisible = true;
        _selector.IsHitTestVisible = true;
    }

    /// <summary>
    /// Hide selection overlay
    /// </summary>
    public void HideSelector()
    {
        _selector.IsVisible = false;
    }

    /// <summary>
    /// Set selection programmatically
    /// </summary>
    public void SetSelection(Rect rect)
    {
        _selector.SetSelection(rect);
        _selectionMode = OverlaySelectionMode.Editing;
    }

    private void OnSelectorSelectionChanged(Rect rect)
    {
        SelectionChanged?.Invoke(rect);
    }

    private void OnSelectorSelectionFinished(Rect rect)
    {
        _selectionMode = OverlaySelectionMode.Editing;
        Log.Information("Selection finished: {X},{Y} {W}x{H} - editable selection created", rect.X, rect.Y, rect.Width, rect.Height);
        SelectionFinished?.Invoke(rect);
    }

    private void OnSelectorConfirmRequested(Rect rect)
    {
        Log.Debug("Selection confirmed: {Rect}", rect);
        ConfirmRequested?.Invoke(rect);
    }
}
