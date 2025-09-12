using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace AGI.Captor.App.Services.Hotkeys;

public sealed class WindowsHotkeyProvider : IHotkeyProvider
{
	private const uint WM_HOTKEY = 0x0312;
	private const uint WM_APP = 0x8000;
	private const uint WM_APP_REGISTER = WM_APP + 1;
	private const uint WM_APP_UNREGISTER = WM_APP + 2;
	private const uint PM_NOREMOVE = 0x0000;

	private readonly Dictionary<int, Action> _callbacks = new();
	private readonly Dictionary<string, int> _ids = new();
	private readonly Dictionary<int, Action> _pendingCallbacks = new();
	private readonly Dictionary<int, HotkeyChord> _pendingChords = new();
	private readonly Dictionary<int, string> _pendingIdNames = new();
	private readonly Dictionary<int, TaskCompletionSource<bool>> _pendingUnregistrations = new();
	private readonly ILogger<WindowsHotkeyProvider>? _logger;
	private int _nextId = 1;
	private bool _initialized;
	private Thread? _messageThread;
	private uint _messageThreadId;
	private readonly ManualResetEventSlim _readyEvent = new(false);

	public WindowsHotkeyProvider(ILogger<WindowsHotkeyProvider>? logger = null)
	{
		_logger = logger;
		if (!OperatingSystem.IsWindows()) return;
		StartMessageThread();
		_readyEvent.Wait(TimeSpan.FromSeconds(5));
		if (!_initialized)
		{
			_logger?.LogError("Hotkey message thread failed to initialize.");
		}
	}

	public bool Register(string id, HotkeyChord chord, Action callback)
	{
		if (!OperatingSystem.IsWindows()) return false;
		if (!_initialized)
		{
			_logger?.LogWarning("Cannot register hotkey: message thread not ready.");
			return false;
		}

		int hotkeyId = Interlocked.Increment(ref _nextId);
		_pendingChords[hotkeyId] = chord;
		_pendingCallbacks[hotkeyId] = callback;
		_pendingIdNames[hotkeyId] = id;

		// Post to message thread to perform RegisterHotKey for the thread (hWnd = NULL)
		if (!PostThreadMessage(_messageThreadId, WM_APP_REGISTER, (IntPtr)hotkeyId, IntPtr.Zero))
		{
			var err = Marshal.GetLastWin32Error();
			_logger?.LogWarning("PostThreadMessage failed: {Error}", err);
			_pendingChords.Remove(hotkeyId);
			_pendingCallbacks.Remove(hotkeyId);
			_pendingIdNames.Remove(hotkeyId);
			return false;
		}

		// Wait briefly for registration result
		var sw = System.Diagnostics.Stopwatch.StartNew();
		while (sw.ElapsedMilliseconds < 500)
		{
			if (_ids.TryGetValue(id, out var activeId) && activeId == hotkeyId)
			{
				_logger?.LogInformation("Hotkey registered: {Id} -> {Chord}", id, chord);
				return true;
			}
			Thread.Sleep(10);
		}

		_pendingChords.Remove(hotkeyId);
		_pendingCallbacks.Remove(hotkeyId);
		_pendingIdNames.Remove(hotkeyId);
		_logger?.LogWarning("Hotkey registration timed out: {Id} {Chord}", id, chord);
		return false;
	}

	public void Unregister(string id)
	{
		// For backward compatibility, use async version but wait synchronously
		try
		{
			UnregisterAsync(id).Wait(TimeSpan.FromMilliseconds(500));
		}
		catch (Exception ex)
		{
			_logger?.LogWarning(ex, "Error during synchronous hotkey unregistration: {Id}", id);
		}
	}

	public async Task<bool> UnregisterAsync(string id)
	{
		if (!_ids.TryGetValue(id, out var regId))
		{
			return true; // Already unregistered
		}

		var tcs = new TaskCompletionSource<bool>();
		_pendingUnregistrations[regId] = tcs;
		
		// Post unregister request to the same message thread that registered the hotkey
		if (!PostThreadMessage(_messageThreadId, WM_APP_UNREGISTER, (IntPtr)regId, IntPtr.Zero))
		{
			var err = Marshal.GetLastWin32Error();
			_logger?.LogWarning("PostThreadMessage for unregister failed: {Error}", err);
			_pendingUnregistrations.Remove(regId);
			return false;
		}
		
		// Wait for completion with timeout
		try
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
			var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));
			
			if (completedTask == tcs.Task)
			{
				var result = await tcs.Task;
				_logger?.LogDebug("Hotkey unregistered: {Id}, Result: {Result}", id, result);
				return result;
			}
			else
			{
				_logger?.LogWarning("Hotkey unregistration timed out: {Id}", id);
				_pendingUnregistrations.Remove(regId);
				return false;
			}
		}
		catch (Exception ex)
		{
			_logger?.LogWarning(ex, "Error during hotkey unregistration: {Id}", id);
			_pendingUnregistrations.Remove(regId);
			return false;
		}
	}

	public void Dispose()
	{
		foreach (var k in _callbacks.Keys)
		{
			UnregisterHotKey(IntPtr.Zero, k);
		}
		_callbacks.Clear();
		_ids.Clear();
	}

	private void StartMessageThread()
	{
		if (_initialized) return;
		_messageThread = new Thread(() =>
		{
			_messageThreadId = GetCurrentThreadId();
			// Ensure a message queue exists
			PeekMessage(out _, IntPtr.Zero, 0, 0, PM_NOREMOVE);
			_initialized = true;
			_readyEvent.Set();
			MSG msg;
			while (GetMessage(out msg, IntPtr.Zero, 0, 0))
			{
				if (msg.message == WM_APP_REGISTER)
				{
					int regId = msg.wParam.ToInt32();
					if (_pendingChords.TryGetValue(regId, out var chord))
					{
						bool ok = RegisterHotKey(IntPtr.Zero, regId, (uint)chord.Modifiers, chord.VirtualKey);
						if (!ok)
						{
							var err = Marshal.GetLastWin32Error();
							_logger?.LogWarning("RegisterHotKey failed on thread: {Error}", err);
						}
						else
						{
							_callbacks[regId] = _pendingCallbacks[regId];
							_ids[_pendingIdNames[regId]] = regId;
						}
						_pendingChords.Remove(regId);
						_pendingCallbacks.Remove(regId);
						_pendingIdNames.Remove(regId);
					}
					continue;
				}
				if (msg.message == WM_APP_UNREGISTER)
				{
					int regId = msg.wParam.ToInt32();
					bool ok = UnregisterHotKey(IntPtr.Zero, regId);
					if (!ok)
					{
						var err = Marshal.GetLastWin32Error();
						_logger?.LogWarning("UnregisterHotKey failed on thread: {Error}", err);
					}
					else
					{
						_logger?.LogDebug("UnregisterHotKey succeeded on thread for id: {RegId}", regId);
					}
					
					// Remove from dictionaries regardless of API success
					_callbacks.Remove(regId);
					// Find and remove the string id
					string? idToRemove = null;
					foreach (var kvp in _ids)
					{
						if (kvp.Value == regId)
						{
							idToRemove = kvp.Key;
							break;
						}
					}
					if (idToRemove != null)
					{
						_ids.Remove(idToRemove);
					}
					
					// Signal completion to waiting thread
					if (_pendingUnregistrations.TryGetValue(regId, out var tcs))
					{
						_pendingUnregistrations.Remove(regId);
						tcs.SetResult(ok);
					}
					
					continue;
				}
				if (msg.message == WM_HOTKEY)
				{
					int id = msg.wParam.ToInt32();
					if (_callbacks.TryGetValue(id, out var cb))
					{
						Dispatcher.UIThread.Post(() =>
						{
							try { cb(); }
							catch (Exception ex) { _logger?.LogError(ex, "Hotkey callback error"); }
						});
					}
					continue;
				}
				TranslateMessage(ref msg);
				DispatchMessage(ref msg);
			}
		})
		{ IsBackground = true, Name = "HotkeyMessageThread" };
		if (OperatingSystem.IsWindows())
			_messageThread.SetApartmentState(ApartmentState.STA);
		_messageThread.Start();
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool TranslateMessage(ref MSG lpMsg);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr DispatchMessage(ref MSG lpMsg);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

	[DllImport("kernel32.dll")]
	private static extern uint GetCurrentThreadId();

	[StructLayout(LayoutKind.Sequential)]
	private struct MSG
	{
		public IntPtr hwnd;
		public uint message;
		public IntPtr wParam;
		public IntPtr lParam;
		public uint time;
		public int pt_x;
		public int pt_y;
	}
}
