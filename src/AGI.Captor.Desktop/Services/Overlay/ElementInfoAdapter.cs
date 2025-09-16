using System;
using Avalonia;

namespace AGI.Captor.Desktop.Services.Overlay;

/// <summary>
/// Adapter to convert DetectedElement to IElementInfo
/// </summary>
public class ElementInfoAdapter : IElementInfo
{
    private readonly DetectedElement _detectedElement;
    
    public ElementInfoAdapter(DetectedElement detectedElement)
    {
        _detectedElement = detectedElement;
    }
    
    public PixelRect Bounds => new PixelRect(
        (int)_detectedElement.Bounds.X,
        (int)_detectedElement.Bounds.Y,
        (int)_detectedElement.Bounds.Width,
        (int)_detectedElement.Bounds.Height);
    
    public string ElementType => _detectedElement.ClassName;
    
    public string? Name => _detectedElement.Name;
    
    public IntPtr WindowHandle => _detectedElement.WindowHandle;
    
    public object? PlatformData => _detectedElement;
    
    /// <summary>
    /// Converts a DetectedElement to IElementInfo
    /// </summary>
    public static IElementInfo FromDetectedElement(DetectedElement detectedElement)
    {
        return new ElementInfoAdapter(detectedElement);
    }
}
