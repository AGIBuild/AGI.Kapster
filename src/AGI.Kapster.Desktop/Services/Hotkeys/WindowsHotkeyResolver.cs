using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AGI.Kapster.Desktop.Models;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Hotkeys;

/// <summary>
/// Windows hotkey resolver - resolves HotkeyGesture to Windows VK + modifiers
/// Uses Win32 layout APIs for character-stable resolution
/// </summary>
public class WindowsHotkeyResolver : IHotkeyResolver
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern short VkKeyScanEx(char ch, IntPtr hkl);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(int idThread);

    public ResolvedHotkey? Resolve(HotkeyGesture gesture)
    {
        if (gesture?.KeySpec == null)
            return null;

        try
        {
            if (gesture.KeySpec is CharKeySpec charSpec)
            {
                return ResolveChar(charSpec.Character, gesture.Modifiers);
            }
            else if (gesture.KeySpec is NamedKeySpec namedSpec)
            {
                return ResolveNamedKey(namedSpec.NamedKey, gesture.Modifiers);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to resolve hotkey gesture: {Gesture}", gesture?.ToDisplayString());
        }

        return null;
    }

    private ResolvedHotkey? ResolveChar(char character, HotkeyModifiers userModifiers)
    {
        // For letter characters, use the lowercase form to get the VK code
        // This avoids adding implicit Shift modifier for uppercase letters
        // (e.g., 'A' would otherwise require Shift+'a', but we want just the 'A' key)
        var lookupChar = character;
        if (character >= 'A' && character <= 'Z')
        {
            lookupChar = char.ToLowerInvariant(character);
        }

        // Get current keyboard layout
        var hkl = GetKeyboardLayout(0);

        // Use VkKeyScanEx to get VK + shift state for the character
        var scanResult = VkKeyScanEx(lookupChar, hkl);

        if (scanResult == -1)
        {
            Log.Warning("Character '{Char}' cannot be resolved to a VK under current keyboard layout", character);
            return null;
        }

        // Extract VK (low byte) and shift state (high byte)
        var vk = (byte)(scanResult & 0xFF);
        var shiftState = (byte)((scanResult >> 8) & 0xFF);

        // Build modifiers: user modifiers + implicit modifiers from shift state
        var modifiers = userModifiers;

        // Check if shift is required (bit 0 = Shift, bit 1 = Control, bit 2 = Alt, bit 3 = Hankaku, bit 4 = Reserved, bit 5 = Shift pressed)
        if ((shiftState & 0x01) != 0)
        {
            modifiers |= HotkeyModifiers.Shift;
        }
        if ((shiftState & 0x02) != 0)
        {
            modifiers |= HotkeyModifiers.Control;
        }
        if ((shiftState & 0x04) != 0)
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        // Build effective display string
        var effectiveParts = new List<string>();
        if ((modifiers & HotkeyModifiers.Control) != 0) effectiveParts.Add("Ctrl");
        if ((modifiers & HotkeyModifiers.Alt) != 0) effectiveParts.Add("Alt");
        if ((modifiers & HotkeyModifiers.Shift) != 0) effectiveParts.Add("Shift");
        if ((modifiers & HotkeyModifiers.Win) != 0) effectiveParts.Add("Win");
        effectiveParts.Add(character.ToString());
        var effectiveDisplay = string.Join("+", effectiveParts);

        return new ResolvedHotkey(vk, modifiers, effectiveDisplay);
    }

    private ResolvedHotkey? ResolveNamedKey(NamedKey namedKey, HotkeyModifiers modifiers)
    {
        var vk = NamedKeyToVK(namedKey);
        if (vk == 0)
        {
            Log.Warning("Named key {NamedKey} cannot be resolved to a VK", namedKey);
            return null;
        }

        var effectiveParts = new List<string>();
        if ((modifiers & HotkeyModifiers.Control) != 0) effectiveParts.Add("Ctrl");
        if ((modifiers & HotkeyModifiers.Alt) != 0) effectiveParts.Add("Alt");
        if ((modifiers & HotkeyModifiers.Shift) != 0) effectiveParts.Add("Shift");
        if ((modifiers & HotkeyModifiers.Win) != 0) effectiveParts.Add("Win");
        effectiveParts.Add(GetNamedKeyDisplay(namedKey));
        var effectiveDisplay = string.Join("+", effectiveParts);

        return new ResolvedHotkey(vk, modifiers, effectiveDisplay);
    }

    private static uint NamedKeyToVK(NamedKey namedKey)
    {
        return namedKey switch
        {
            NamedKey.F1 => 0x70,
            NamedKey.F2 => 0x71,
            NamedKey.F3 => 0x72,
            NamedKey.F4 => 0x73,
            NamedKey.F5 => 0x74,
            NamedKey.F6 => 0x75,
            NamedKey.F7 => 0x76,
            NamedKey.F8 => 0x77,
            NamedKey.F9 => 0x78,
            NamedKey.F10 => 0x79,
            NamedKey.F11 => 0x7A,
            NamedKey.F12 => 0x7B,
            NamedKey.F13 => 0x7C,
            NamedKey.F14 => 0x7D,
            NamedKey.F15 => 0x7E,
            NamedKey.F16 => 0x7F,
            NamedKey.F17 => 0x80,
            NamedKey.F18 => 0x81,
            NamedKey.F19 => 0x82,
            NamedKey.F20 => 0x83,
            NamedKey.F21 => 0x84,
            NamedKey.F22 => 0x85,
            NamedKey.F23 => 0x86,
            NamedKey.F24 => 0x87,
            NamedKey.Enter => 0x0D,
            NamedKey.Tab => 0x09,
            NamedKey.Escape => 0x1B,
            NamedKey.Space => 0x20,
            NamedKey.Backspace => 0x08,
            NamedKey.Delete => 0x2E,
            NamedKey.Insert => 0x2D,
            NamedKey.Home => 0x24,
            NamedKey.End => 0x23,
            NamedKey.PageUp => 0x21,
            NamedKey.PageDown => 0x22,
            NamedKey.Up => 0x26,
            NamedKey.Down => 0x28,
            NamedKey.Left => 0x25,
            NamedKey.Right => 0x27,
            _ => 0
        };
    }

    private static string GetNamedKeyDisplay(NamedKey namedKey)
    {
        return namedKey switch
        {
            NamedKey.F1 => "F1",
            NamedKey.F2 => "F2",
            NamedKey.F3 => "F3",
            NamedKey.F4 => "F4",
            NamedKey.F5 => "F5",
            NamedKey.F6 => "F6",
            NamedKey.F7 => "F7",
            NamedKey.F8 => "F8",
            NamedKey.F9 => "F9",
            NamedKey.F10 => "F10",
            NamedKey.F11 => "F11",
            NamedKey.F12 => "F12",
            NamedKey.Enter => "Enter",
            NamedKey.Tab => "Tab",
            NamedKey.Escape => "Esc",
            NamedKey.Space => "Space",
            NamedKey.Backspace => "Backspace",
            NamedKey.Delete => "Delete",
            NamedKey.Insert => "Insert",
            NamedKey.Home => "Home",
            NamedKey.End => "End",
            NamedKey.PageUp => "PageUp",
            NamedKey.PageDown => "PageDown",
            NamedKey.Up => "Up",
            NamedKey.Down => "Down",
            NamedKey.Left => "Left",
            NamedKey.Right => "Right",
            _ => namedKey.ToString()
        };
    }
}

