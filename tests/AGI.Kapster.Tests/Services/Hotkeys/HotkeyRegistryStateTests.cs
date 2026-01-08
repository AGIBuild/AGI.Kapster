using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Services.Hotkeys;
using Xunit;

namespace AGI.Kapster.Tests.Services.Hotkeys;

public class WindowsHotkeyProviderTests
{
    [Fact]
    public async Task RegisterHotkey_WhenOvertaken_RollsBackFirstNativeId()
    {
        var native = new FakeNativeApi();
        var invoker = new BlockFirstCallInvoker();
        using var provider = new WindowsHotkeyProvider(
            resolver: null,
            nativeApi: native,
            invoker: invoker,
            startMessageThread: false);

        var gesture = HotkeyGesture.FromNamedKey(HotkeyModifiers.Alt, NamedKey.F1);

        bool firstResult = true;
        var t1 = Task.Run(() =>
        {
            firstResult = provider.RegisterHotkey("open_settings", gesture, () => { });
        });

        invoker.WaitUntilFirstStarted();

        // This one should win (last writer wins).
        var secondResult = provider.RegisterHotkey("open_settings", gesture, () => { });
        Assert.True(secondResult);

        invoker.ReleaseFirst();
        await t1;

        Assert.False(firstResult);

        // First BeginRegister gets nativeId=1; when overtaken it must be rolled back.
        Assert.Contains(1, native.UnregisteredIds);
        Assert.DoesNotContain(2, native.UnregisteredIds);
    }

    [Fact]
    public async Task UnregisterHotkey_DuringInFlightRegister_InvalidatesCommitAndRollsBack()
    {
        var native = new FakeNativeApi();
        var invoker = new BlockFirstCallInvoker();
        using var provider = new WindowsHotkeyProvider(
            resolver: null,
            nativeApi: native,
            invoker: invoker,
            startMessageThread: false);

        var gesture = HotkeyGesture.FromNamedKey(HotkeyModifiers.Alt, NamedKey.F1);

        bool registerResult = true;
        var t1 = Task.Run(() =>
        {
            registerResult = provider.RegisterHotkey("open_settings", gesture, () => { });
        });

        invoker.WaitUntilFirstStarted();

        // No committed registration exists yet, so UnregisterHotkey returns false,
        // but it must invalidate the in-flight register commit.
        Assert.False(provider.UnregisterHotkey("open_settings"));

        invoker.ReleaseFirst();
        await t1;

        Assert.False(registerResult);
        Assert.Contains(1, native.UnregisteredIds);
    }

    [Fact]
    public void Dispose_UnregistersCommittedHotkeys()
    {
        var native = new FakeNativeApi();
        var invoker = new InlineInvoker();
        var provider = new WindowsHotkeyProvider(
            resolver: null,
            nativeApi: native,
            invoker: invoker,
            startMessageThread: false);

        var gesture = HotkeyGesture.FromNamedKey(HotkeyModifiers.Alt, NamedKey.F1);
        Assert.True(provider.RegisterHotkey("a", gesture, () => { }));
        Assert.True(provider.RegisterHotkey("b", gesture, () => { }));

        provider.Dispose();

        // a=1, b=2
        Assert.Contains(1, native.UnregisteredIds);
        Assert.Contains(2, native.UnregisteredIds);
    }

    private sealed class InlineInvoker : IHotkeyThreadInvoker
    {
        public T Invoke<T>(Func<T> func, T fallback)
        {
            try { return func(); }
            catch { return fallback; }
        }
    }

    private sealed class BlockFirstCallInvoker : IHotkeyThreadInvoker
    {
        private int _callCount;
        private readonly ManualResetEventSlim _firstStarted = new(false);
        private readonly ManualResetEventSlim _releaseFirst = new(false);

        public void WaitUntilFirstStarted()
        {
            if (!_firstStarted.Wait(TimeSpan.FromSeconds(2)))
                throw new TimeoutException("First invoke did not start");
        }

        public void ReleaseFirst() => _releaseFirst.Set();

        public T Invoke<T>(Func<T> func, T fallback)
        {
            var call = Interlocked.Increment(ref _callCount);
            if (call == 1)
            {
                _firstStarted.Set();
                _releaseFirst.Wait(TimeSpan.FromSeconds(2));
            }

            try { return func(); }
            catch { return fallback; }
        }
    }

    private sealed class FakeNativeApi : IWindowsHotkeyNativeApi
    {
        public List<int> RegisteredIds { get; } = new();
        public List<int> UnregisteredIds { get; } = new();

        public bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk)
        {
            RegisteredIds.Add(id);
            return true;
        }

        public bool UnregisterHotKey(IntPtr hWnd, int id)
        {
            UnregisteredIds.Add(id);
            return true;
        }

        public int GetLastError() => 0;
    }
}
