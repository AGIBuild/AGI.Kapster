using System;
using System.Collections.Generic;

namespace AGI.Kapster.Desktop.Services.Hotkeys;

internal readonly record struct HotkeyRegisterStart(long Version, int NativeId, int? ExistingNativeId);

/// <summary>
/// Pure managed state machine for hotkey registrations.
///
/// Purpose:
/// - Keep WindowsHotkeyProvider's Win32/PInvoke surface separate from concurrency/ordering logic.
/// - Support two-phase commit (Begin -> Win32 call -> Commit) so we never hold locks while waiting.
/// - Support "last writer wins" semantics via per-id versioning.
/// </summary>
internal sealed class HotkeyRegistryState
{
    private readonly object _lock = new();

    private readonly Dictionary<string, int> _nativeIdByStringId = new();
    private readonly Dictionary<int, Action> _callbackByNativeId = new();

    private long _opVersion;
    private readonly Dictionary<string, long> _idVersion = new();

    private int _nextNativeId = 1;
    private bool _disposed;

    public bool IsDisposed
    {
        get
        {
            lock (_lock) return _disposed;
        }
    }

    public bool TryGetCallback(int nativeId, out Action? callback)
    {
        lock (_lock)
        {
            return _callbackByNativeId.TryGetValue(nativeId, out callback);
        }
    }

    public HotkeyRegisterStart BeginRegister(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Hotkey id is required", nameof(id));

        lock (_lock)
        {
            if (_disposed)
                return default;

            var version = ++_opVersion;
            _idVersion[id] = version;

            int? existingNativeId = null;
            if (_nativeIdByStringId.TryGetValue(id, out var existing))
            {
                existingNativeId = existing;
                _nativeIdByStringId.Remove(id);
                _callbackByNativeId.Remove(existing);
            }

            var nativeId = _nextNativeId++;
            return new HotkeyRegisterStart(version, nativeId, existingNativeId);
        }
    }

    public bool CommitRegister(string id, long version, int nativeId, Action callback)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Hotkey id is required", nameof(id));
        if (callback is null)
            throw new ArgumentNullException(nameof(callback));

        lock (_lock)
        {
            if (_disposed)
                return false;

            if (_idVersion.TryGetValue(id, out var current) && current == version)
            {
                _nativeIdByStringId[id] = nativeId;
                _callbackByNativeId[nativeId] = callback;
                return true;
            }

            return false;
        }
    }

    public bool BeginUnregister(string id, out int nativeId)
    {
        nativeId = default;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        lock (_lock)
        {
            if (_disposed)
                return false;

            // Invalidate any in-flight register for this id.
            _idVersion[id] = ++_opVersion;

            if (!_nativeIdByStringId.TryGetValue(id, out nativeId))
                return false;

            _nativeIdByStringId.Remove(id);
            _callbackByNativeId.Remove(nativeId);
            return true;
        }
    }

    public List<int> SnapshotUnregisterAll()
    {
        lock (_lock)
        {
            if (_disposed)
                return new List<int>();

            // Invalidate any in-flight per-id operations.
            _opVersion++;
            _idVersion.Clear();

            var nativeIds = new List<int>(_callbackByNativeId.Keys);
            _nativeIdByStringId.Clear();
            _callbackByNativeId.Clear();
            _nextNativeId = 1;
            return nativeIds;
        }
    }

    public List<int> MarkDisposedAndSnapshotAll()
    {
        lock (_lock)
        {
            if (_disposed)
                return new List<int>();
            _disposed = true;

            _opVersion++;
            _idVersion.Clear();

            var nativeIds = new List<int>(_callbackByNativeId.Keys);
            _nativeIdByStringId.Clear();
            _callbackByNativeId.Clear();
            _nextNativeId = 1;
            return nativeIds;
        }
    }
}
