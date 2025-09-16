using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AGI.Captor.Desktop.Services.Overlay;

/// <summary>
/// Simplified overlay manager that uses DI directly without factory pattern
/// </summary>
public class SimplifiedOverlayManager : IOverlayController
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<IOverlayWindow> _windows = new();
    private IScreenCaptureStrategy? _captureStrategy;
    private IClipboardStrategy? _clipboardStrategy;

    public SimplifiedOverlayManager(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeServices();
    }

    public bool IsActive => _windows.Count > 0;

    public void ShowAll()
    {
        try
        {
            Log.Information("Showing overlay windows");
            
            CloseAll(); // Clean up any existing windows
            
            var screens = GetAvailableScreens();
            if (screens == null || screens.Count == 0)
            {
                Log.Warning("No screens available, creating default window");
                var window = CreateWindow();
                window.SetFullScreen(CreateDefaultScreen());
                window.Show();
                _windows.Add(window);
                return;
            }

            // Create an overlay window for each screen
            foreach (var screen in screens)
            {
                Log.Debug("Creating overlay for screen: {Screen}", screen.DisplayName);
                var window = CreateWindow();
                window.SetFullScreen(screen);
                window.Show();
                _windows.Add(window);
            }

            Log.Information("Created {Count} overlay windows", _windows.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show overlay windows");
            CloseAll();
        }
    }

    public void CloseAll()
    {
        Log.Information("Closing all overlay windows");
        
        foreach (var window in _windows)
        {
            try
            {
                UnsubscribeWindowEvents(window);
                window.Close();
                window.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error closing overlay window");
            }
        }
        
        _windows.Clear();
    }

    private void InitializeServices()
    {
        _captureStrategy = _serviceProvider.GetService<IScreenCaptureStrategy>();
        _clipboardStrategy = _serviceProvider.GetService<IClipboardStrategy>();
        
        if (_captureStrategy == null)
        {
            Log.Warning("No screen capture strategy available");
        }
        
        if (_clipboardStrategy == null)
        {
            Log.Warning("No clipboard strategy available");
        }
    }

    private IOverlayWindow CreateWindow()
    {
        var window = _serviceProvider.GetRequiredService<IOverlayWindow>();
        ConfigureWindow(window);
        return window;
    }

    private void ConfigureWindow(IOverlayWindow window)
    {
        window.RegionSelected += OnRegionSelected;
        window.Cancelled += OnCancelled;
        window.Closed += OnWindowClosed;
    }

    private void UnsubscribeWindowEvents(IOverlayWindow window)
    {
        window.RegionSelected -= OnRegionSelected;
        window.Cancelled -= OnCancelled;
        window.Closed -= OnWindowClosed;
    }

    private List<Screen>? GetAvailableScreens()
    {
        try
        {
            // Create a temporary window to get screen information
            var tempWindow = new Window
            {
                Width = 1,
                Height = 1,
                ShowInTaskbar = false,
                WindowState = WindowState.Minimized,
                SystemDecorations = SystemDecorations.None,
                Opacity = 0
            };
            
            tempWindow.Show();
            var screens = tempWindow.Screens?.All?.ToList();
            tempWindow.Close();
            
            return screens;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get available screens");
            return null;
        }
    }

    private async void OnRegionSelected(object? sender, CaptureRegionEventArgs e)
    {
        try
        {
            Log.Information("Region selected: {Region}", e.Region);
            
            if (_captureStrategy != null && _clipboardStrategy != null)
            {
                // Capture the region
                var bitmap = await _captureStrategy.CaptureRegionAsync(e.Region);
                if (bitmap != null)
                {
                    // Copy to clipboard
                    var success = await _clipboardStrategy.SetImageAsync(bitmap);
                    Log.Information("Screenshot copied to clipboard: {Success}", success);
                }
                else
                {
                    Log.Warning("Failed to capture region");
                }
            }
            
            CloseAll();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling region selection");
        }
    }

    private void OnCancelled(object? sender, EventArgs e)
    {
        Log.Information("Overlay cancelled");
        CloseAll();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is IOverlayWindow window && _windows.Contains(window))
        {
            _windows.Remove(window);
            UnsubscribeWindowEvents(window);
            
            if (_windows.Count == 0)
            {
                Log.Information("All overlay windows closed");
            }
        }
    }

    private Screen CreateDefaultScreen()
    {
        return new Screen(
            1.0,
            new PixelRect(0, 0, 1920, 1080),
            new PixelRect(0, 0, 1920, 1080),
            true
        );
    }
}
