using Serilog;

namespace AGI.Kapster.Desktop.Services.Input;

/// <summary>
/// No-operation IME controller for platforms that don't support IME control
/// or as a fallback when IME control is not needed
/// </summary>
public class NoOpImeController : IImeController
{
    public bool IsSupported => false;

    public void DisableIme(nint windowHandle)
    {
        Log.Debug("IME control not supported on this platform - DisableIme ignored");
    }

    public void EnableIme(nint windowHandle)
    {
        Log.Debug("IME control not supported on this platform - EnableIme ignored");
    }
}

