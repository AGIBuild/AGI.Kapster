using System;
using System.Collections.Generic;
using AGI.Kapster.Desktop.Models;
using Serilog;

namespace AGI.Kapster.Desktop.Services.Annotation;

/// <summary>
/// Centralized annotation tool hotkey management
/// </summary>
public static class AnnotationToolHotkeys
{
    /// <summary>
    /// Tool hotkey mappings
    /// </summary>
    public static readonly Dictionary<AnnotationToolType, string> ToolHotkeys = new()
    {
        { AnnotationToolType.None, "S" },      // None is used for selection tool
        { AnnotationToolType.Arrow, "A" },
        { AnnotationToolType.Rectangle, "R" },
        { AnnotationToolType.Ellipse, "E" },
        { AnnotationToolType.Text, "T" },
        { AnnotationToolType.Freehand, "F" },
        { AnnotationToolType.Mosaic, "M" },
        { AnnotationToolType.Emoji, "J" }
    };

    /// <summary>
    /// Get hotkey for a specific tool
    /// </summary>
    /// <param name="tool">The annotation tool</param>
    /// <returns>Hotkey string, or null if not found</returns>
    public static string? GetHotkey(AnnotationToolType tool)
    {
        return ToolHotkeys.TryGetValue(tool, out var hotkey) ? hotkey : null;
    }

    /// <summary>
    /// Get tool type from hotkey
    /// </summary>
    /// <param name="hotkey">The hotkey string</param>
    /// <returns>Tool type, or null if not found</returns>
    public static AnnotationToolType? GetToolFromHotkey(string hotkey)
    {
        if (string.IsNullOrEmpty(hotkey))
            return null;

        var upperHotkey = hotkey.ToUpperInvariant();
        foreach (var kvp in ToolHotkeys)
        {
            if (kvp.Value.Equals(upperHotkey, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Key;
            }
        }

        return null;
    }

    /// <summary>
    /// Check if a hotkey is a tool hotkey
    /// </summary>
    /// <param name="hotkey">The hotkey string</param>
    /// <returns>True if it's a tool hotkey</returns>
    public static bool IsToolHotkey(string hotkey)
    {
        return GetToolFromHotkey(hotkey) != null;
    }

    /// <summary>
    /// Get all tool hotkey descriptions for display
    /// </summary>
    /// <returns>Dictionary of tool descriptions</returns>
    public static Dictionary<AnnotationToolType, string> GetToolDescriptions()
    {
        return new Dictionary<AnnotationToolType, string>
        {
            { AnnotationToolType.None, "Select and edit annotation elements" },
            { AnnotationToolType.Arrow, "Draw pointing arrows" },
            { AnnotationToolType.Rectangle, "Draw rectangle frames" },
            { AnnotationToolType.Ellipse, "Draw ellipses" },
            { AnnotationToolType.Text, "Add text annotations" },
            { AnnotationToolType.Freehand, "Free drawing" },
            { AnnotationToolType.Mosaic, "Pixelate and blur regions" },
            { AnnotationToolType.Emoji, "Insert emoji symbols" }
        };
    }

    /// <summary>
    /// Log all tool hotkeys for debugging
    /// </summary>
    public static void LogToolHotkeys()
    {
        Log.Debug("Annotation tool hotkeys:");
        foreach (var kvp in ToolHotkeys)
        {
            Log.Debug("  {Tool}: {Hotkey}", kvp.Key, kvp.Value);
        }
    }
}
