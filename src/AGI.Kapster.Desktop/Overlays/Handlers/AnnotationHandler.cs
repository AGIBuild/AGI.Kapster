using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Services.Annotation;
using AGI.Kapster.Desktop.Services.Settings;
using Avalonia;
using Avalonia.Controls;
using Serilog;
using System;

namespace AGI.Kapster.Desktop.Overlays.Handlers;

/// <summary>
/// Manages annotation overlay lifecycle, initialization, and coordination with toolbar
/// Handles annotation-related event subscriptions and state management
/// </summary>
internal sealed class AnnotationHandler
{
    private readonly ISettingsService _settingsService;
    private NewAnnotationOverlay? _annotator;
    private NewAnnotationToolbar? _toolbar;
    private SelectionOverlay? _selector;
    private Grid? _parentGrid;

    // Events for external consumers
    public event Action<Rect>? ConfirmRequested;
    public event Action? ExportRequested;
    public event Action? ColorPickerRequested;

    public AnnotationHandler(ISettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    /// <summary>
    /// Initialize annotation overlay and add it to the grid
    /// Should be called after window is opened
    /// </summary>
    public NewAnnotationOverlay InitializeAnnotator(
        Grid parentGrid,
        SelectionOverlay selector,
        NewAnnotationToolbar toolbar)
    {
        _parentGrid = parentGrid ?? throw new ArgumentNullException(nameof(parentGrid));
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _toolbar = toolbar ?? throw new ArgumentNullException(nameof(toolbar));

        // Create annotator with injected settings service
        _annotator = new NewAnnotationOverlay(_settingsService)
        {
            Name = "Annotator"
        };

        // Add annotator to grid (after Selector)
        var selectorIndex = _parentGrid.Children.IndexOf(_selector);
        _parentGrid.Children.Insert(selectorIndex + 1, _annotator);

        // Set focus to enable keyboard shortcuts
        _annotator.Focus();
        Log.Debug("Annotator initialized and added to grid");

        // Setup toolbar and event subscriptions
        SetupToolbarAndEvents();

        return _annotator;
    }

    /// <summary>
    /// Get the annotation overlay instance
    /// </summary>
    public NewAnnotationOverlay? Annotator => _annotator;

    /// <summary>
    /// Update annotator selection rect and hit test visibility
    /// </summary>
    public void UpdateSelection(Rect selection)
    {
        if (_annotator == null)
            return;

        _annotator.SelectionRect = selection;
        _annotator.IsHitTestVisible = selection.Width > 2 && selection.Height > 2;
    }

    /// <summary>
    /// Focus the annotator for keyboard input
    /// </summary>
    public void FocusAnnotator()
    {
        _annotator?.Focus();
        Log.Debug("Focus set to annotator");
    }

    /// <summary>
    /// Request export operation
    /// </summary>
    public void RequestExport()
    {
        _annotator?.RequestExport();
    }

    /// <summary>
    /// Show color picker dialog
    /// </summary>
    public void ShowColorPicker()
    {
        if (_toolbar != null)
        {
            _toolbar.ShowColorPicker();
            Log.Debug("Color picker opened");
        }
        else
        {
            Log.Warning("Cannot show color picker - toolbar not initialized");
        }
    }

    /// <summary>
    /// End text editing mode
    /// </summary>
    public void EndTextEditing()
    {
        _annotator?.EndTextEditing();
    }

    private void SetupToolbarAndEvents()
    {
        if (_annotator == null || _toolbar == null)
            return;

        // Set default tool to Arrow
        Log.Information("Setting default tool to Arrow");
        _annotator.CurrentTool = AnnotationToolType.Arrow;
        Log.Information("Default tool set to: {CurrentTool}", _annotator.CurrentTool);

        // Setup toolbar target (syncs UI with annotator)
        _toolbar.SetTarget(_annotator);

        // Subscribe to export events
        _annotator.ExportRequested += OnExportRequested;

        // Subscribe to color picker events
        _annotator.ColorPickerRequested += OnColorPickerRequested;

        // Subscribe to confirm events (double-click save)
        _annotator.ConfirmRequested += OnConfirmRequested;

        Log.Debug("Toolbar and event subscriptions configured");
    }

    private void OnExportRequested()
    {
        Log.Debug("Export requested from annotator");
        ExportRequested?.Invoke();
    }

    private void OnColorPickerRequested()
    {
        Log.Debug("Color picker requested from annotator");
        ColorPickerRequested?.Invoke();
    }

    private void OnConfirmRequested(Rect selectionRect)
    {
        Log.Debug("Confirm requested from annotator with selection: {Rect}", selectionRect);
        ConfirmRequested?.Invoke(selectionRect);
    }
}
