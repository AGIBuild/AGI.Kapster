using System.ComponentModel;

namespace AGI.Captor.Desktop.Models;

/// <summary>
/// Export format enumeration
/// </summary>
public enum ExportFormat
{
    [Description("PNG - Portable Network Graphics")]
    PNG,
    
    [Description("JPEG - Joint Photographic Experts Group")]
    JPEG,
    
    [Description("BMP - Windows Bitmap")]
    BMP,
    
    [Description("TIFF - Tagged Image File Format")]
    TIFF,
    
    [Description("WebP - Web Picture Format")]
    WebP,
    
    [Description("GIF - Graphics Interchange Format")]
    GIF
}

/// <summary>
/// Export quality settings
/// </summary>
public class ExportSettings
{
    /// <summary>
    /// Export format
    /// </summary>
    public ExportFormat Format { get; set; } = ExportFormat.PNG;
    
    /// <summary>
    /// Quality level (0-100, applies to JPEG, WebP)
    /// </summary>
    public int Quality { get; set; } = 90;
    
    /// <summary>
    /// Compression level (0-9, applies to PNG, TIFF)
    /// </summary>
    public int Compression { get; set; } = 6;
    
    /// <summary>
    /// DPI setting for the exported image
    /// </summary>
    public int DPI { get; set; } = 96;
    
    /// <summary>
    /// Whether to preserve transparency (PNG, WebP, TIFF)
    /// </summary>
    public bool PreserveTransparency { get; set; } = true;
    
    
    /// <summary>
    /// Get file extension for the current format
    /// </summary>
    public string GetFileExtension()
    {
        return Format switch
        {
            ExportFormat.PNG => ".png",
            ExportFormat.JPEG => ".jpg",
            ExportFormat.BMP => ".bmp",
            ExportFormat.TIFF => ".tiff",
            ExportFormat.WebP => ".webp",
            ExportFormat.GIF => ".gif",
            _ => ".png"
        };
    }
    
    /// <summary>
    /// Get MIME type for the current format
    /// </summary>
    public string GetMimeType()
    {
        return Format switch
        {
            ExportFormat.PNG => "image/png",
            ExportFormat.JPEG => "image/jpeg",
            ExportFormat.BMP => "image/bmp",
            ExportFormat.TIFF => "image/tiff",
            ExportFormat.WebP => "image/webp",
            ExportFormat.GIF => "image/gif",
            _ => "image/png"
        };
    }
    
    /// <summary>
    /// Check if the format supports quality setting
    /// </summary>
    public bool SupportsQuality()
    {
        return Format == ExportFormat.JPEG || Format == ExportFormat.WebP;
    }
    
    /// <summary>
    /// Check if the format supports compression setting
    /// </summary>
    public bool SupportsCompression()
    {
        return Format == ExportFormat.PNG || Format == ExportFormat.TIFF;
    }
    
    /// <summary>
    /// Check if the format supports transparency
    /// </summary>
    public bool SupportsTransparency()
    {
        return Format == ExportFormat.PNG || Format == ExportFormat.WebP || 
               Format == ExportFormat.TIFF || Format == ExportFormat.GIF;
    }
}
