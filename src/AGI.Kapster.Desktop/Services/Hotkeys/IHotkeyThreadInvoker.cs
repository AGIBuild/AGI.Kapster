using System;

namespace AGI.Kapster.Desktop.Services.Hotkeys;

public interface IHotkeyThreadInvoker
{
    T Invoke<T>(Func<T> func, T fallback);
}
