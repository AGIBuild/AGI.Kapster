using System;
using System.Threading;
using AGI.Captor.App.Services.Hotkeys;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AGI.Captor.Tests;

public class HotkeyProviderTests
{
    [Fact]
    public void Register_DoesNotThrow_AndReturnsBool()
    {
        if (!OperatingSystem.IsWindows()) return; // skip on non-windows
        using IHotkeyProvider provider = new WindowsHotkeyProvider(new NullLogger<WindowsHotkeyProvider>());
        var fired = false;
        var ok = provider.Register("test", new HotkeyChord(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x7B), () => fired = true);
        
        // Use the variable to avoid warning
        _ = fired;
        Assert.True(ok);
        // We cannot synthesize key presses here reliably. Just ensure no exception and registration returns.
    }
}


