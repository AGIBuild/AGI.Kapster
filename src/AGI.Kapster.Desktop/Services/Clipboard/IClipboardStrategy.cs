using System.Threading.Tasks;
using SkiaSharp;

namespace AGI.Kapster.Desktop.Services.Clipboard;

/// <summary>
/// Platform-specific clipboard strategy
/// </summary>
public interface IClipboardStrategy
{
    /// <summary>
    /// Sets an image to the clipboard
    /// </summary>
    Task<bool> SetImageAsync(SKBitmap bitmap);

    /// <summary>
    /// Sets text to the clipboard
    /// </summary>
    Task<bool> SetTextAsync(string text);

    /// <summary>
    /// Gets an image from the clipboard
    /// </summary>
    Task<SKBitmap?> GetImageAsync();

    /// <summary>
    /// Gets text from the clipboard
    /// </summary>
    Task<string?> GetTextAsync();

    /// <summary>
    /// Clears the clipboard
    /// </summary>
    Task<bool> ClearAsync();

    /// <summary>
    /// Gets whether the platform supports multiple clipboard formats simultaneously
    /// </summary>
    bool SupportsMultipleFormats { get; }

    /// <summary>
    /// Gets whether the platform supports image clipboard operations
    /// </summary>
    bool SupportsImages { get; }
}


