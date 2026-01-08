using System;
using System.Runtime.InteropServices;

namespace AGI.Kapster.Desktop.Services.Hotkeys;

public sealed class WindowsHotkeyNativeApi : IWindowsHotkeyNativeApi
{
    public bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk)
        => RegisterHotKeyPInvoke(hWnd, id, fsModifiers, vk);

    public bool UnregisterHotKey(IntPtr hWnd, int id)
        => UnregisterHotKeyPInvoke(hWnd, id);

    public int GetLastError() => Marshal.GetLastWin32Error();

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "RegisterHotKey")]
    private static extern bool RegisterHotKeyPInvoke(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "UnregisterHotKey")]
    private static extern bool UnregisterHotKeyPInvoke(IntPtr hWnd, int id);
}
