using System.IO;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace AGI.Captor.Desktop.Services.Overlay;

/// <summary>
/// Helper class to convert between different bitmap formats
/// </summary>
public static class BitmapConverter
{
    /// <summary>
    /// Converts SKBitmap to Avalonia Bitmap
    /// </summary>
    public static Bitmap? ConvertToAvaloniaBitmap(SKBitmap? skBitmap)
    {
        if (skBitmap == null)
            return null;
        
        using var data = skBitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Position = 0;
        return new Bitmap(stream);
    }
    
    /// <summary>
    /// Converts Avalonia Bitmap to SKBitmap
    /// </summary>
    public static SKBitmap? ConvertToSKBitmap(Bitmap? bitmap)
    {
        if (bitmap == null)
            return null;
        
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        stream.Position = 0;
        return SKBitmap.Decode(stream);
    }
}


