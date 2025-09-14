using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

using AGI.Captor.Desktop.Services.Overlay;
using AGI.Captor.Desktop.Services;
using AGI.Captor.Desktop.Models;
using Serilog;

namespace AGI.Captor.Desktop.Overlays;

public sealed class OverlayWindowManager : IOverlayController
{
	private readonly List<OverlayWindow> _windows = new();
    private readonly IElementDetector? _elementDetector;
    
    // Note: Global element highlighting is now managed by GlobalElementHighlightState singleton

    public OverlayWindowManager(IElementDetector? elementDetector = null)
    {
        _elementDetector = elementDetector;
    }

    /// <summary>
    /// Gets whether any overlay windows are currently active
    /// </summary>
    public bool IsActive => _windows.Count > 0;

	public void ShowAll()
	{
		if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime life && life.MainWindow is not null)
		{
			Log.Information("Overlay ShowAll (no-arg) using MainWindow");
			ShowAll(life.MainWindow);
			return;
		}
		Log.Information("Overlay ShowAll (no-arg) no MainWindow, using single maximized window");
		var single = new OverlayWindow(_elementDetector);
		single.WindowState = WindowState.Maximized;
		single.Show();
		_windows.Add(single);
	}

	public void ShowAll(TopLevel anchor)
	{
		CloseAll();
		Log.Information("Overlay ShowAll for anchor size {W}x{H}", anchor?.Bounds.Width, anchor?.Bounds.Height);
		var screens = anchor?.Screens;
		if (screens is null || screens.All.Count == 0)
		{
			Log.Information("Overlay ShowAll: no screens found via anchor; fallback single window");
			var single = new OverlayWindow(_elementDetector);
			single.WindowState = WindowState.Maximized;
			single.Show();
			_windows.Add(single);
			return;
		}
		foreach (var s in screens.All)
		{
			var wdw = new OverlayWindow(_elementDetector)
			{
				Position = new PixelPoint(s.Bounds.Position.X, s.Bounds.Position.Y)
			};
			wdw.WindowState = WindowState.FullScreen;
			wdw.Closed += OnAnyWindowClosed;
			wdw.Show();
			_windows.Add(wdw);
		}
	}

	public void CloseAll()
	{
		foreach (var w in _windows)
		{
			try { w.Closed -= OnAnyWindowClosed; w.Close(); } catch {}
		}
		Log.Information("Overlay windows closed: {Count}", _windows.Count);
		_windows.Clear();
	}
    private void OnAnyWindowClosed(object? sender, EventArgs e)
    {
        CloseAll();
    }
}
