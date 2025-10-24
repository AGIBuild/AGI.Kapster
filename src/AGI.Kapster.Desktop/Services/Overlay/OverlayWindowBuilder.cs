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
    private bool _elementDetection = false;

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

    public IOverlayWindowBuilder EnableElementDetection(bool enable = true)
    {
        _elementDetection = enable;
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
        
        // Create window via DI (IOverlayOrchestrator auto-injected)
        var window = ActivatorUtilities.CreateInstance<OverlayWindow>(_serviceProvider);
        
        // Configure window properties (position and size)
        window.Position = new PixelPoint((int)bounds.X, (int)bounds.Y);
        window.Width = bounds.Width;
        window.Height = bounds.Height;
        
        // Set overlay-specific configuration
        window.SetSession(_session);
        window.SetScreens(_screens);
        window.SetMaskSize(bounds.Width, bounds.Height);
        window.ElementDetectionEnabled = _elementDetection;
        
        // Wire window events to session (event forwarding)
        window.RegionSelected += (sender, e) => _session.NotifyRegionSelected(sender, e);
        
        // Add window to session (unified lifecycle management)
        _session.AddWindow(window.AsWindow());
        
        Log.Debug("OverlayWindowBuilder: Window created via DI and configured - Bounds={Bounds}, Screens={ScreenCount}, ElementDetection={ElementDetection}", 
            bounds, _screens.Count, _elementDetection);
        
        return window;
    }
}

