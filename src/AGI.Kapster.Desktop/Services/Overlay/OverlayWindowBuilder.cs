using System;
using System.Collections.Generic;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.Overlay.State;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Overlay;

/// <summary>
/// Builder for configuring IOverlayWindow instances
/// Provides fluent API for window setup
/// No Factory dependency - uses IServiceProvider directly
/// </summary>
public class OverlayWindowBuilder : IOverlayWindowBuilder
{
    private readonly OverlaySession _session;
    private readonly IServiceProvider _serviceProvider;
    private Rect? _bounds;
    private IReadOnlyList<Screen>? _screens;

    internal OverlayWindowBuilder(OverlaySession session, IServiceProvider serviceProvider)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IOverlayWindowBuilder WithBounds(Rect bounds)
    {
        _bounds = bounds;
        return this;
    }

    public IOverlayWindowBuilder WithScreens(IReadOnlyList<Screen> screens)
    {
        _screens = screens;
        return this;
    }

    public IOverlayWindow Build()
    {
        // Validate required parameters
        if (_bounds == null)
            throw new InvalidOperationException("Bounds is required. Call WithBounds() before Build()");
        if (_screens == null)
            throw new InvalidOperationException("Screens is required. Call WithScreens() before Build()");

        var bounds = _bounds.Value;
        
        // Create window (no DI dependencies in constructor)
        var window = ActivatorUtilities.CreateInstance<OverlayWindow>(_serviceProvider);
        
        // Configure window properties (position and size)
        window.Position = new PixelPoint((int)bounds.X, (int)bounds.Y);
        window.Width = bounds.Width;
        window.Height = bounds.Height;
        
        // Set overlay-specific configuration (UI only - no Session reference)
        window.SetMaskSize(bounds.Width, bounds.Height);
        
        // Screens information is stored in builder for potential future use
        // (e.g., passing to Session/Orchestrator for context updates)
        
        // Add window to session (unified lifecycle management)
        _session.AddWindow(window.AsWindow());
        
        // Subscribe to Loaded event - Session will initialize Orchestrator and subscribe to events
        window.AsWindow().Loaded += (sender, e) => _session.NotifyWindowReady(window);
        
        Log.Debug("OverlayWindowBuilder: Window created via DI and configured - Bounds={Bounds}, Screens={ScreenCount}", 
            bounds, _screens.Count);
        
        return window;
    }
}

