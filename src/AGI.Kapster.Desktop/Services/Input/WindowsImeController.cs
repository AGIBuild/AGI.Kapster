using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Input;

/// <summary>
/// Windows implementation of IME controller using imm32.dll
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsImeController : IImeController
{
    private nint _savedImeContext = nint.Zero;

    public bool IsSupported => true;

    /// <summary>
    /// Get IME context for the specified window
    /// </summary>
    [DllImport("imm32.dll")]
    private static extern nint ImmGetContext(nint hWnd);

    /// <summary>
    /// Release IME context
    /// </summary>
    [DllImport("imm32.dll")]
    private static extern bool ImmReleaseContext(nint hWnd, nint hImc);

    /// <summary>
    /// Associate IME context with window (pass Zero to disable IME)
    /// </summary>
    [DllImport("imm32.dll")]
    private static extern nint ImmAssociateContext(nint hWnd, nint hImc);

    public void DisableIme(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
        {
            Log.Warning("Cannot disable IME: invalid window handle");
            return;
        }

        try
        {
            // Save current IME context before disabling
            _savedImeContext = ImmGetContext(windowHandle);
            
            // Disable IME by associating null context
            var result = ImmAssociateContext(windowHandle, nint.Zero);
            
            if (result == nint.Zero)
            {
                Log.Warning("Failed to disable IME for window {Handle}", windowHandle);
            }
            else
            {
                Log.Debug("IME disabled for window {Handle}, saved context: {Context}", 
                    windowHandle, _savedImeContext);
            }

            // Release the context handle
            if (_savedImeContext != nint.Zero)
            {
                ImmReleaseContext(windowHandle, _savedImeContext);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to disable IME for window {Handle}", windowHandle);
        }
    }

    public void EnableIme(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
        {
            Log.Warning("Cannot enable IME: invalid window handle");
            return;
        }

        try
        {
            // Restore saved IME context
            if (_savedImeContext != nint.Zero)
            {
                var result = ImmAssociateContext(windowHandle, _savedImeContext);
                
                if (result == nint.Zero)
                {
                    Log.Warning("Failed to enable IME for window {Handle}", windowHandle);
                }
                else
                {
                    Log.Debug("IME enabled for window {Handle}, restored context: {Context}", 
                        windowHandle, _savedImeContext);
                }
            }
            else
            {
                // If no saved context, get default context and associate
                var defaultContext = ImmGetContext(windowHandle);
                if (defaultContext != nint.Zero)
                {
                    ImmAssociateContext(windowHandle, defaultContext);
                    ImmReleaseContext(windowHandle, defaultContext);
                    Log.Debug("IME enabled with default context for window {Handle}", windowHandle);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to enable IME for window {Handle}", windowHandle);
        }
    }
}

