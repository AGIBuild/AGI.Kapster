using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using AGI.Kapster.Desktop.Models;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Hotkeys;

/// <summary>
/// macOS hotkey resolver - resolves HotkeyGesture to macOS kVK_* + modifiers
/// Uses Core Foundation layout APIs for character-stable resolution
/// </summary>
public class MacHotkeyResolver : IHotkeyResolver
{
    [DllImport("/System/Library/Frameworks/Carbon.framework/Carbon", CallingConvention = CallingConvention.Cdecl, EntryPoint = "TISCopyCurrentKeyboardInputSource", SetLastError = true)]
    private static extern IntPtr TISCopyCurrentKeyboardInputSource();

    [DllImport("/System/Library/Frameworks/Carbon.framework/Carbon", CallingConvention = CallingConvention.Cdecl, EntryPoint = "TISGetInputSourceProperty", SetLastError = true)]
    private static extern IntPtr TISGetInputSourceProperty(IntPtr inputSource, IntPtr propertyKey);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string cStr, uint encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", CallingConvention = CallingConvention.Cdecl)]
    private static extern void CFRelease(IntPtr cf);

    [DllImport("/System/Library/Frameworks/Carbon.framework/Carbon")]
    private static extern uint UCKeyTranslate(IntPtr keyLayoutPtr, ushort keyCode, ushort keyAction, ushort modifierKeyState, uint keyboardType, uint keyTranslateOptions, ref uint deadKeyState, ushort maxStringLength, ref ushort actualStringLength, IntPtr unicodeString);

    private const uint kCFStringEncodingUTF8 = 0x08000100;
    private static readonly IntPtr kTISPropertyUnicodeKeyLayoutData = CFStringCreateWithCString(IntPtr.Zero, "TISPropertyUnicodeKeyLayoutData", kCFStringEncodingUTF8);

    public ResolvedHotkey? Resolve(HotkeyGesture gesture)
    {
        if (gesture?.KeySpec == null)
            return null;

        try
        {
            if (gesture.KeySpec is CharKeySpec charSpec)
            {
                Log.Debug("Resolving character hotkey: {Char} with modifiers {Modifiers}", charSpec.Character, gesture.Modifiers);
                return ResolveChar(charSpec.Character, gesture.Modifiers);
            }
            else if (gesture.KeySpec is NamedKeySpec namedSpec)
            {
                Log.Debug("Resolving named hotkey: {NamedKey} with modifiers {Modifiers}", namedSpec.NamedKey, gesture.Modifiers);
                return ResolveNamedKey(namedSpec.NamedKey, gesture.Modifiers);
            }
        }
        catch (EntryPointNotFoundException ex)
        {
            Log.Warning(ex, "TIS API not available, using fallback for gesture: {Gesture}", gesture?.ToDisplayString());
            // Try fallback
            if (gesture?.KeySpec is CharKeySpec charSpec)
            {
                return ResolveCharFallback(charSpec.Character, gesture.Modifiers);
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
        // For letters, use standard ANSI key codes directly (they are layout-independent)
        // Note: macOS virtual key codes are NOT sequential by letter!
        // This should be done FIRST, before any TIS API calls
        if (character >= 'A' && character <= 'Z')
        {
            var standardKeyCode = CharToMacVKLetter(character);
            if (standardKeyCode != uint.MaxValue)
            {
                var effectiveParts = new List<string>();
                if ((userModifiers & HotkeyModifiers.Control) != 0) effectiveParts.Add("Ctrl");
                if ((userModifiers & HotkeyModifiers.Alt) != 0) effectiveParts.Add("Alt");
                if ((userModifiers & HotkeyModifiers.Shift) != 0) effectiveParts.Add("Shift");
                if ((userModifiers & HotkeyModifiers.Win) != 0) effectiveParts.Add("Cmd");
                effectiveParts.Add(character.ToString());
                Log.Debug("Resolved character '{Char}' to keyCode {KeyCode} (standard ANSI, direct)", character, standardKeyCode);
                return new ResolvedHotkey(standardKeyCode, userModifiers, string.Join("+", effectiveParts));
            }
        }
        else if (character >= 'a' && character <= 'z')
        {
            // Lowercase: same keycode as uppercase
            var standardKeyCode = CharToMacVKLetter(char.ToUpper(character));
            if (standardKeyCode != uint.MaxValue)
            {
                var effectiveParts = new List<string>();
                if ((userModifiers & HotkeyModifiers.Control) != 0) effectiveParts.Add("Ctrl");
                if ((userModifiers & HotkeyModifiers.Alt) != 0) effectiveParts.Add("Alt");
                if ((userModifiers & HotkeyModifiers.Shift) != 0) effectiveParts.Add("Shift");
                if ((userModifiers & HotkeyModifiers.Win) != 0) effectiveParts.Add("Cmd");
                effectiveParts.Add(character.ToString());
                Log.Debug("Resolved character '{Char}' to keyCode {KeyCode} (standard ANSI, direct)", character, standardKeyCode);
                return new ResolvedHotkey(standardKeyCode, userModifiers, string.Join("+", effectiveParts));
            }
        }

        // For non-letter characters, try TIS API
        try
        {
            // Get current keyboard layout
            var inputSource = TISCopyCurrentKeyboardInputSource();
            if (inputSource == IntPtr.Zero)
            {
                // Fallback to simple mapping if TIS API unavailable
                Log.Debug("TIS API returned zero, using fallback for character '{Char}'", character);
                return ResolveCharFallback(character, userModifiers);
            }

            try
            {
                var layoutData = TISGetInputSourceProperty(inputSource, kTISPropertyUnicodeKeyLayoutData);
                if (layoutData == IntPtr.Zero)
                {
                    // Fallback to simple mapping
                    Log.Debug("Layout data is zero, using fallback for character '{Char}'", character);
                    return ResolveCharFallback(character, userModifiers);
                }

                // For other characters, iterate through common keycodes
                for (uint keyCode = 0; keyCode < 128; keyCode++)
                {
                    // Try without modifiers first
                    var result = TryKeyCodeWithModifiers(layoutData, (ushort)keyCode, 0, character);
                    if (result != null)
                    {
                        var effectiveParts = new List<string>();
                        if ((userModifiers & HotkeyModifiers.Control) != 0) effectiveParts.Add("Ctrl");
                        if ((userModifiers & HotkeyModifiers.Alt) != 0) effectiveParts.Add("Alt");
                        if ((userModifiers & HotkeyModifiers.Shift) != 0) effectiveParts.Add("Shift");
                        if ((userModifiers & HotkeyModifiers.Win) != 0) effectiveParts.Add("Cmd");
                        effectiveParts.Add(character.ToString());
                        Log.Debug("Resolved character '{Char}' to keyCode {KeyCode} (layout-specific)", character, keyCode);
                        return new ResolvedHotkey(keyCode, userModifiers, string.Join("+", effectiveParts));
                    }

                    // Try with Shift
                    result = TryKeyCodeWithModifiers(layoutData, (ushort)keyCode, 0x02, character); // Shift flag
                    if (result != null)
                    {
                        var modifiers = userModifiers | HotkeyModifiers.Shift;
                        var effectiveParts = new List<string>();
                        if ((modifiers & HotkeyModifiers.Control) != 0) effectiveParts.Add("Ctrl");
                        if ((modifiers & HotkeyModifiers.Alt) != 0) effectiveParts.Add("Alt");
                        if ((modifiers & HotkeyModifiers.Shift) != 0) effectiveParts.Add("Shift");
                        if ((modifiers & HotkeyModifiers.Win) != 0) effectiveParts.Add("Cmd");
                        effectiveParts.Add(character.ToString());
                        Log.Debug("Resolved character '{Char}' to keyCode {KeyCode} with Shift (layout-specific)", character, keyCode);
                        return new ResolvedHotkey(keyCode, modifiers, string.Join("+", effectiveParts));
                    }

                    // Try with Option (Alt)
                    result = TryKeyCodeWithModifiers(layoutData, (ushort)keyCode, 0x08, character); // Option flag
                    if (result != null)
                    {
                        var modifiers = userModifiers | HotkeyModifiers.Alt;
                        var effectiveParts = new List<string>();
                        if ((modifiers & HotkeyModifiers.Control) != 0) effectiveParts.Add("Ctrl");
                        if ((modifiers & HotkeyModifiers.Alt) != 0) effectiveParts.Add("Alt");
                        if ((modifiers & HotkeyModifiers.Shift) != 0) effectiveParts.Add("Shift");
                        if ((modifiers & HotkeyModifiers.Win) != 0) effectiveParts.Add("Cmd");
                        effectiveParts.Add(character.ToString());
                        Log.Debug("Resolved character '{Char}' to keyCode {KeyCode} with Option (layout-specific)", character, keyCode);
                        return new ResolvedHotkey(keyCode, modifiers, string.Join("+", effectiveParts));
                    }
                }

                Log.Warning("Character '{Char}' cannot be resolved to a kVK under current keyboard layout, using fallback", character);
                return ResolveCharFallback(character, userModifiers);
            }
            finally
            {
                CFRelease(inputSource);
            }
        }
        catch (EntryPointNotFoundException)
        {
            // TIS API not available, use fallback
            Log.Debug("TIS API not available, using fallback character resolution");
            return ResolveCharFallback(character, userModifiers);
        }
    }

    /// <summary>
    /// Fallback character resolution using simple US keyboard layout mapping
    /// Used when TIS API is unavailable
    /// </summary>
    private ResolvedHotkey? ResolveCharFallback(char character, HotkeyModifiers userModifiers)
    {
        // Simple mapping for common characters (US keyboard layout)
        // This is a fallback when TIS API is not available
        var keyCode = CharToMacVKFallback(character);
        if (keyCode == uint.MaxValue)
        {
            Log.Warning("Character '{Char}' cannot be mapped to macOS kVK (fallback)", character);
            return null;
        }

        var effectiveParts = new List<string>();
        if ((userModifiers & HotkeyModifiers.Control) != 0) effectiveParts.Add("Ctrl");
        if ((userModifiers & HotkeyModifiers.Alt) != 0) effectiveParts.Add("Alt");
        if ((userModifiers & HotkeyModifiers.Shift) != 0) effectiveParts.Add("Shift");
        if ((userModifiers & HotkeyModifiers.Win) != 0) effectiveParts.Add("Cmd");
        effectiveParts.Add(character.ToString());

        Log.Debug("Resolved character '{Char}' to keyCode {KeyCode} (fallback)", character, keyCode);
        return new ResolvedHotkey(keyCode, userModifiers, string.Join("+", effectiveParts));
    }

    /// <summary>
    /// Maps a letter (A-Z) to macOS virtual key code.
    /// Note: macOS virtual key codes are NOT sequential by letter!
    /// </summary>
    private static uint CharToMacVKLetter(char letter)
    {
        // macOS ANSI key codes (kVK_ANSI_*) - these are NOT sequential!
        return letter switch
        {
            'A' => 0x00, // kVK_ANSI_A
            'S' => 0x01, // kVK_ANSI_S
            'D' => 0x02, // kVK_ANSI_D
            'F' => 0x03, // kVK_ANSI_F
            'H' => 0x04, // kVK_ANSI_H
            'G' => 0x05, // kVK_ANSI_G
            'Z' => 0x06, // kVK_ANSI_Z
            'X' => 0x07, // kVK_ANSI_X
            'C' => 0x08, // kVK_ANSI_C
            'V' => 0x09, // kVK_ANSI_V
            'B' => 0x0B, // kVK_ANSI_B (note: 0x0A is Tab)
            'Q' => 0x0C, // kVK_ANSI_Q
            'W' => 0x0D, // kVK_ANSI_W
            'E' => 0x0E, // kVK_ANSI_E
            'R' => 0x0F, // kVK_ANSI_R
            'Y' => 0x10, // kVK_ANSI_Y
            'T' => 0x11, // kVK_ANSI_T
            '1' => 0x12, // kVK_ANSI_1
            '2' => 0x13, // kVK_ANSI_2
            '3' => 0x14, // kVK_ANSI_3
            '4' => 0x15, // kVK_ANSI_4
            '6' => 0x16, // kVK_ANSI_6
            '5' => 0x17, // kVK_ANSI_5
            '=' => 0x18, // kVK_ANSI_Equal
            '9' => 0x19, // kVK_ANSI_9
            '7' => 0x1A, // kVK_ANSI_7
            '-' => 0x1B, // kVK_ANSI_Minus
            '8' => 0x1C, // kVK_ANSI_8
            '0' => 0x1D, // kVK_ANSI_0
            ']' => 0x1E, // kVK_ANSI_RightBracket
            'O' => 0x1F, // kVK_ANSI_O
            'U' => 0x20, // kVK_ANSI_U
            '[' => 0x21, // kVK_ANSI_LeftBracket
            'I' => 0x22, // kVK_ANSI_I
            'P' => 0x23, // kVK_ANSI_P
            'L' => 0x25, // kVK_ANSI_L
            'J' => 0x26, // kVK_ANSI_J
            '\'' => 0x27, // kVK_ANSI_Quote
            'K' => 0x28, // kVK_ANSI_K
            ';' => 0x29, // kVK_ANSI_Semicolon
            '\\' => 0x2A, // kVK_ANSI_Backslash
            ',' => 0x2B, // kVK_ANSI_Comma
            '/' => 0x2C, // kVK_ANSI_Slash
            'N' => 0x2D, // kVK_ANSI_N
            'M' => 0x2E, // kVK_ANSI_M
            '.' => 0x2F, // kVK_ANSI_Period
            '`' => 0x32, // kVK_ANSI_Grave
            ' ' => 0x31, // kVK_Space
            _ => uint.MaxValue
        };
    }

    /// <summary>
    /// Simple character to macOS VK mapping (US keyboard layout)
    /// </summary>
    private static uint CharToMacVKFallback(char character)
    {
        // For letters, use the proper mapping
        if (character >= 'A' && character <= 'Z')
        {
            return CharToMacVKLetter(character);
        }
        if (character >= 'a' && character <= 'z')
        {
            return CharToMacVKLetter(char.ToUpper(character));
        }
        if (character >= '0' && character <= '9')
        {
            return (uint)(0x12 + (character - '0')); // kVK_ANSI_1 = 0x12
        }

        // Common symbols (US layout)
        return character switch
        {
            '-' => 0x1B, // kVK_ANSI_Minus
            '=' => 0x18, // kVK_ANSI_Equal
            '[' => 0x21, // kVK_ANSI_LeftBracket
            ']' => 0x1E, // kVK_ANSI_RightBracket
            ';' => 0x29, // kVK_ANSI_Semicolon
            '\'' => 0x27, // kVK_ANSI_Quote
            ',' => 0x2B, // kVK_ANSI_Comma
            '.' => 0x2F, // kVK_ANSI_Period
            '/' => 0x2C, // kVK_ANSI_Slash
            '\\' => 0x2A, // kVK_ANSI_Backslash
            '`' => 0x32, // kVK_ANSI_Grave
            ' ' => 0x31, // kVK_Space
            _ => uint.MaxValue
        };
    }

    private char? TryKeyCodeWithModifiers(IntPtr layoutData, ushort keyCode, ushort modifierState, char targetChar)
    {
        uint deadKeyState = 0;
        ushort actualLength = 0;
        IntPtr buffer = IntPtr.Zero;

        try
        {
            // UCKeyTranslate writes UTF-16 code units (UniChar). We only request 1.
            buffer = Marshal.AllocHGlobal(sizeof(ushort));
            var result = UCKeyTranslate(layoutData, keyCode, 0, modifierState, 0, 0, ref deadKeyState, 1, ref actualLength, buffer);

            if (result == 0 && actualLength == 1)
            {
                var unicodeChar = (ushort)Marshal.ReadInt16(buffer);
                if (unicodeChar == targetChar)
                {
                    return (char)unicodeChar;
                }
            }

            return null;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    private ResolvedHotkey? ResolveNamedKey(NamedKey namedKey, HotkeyModifiers modifiers)
    {
        var keyCode = NamedKeyToMacVK(namedKey);
        if (keyCode == uint.MaxValue)
        {
            Log.Warning("Named key {NamedKey} cannot be resolved to a macOS kVK", namedKey);
            return null;
        }

        var effectiveParts = new List<string>();
        if ((modifiers & HotkeyModifiers.Control) != 0) effectiveParts.Add("Ctrl");
        if ((modifiers & HotkeyModifiers.Alt) != 0) effectiveParts.Add("Alt");
        if ((modifiers & HotkeyModifiers.Shift) != 0) effectiveParts.Add("Shift");
        if ((modifiers & HotkeyModifiers.Win) != 0) effectiveParts.Add("Cmd");
        effectiveParts.Add(GetNamedKeyDisplay(namedKey));
        var effectiveDisplay = string.Join("+", effectiveParts);

        return new ResolvedHotkey(keyCode, modifiers, effectiveDisplay);
    }

    private static uint NamedKeyToMacVK(NamedKey namedKey)
    {
        // macOS virtual key codes (kVK_ANSI_*)
        return namedKey switch
        {
            NamedKey.F1 => 0x7A,
            NamedKey.F2 => 0x78,
            NamedKey.F3 => 0x63,
            NamedKey.F4 => 0x76,
            NamedKey.F5 => 0x60,
            NamedKey.F6 => 0x61,
            NamedKey.F7 => 0x62,
            NamedKey.F8 => 0x64,
            NamedKey.F9 => 0x65,
            NamedKey.F10 => 0x6D,
            NamedKey.F11 => 0x67,
            NamedKey.F12 => 0x6F,
            NamedKey.F13 => 0x69,
            NamedKey.F14 => 0x6B,
            NamedKey.F15 => 0x71,
            NamedKey.F16 => 0x6A,
            NamedKey.F17 => 0x40,
            NamedKey.F18 => 0x4F,
            NamedKey.F19 => 0x50,
            NamedKey.F20 => 0x5A,
            NamedKey.Enter => 0x24,
            NamedKey.Tab => 0x30,
            NamedKey.Escape => 0x35,
            NamedKey.Space => 0x31,
            NamedKey.Backspace => 0x33,
            NamedKey.Delete => 0x75,
            NamedKey.Insert => 0x72,
            NamedKey.Home => 0x73,
            NamedKey.End => 0x77,
            NamedKey.PageUp => 0x74,
            NamedKey.PageDown => 0x79,
            NamedKey.Up => 0x7E,
            NamedKey.Down => 0x7D,
            NamedKey.Left => 0x7B,
            NamedKey.Right => 0x7C,
            _ => uint.MaxValue
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

