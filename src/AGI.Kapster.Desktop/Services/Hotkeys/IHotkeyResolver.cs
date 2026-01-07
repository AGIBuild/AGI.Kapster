using System;
using AGI.Kapster.Desktop.Models;

namespace AGI.Kapster.Desktop.Services.Hotkeys;

/// <summary>
/// Resolves a HotkeyGesture to a platform-specific registerable hotkey chord
/// Handles character-stable resolution based on current keyboard layout
/// </summary>
public interface IHotkeyResolver
{
    /// <summary>
    /// Resolve a hotkey gesture to a platform-specific registerable chord
    /// </summary>
    /// <param name="gesture">The hotkey gesture to resolve</param>
    /// <returns>Resolved hotkey with platform keycode and modifiers (including implicit modifiers), or null if unresolvable</returns>
    ResolvedHotkey? Resolve(HotkeyGesture gesture);
}

/// <summary>
/// Platform-specific resolved hotkey (keycode + modifiers ready for registration)
/// </summary>
public class ResolvedHotkey
{
    /// <summary>
    /// Platform-specific keycode (Windows VK, macOS kVK_*, etc.)
    /// </summary>
    public uint KeyCode { get; set; }

    /// <summary>
    /// Platform-specific modifiers (includes user-selected + implicit modifiers needed for character)
    /// </summary>
    public HotkeyModifiers Modifiers { get; set; }

    /// <summary>
    /// Display string showing the effective chord (e.g., "Shift+-" if '-' requires Shift)
    /// </summary>
    public string EffectiveDisplayString { get; set; } = string.Empty;

    public ResolvedHotkey(uint keyCode, HotkeyModifiers modifiers, string effectiveDisplayString)
    {
        KeyCode = keyCode;
        Modifiers = modifiers;
        EffectiveDisplayString = effectiveDisplayString;
    }
}



