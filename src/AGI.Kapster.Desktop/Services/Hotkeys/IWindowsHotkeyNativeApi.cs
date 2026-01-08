using System;

namespace AGI.Kapster.Desktop.Services.Hotkeys;

public interface IWindowsHotkeyNativeApi
{
    bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    bool UnregisterHotKey(IntPtr hWnd, int id);

    int GetLastError();
}
