using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

using AGI.Captor.App.Services.Overlay;
using AGI.Captor.App.Services;
using AGI.Captor.App.Models;
using Serilog;
using AGI.Captor.App.Services.Hotkeys;

namespace AGI.Captor.App.Overlays;

public sealed class OverlayWindowManager : IOverlayController
{
	private readonly List<OverlayWindow> _windows = new();
    private readonly IHotkeyProvider? _hotkeyProvider;
    private readonly IElementDetector? _elementDetector;
    private bool _escRegistered;
    
    // Note: Global element highlighting is now managed by GlobalElementHighlightState singleton

    public OverlayWindowManager(IHotkeyProvider? hotkeyProvider = null, IElementDetector? elementDetector = null)
    {
        _hotkeyProvider = hotkeyProvider;
        _elementDetector = elementDetector;
    }

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
		// Register global ESC to close all overlays during active session
		if (!_escRegistered && _hotkeyProvider is not null)
		{
			_escRegistered = _hotkeyProvider.Register("overlay-esc", new HotkeyChord(HotkeyModifiers.None, 0x1B /*Esc*/), () =>
			{
				CloseAll();
			});
			if (_escRegistered)
			{
				Log.Information("Overlay ESC hook registered");
			}
		}
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

		if (_escRegistered && _hotkeyProvider is not null)
		{
			_hotkeyProvider.Unregister("overlay-esc");
			_escRegistered = false;
			Log.Information("Overlay ESC hook unregistered");
		}
	}
    private void OnAnyWindowClosed(object? sender, EventArgs e)
    {
        CloseAll();
    }
}
