namespace AGI.Kapster.Desktop.Services.Input;

/// <summary>
/// Controls Input Method Editor (IME) state for windows
/// Used to prevent IME interference with keyboard shortcuts in overlay mode
/// </summary>
public interface IImeController
{
    /// <summary>
    /// Disable IME for the specified window
    /// </summary>
    /// <param name="windowHandle">Native window handle</param>
    void DisableIme(nint windowHandle);

    /// <summary>
    /// Enable IME for the specified window (restore previous state)
    /// </summary>
    /// <param name="windowHandle">Native window handle</param>
    void EnableIme(nint windowHandle);

    /// <summary>
    /// Check if IME control is supported on current platform
    /// </summary>
    bool IsSupported { get; }
}

