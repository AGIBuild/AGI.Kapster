using System;
using System.Threading.Tasks;

namespace AGI.Captor.App.Services.Hotkeys;

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}

public readonly struct HotkeyChord
{
    public readonly HotkeyModifiers Modifiers;
    public readonly uint VirtualKey;

    public HotkeyChord(HotkeyModifiers modifiers, uint virtualKey)
    {
        Modifiers = modifiers;
        VirtualKey = virtualKey;
    }

    public override string ToString() => $"{Modifiers}+0x{VirtualKey:X2}";
}

public interface IHotkeyProvider : IDisposable
{
    bool Register(string id, HotkeyChord chord, Action callback);
    void Unregister(string id);
    Task<bool> UnregisterAsync(string id);
}


