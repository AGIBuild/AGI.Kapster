using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AGI.Kapster.Desktop.Models;

/// <summary>
/// Represents a hotkey gesture: modifiers + key specification
/// </summary>
public class HotkeyGesture
{
    /// <summary>
    /// Modifier keys (Alt, Control, Shift, Win/Command)
    /// </summary>
    public HotkeyModifiers Modifiers { get; set; }

    /// <summary>
    /// Key specification (character or named key)
    /// </summary>
    public HotkeyKeySpec KeySpec { get; set; } = null!;

    /// <summary>
    /// Default constructor for JSON deserialization
    /// </summary>
    public HotkeyGesture()
    {
    }

    /// <summary>
    /// Create a hotkey gesture with modifiers and key spec
    /// </summary>
    public HotkeyGesture(HotkeyModifiers modifiers, HotkeyKeySpec keySpec)
    {
        Modifiers = modifiers;
        KeySpec = keySpec ?? throw new ArgumentNullException(nameof(keySpec));
    }

    /// <summary>
    /// Create a hotkey gesture from a character key
    /// </summary>
    public static HotkeyGesture FromChar(HotkeyModifiers modifiers, char character)
    {
        return new HotkeyGesture(modifiers, new CharKeySpec(character));
    }

    /// <summary>
    /// Create a hotkey gesture from a named key
    /// </summary>
    public static HotkeyGesture FromNamedKey(HotkeyModifiers modifiers, NamedKey namedKey)
    {
        return new HotkeyGesture(modifiers, new NamedKeySpec(namedKey));
    }

    /// <summary>
    /// Get display string for UI (e.g., "Alt+A", "Ctrl+Shift+-")
    /// </summary>
    public string ToDisplayString()
    {
        var parts = new List<string>();

        if ((Modifiers & HotkeyModifiers.Control) != 0)
            parts.Add("Ctrl");
        if ((Modifiers & HotkeyModifiers.Alt) != 0)
            parts.Add("Alt");
        if ((Modifiers & HotkeyModifiers.Shift) != 0)
            parts.Add("Shift");
        if ((Modifiers & HotkeyModifiers.Win) != 0)
            parts.Add(OperatingSystem.IsMacOS() ? "Cmd" : "Win");

        parts.Add(KeySpec.ToDisplayString());

        return string.Join("+", parts);
    }

    public override bool Equals(object? obj)
    {
        return obj is HotkeyGesture other &&
               Modifiers == other.Modifiers &&
               KeySpec.Equals(other.KeySpec);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Modifiers, KeySpec);
    }
}

/// <summary>
/// Base class for hotkey key specifications
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(CharKeySpec), typeDiscriminator: "char")]
[JsonDerivedType(typeof(NamedKeySpec), typeDiscriminator: "named")]
public abstract class HotkeyKeySpec
{
    public abstract string ToDisplayString();
}

/// <summary>
/// Character-based key specification (for printable characters like '-', '[', ';', etc.)
/// </summary>
public class CharKeySpec : HotkeyKeySpec
{
    /// <summary>
    /// The character to use as the hotkey (must be a single printable character)
    /// </summary>
    public char Character { get; set; }

    public CharKeySpec()
    {
    }

    public CharKeySpec(char character)
    {
        Character = character;
    }

    public override string ToDisplayString()
    {
        // Map common characters to readable names
        return Character switch
        {
            ' ' => "Space",
            '-' => "-",
            '=' => "=",
            '[' => "[",
            ']' => "]",
            ';' => ";",
            '\'' => "'",
            ',' => ",",
            '.' => ".",
            '/' => "/",
            '\\' => "\\",
            '`' => "`",
            _ => Character.ToString()
        };
    }

    public override bool Equals(object? obj)
    {
        return obj is CharKeySpec other && Character == other.Character;
    }

    public override int GetHashCode()
    {
        return Character.GetHashCode();
    }
}

/// <summary>
/// Named key specification (for function keys, navigation keys, etc.)
/// </summary>
public class NamedKeySpec : HotkeyKeySpec
{
    /// <summary>
    /// The named key (F1-F24, Enter, Tab, Esc, arrows, etc.)
    /// </summary>
    public NamedKey NamedKey { get; set; }

    public NamedKeySpec()
    {
    }

    public NamedKeySpec(NamedKey namedKey)
    {
        NamedKey = namedKey;
    }

    public override string ToDisplayString()
    {
        return NamedKey switch
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
            _ => NamedKey.ToString()
        };
    }

    public override bool Equals(object? obj)
    {
        return obj is NamedKeySpec other && NamedKey == other.NamedKey;
    }

    public override int GetHashCode()
    {
        return NamedKey.GetHashCode();
    }
}

/// <summary>
/// Named keys (function keys, navigation keys, etc.)
/// </summary>
public enum NamedKey
{
    // Function keys
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    F13, F14, F15, F16, F17, F18, F19, F20, F21, F22, F23, F24,

    // Navigation and editing
    Enter,
    Tab,
    Escape,
    Space,
    Backspace,
    Delete,
    Insert,
    Home,
    End,
    PageUp,
    PageDown,
    Up,
    Down,
    Left,
    Right
}

/// <summary>
/// Hotkey modifiers (flags enum)
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}

