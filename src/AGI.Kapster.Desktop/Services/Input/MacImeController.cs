using System;
using System.Runtime.InteropServices;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Input;

/// <summary>
/// macOS IME controller - switches to ASCII-capable input source to prevent
/// CJK input methods from interfering with keyboard shortcuts.
/// </summary>
public class MacImeController : IImeController
{
    private const string CarbonLib = "/System/Library/Frameworks/Carbon.framework/Carbon";

    // TIS (Text Input Source) API
    [DllImport(CarbonLib)]
    private static extern IntPtr TISCopyCurrentKeyboardInputSource();

    [DllImport(CarbonLib)]
    private static extern IntPtr TISCopyCurrentASCIICapableKeyboardInputSource();

    [DllImport(CarbonLib)]
    private static extern int TISSelectInputSource(IntPtr inputSource);

    // CoreFoundation
    [DllImport(CarbonLib)]
    private static extern void CFRetain(IntPtr cf);

    [DllImport(CarbonLib)]
    private static extern void CFRelease(IntPtr cf);

    private IntPtr _savedInputSource = IntPtr.Zero;

    public bool IsSupported => OperatingSystem.IsMacOS();

    public void DisableIme(nint windowHandle)
    {
        if (!IsSupported)
            return;

        try
        {
            // Save current input source
            var current = TISCopyCurrentKeyboardInputSource();
            if (current != IntPtr.Zero)
            {
                // Release any previously saved input source
                if (_savedInputSource != IntPtr.Zero)
                {
                    CFRelease(_savedInputSource);
                }
                _savedInputSource = current;
                // Note: TISCopy* returns a retained object, so we own it
            }

            // Switch to ASCII-capable input source (typically US keyboard)
            var asciiSource = TISCopyCurrentASCIICapableKeyboardInputSource();
            if (asciiSource != IntPtr.Zero)
            {
                var result = TISSelectInputSource(asciiSource);
                CFRelease(asciiSource);

                if (result == 0)
                {
                    Log.Debug("macOS IME disabled - switched to ASCII input source");
                }
                else
                {
                    Log.Warning("Failed to switch to ASCII input source, result={Result}", result);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to disable IME on macOS");
        }
    }

    public void EnableIme(nint windowHandle)
    {
        if (!IsSupported)
            return;

        try
        {
            // Restore saved input source
            if (_savedInputSource != IntPtr.Zero)
            {
                var result = TISSelectInputSource(_savedInputSource);
                CFRelease(_savedInputSource);
                _savedInputSource = IntPtr.Zero;

                if (result == 0)
                {
                    Log.Debug("macOS IME enabled - restored previous input source");
                }
                else
                {
                    Log.Warning("Failed to restore input source, result={Result}", result);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to enable IME on macOS");
        }
    }
}

