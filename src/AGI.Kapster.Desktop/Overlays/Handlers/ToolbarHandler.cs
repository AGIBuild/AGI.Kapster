using AGI.Kapster.Desktop.Services.UI;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Serilog;
using System;
using System.Collections.Generic;

namespace AGI.Kapster.Desktop.Overlays.Handlers;

/// <summary>
/// Manages annotation toolbar positioning based on selection region
/// </summary>
internal sealed class ToolbarHandler
{
    private readonly Window _window;
    private readonly Canvas _uiCanvas;
    private readonly NewAnnotationToolbar _toolbar;
    private readonly IToolbarPositionCalculator _positionCalculator;
    private IReadOnlyList<Screen>? _screens;

    public ToolbarHandler(
        Window window,
        Canvas uiCanvas,
        NewAnnotationToolbar toolbar,
        IToolbarPositionCalculator positionCalculator)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _uiCanvas = uiCanvas ?? throw new ArgumentNullException(nameof(uiCanvas));
        _toolbar = toolbar ?? throw new ArgumentNullException(nameof(toolbar));
        _positionCalculator = positionCalculator ?? throw new ArgumentNullException(nameof(positionCalculator));
    }

    /// <summary>
    /// Set screens information for position calculation
    /// </summary>
    public void SetScreens(IReadOnlyList<Screen>? screens)
    {
        _screens = screens;
        Log.Debug("Toolbar handler: screens set ({Count} screen(s))", screens?.Count ?? 0);
    }

    /// <summary>
    /// Update toolbar position based on selection region
    /// </summary>
    public void UpdatePosition(Rect selection)
    {
        if (selection.Width <= 0 || selection.Height <= 0)
        {
            HideToolbar();
            return;
        }

        // Measure toolbar size
        _toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var toolbarSize = _toolbar.DesiredSize;

        // Calculate position using the calculator service
        var context = new ToolbarPositionContext(
            Selection: selection,
            ToolbarSize: toolbarSize,
            OverlayPosition: _window.Position,
            Screens: _screens);

        var position = _positionCalculator.CalculatePosition(context);

        Canvas.SetLeft(_toolbar, position.X);
        Canvas.SetTop(_toolbar, position.Y);

        Log.Debug("Toolbar positioned at ({X}, {Y})", position.X, position.Y);
    }

    /// <summary>
    /// Hide toolbar by positioning it off-screen
    /// </summary>
    public void HideToolbar()
    {
        Canvas.SetLeft(_toolbar, -10000);
        Canvas.SetTop(_toolbar, -10000);
    }

    /// <summary>
    /// Get toolbar reference
    /// </summary>
    public NewAnnotationToolbar Toolbar => _toolbar;
}
