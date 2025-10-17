using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace AGI.Kapster.Desktop.Services.Export.Imaging;

/// <summary>
/// Helper class to convert between different bitmap formats
/// </summary>
public static class BitmapConverter
{
    /// <summary>
    /// Fast conversion from SKBitmap to Avalonia Bitmap using direct pixel copy (no encoding/decoding)
    /// </summary>
    public static Bitmap? ConvertToAvaloniaBitmapFast(SKBitmap? skBitmap)
    {
        if (skBitmap == null)
            return null;

        try
        {
            // Ensure source is in BGRA8888 format (Avalonia's native format)
            SKBitmap source = skBitmap;
            SKBitmap? convertedTemp = null;
            
            if (skBitmap.ColorType != SKColorType.Bgra8888 || skBitmap.AlphaType != SKAlphaType.Premul)
            {
                convertedTemp = new SKBitmap();
                if (!skBitmap.CopyTo(convertedTemp, SKColorType.Bgra8888))
                {
                    convertedTemp?.Dispose();
                    // Fallback to slow path
                    return ConvertToAvaloniaBitmap(skBitmap);
                }
                source = convertedTemp;
            }

            try
            {
                int width = source.Width;
                int height = source.Height;
                int srcStride = source.RowBytes;
                
                // Create WriteableBitmap with matching format
                var wb = new WriteableBitmap(
                    new PixelSize(width, height), 
                    new Vector(96, 96), 
                    PixelFormat.Bgra8888, 
                    AlphaFormat.Premul);
                
                using (var fb = wb.Lock())
                {
                    int dstStride = fb.RowBytes;
                    int bytesToCopyPerRow = System.Math.Min(srcStride, dstStride);

                    var srcBasePtr = source.GetPixels();
                    var dstBasePtr = fb.Address;
                    
                    // Row-by-row copy to handle stride differences
                    var rowBuffer = new byte[bytesToCopyPerRow];
                    for (int y = 0; y < height; y++)
                    {
                        var srcRowPtr = srcBasePtr + y * srcStride;
                        var dstRowPtr = dstBasePtr + y * dstStride;
                        Marshal.Copy(srcRowPtr, rowBuffer, 0, bytesToCopyPerRow);
                        Marshal.Copy(rowBuffer, 0, dstRowPtr, bytesToCopyPerRow);
                    }
                }
                
                return wb;
            }
            finally
            {
                convertedTemp?.Dispose();
            }
        }
        catch
        {
            // If fast path fails, fall back to slow encoding path
            return ConvertToAvaloniaBitmap(skBitmap);
        }
    }

    /// <summary>
    /// Converts SKBitmap to Avalonia Bitmap (slower encoding path, fallback)
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


