using System;
using Avalonia;
using Avalonia.Media.Imaging;
using AGI.Captor.Desktop.Services;

namespace AGI.Captor.Desktop.Overlays;

/// <summary>
/// Event arguments for region selection in overlay windows
/// </summary>
public class RegionSelectedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the selected region
    /// </summary>
    public Rect Region { get; }
    
    /// <summary>
    /// Gets whether this is a full screen capture
    /// </summary>
    public bool IsFullScreen { get; }
    
    /// <summary>
    /// Gets the detected element information if available
    /// </summary>
    public DetectedElement? DetectedElement { get; }
    
    /// <summary>
    /// Gets whether this is an editable selection (for annotations)
    /// </summary>
    public bool IsEditableSelection { get; }
    
    /// <summary>
    /// Gets the composite image with annotations (for platforms that need manual composition like macOS)
    /// </summary>
    public Bitmap? CompositeImage { get; }
    
    public RegionSelectedEventArgs(Rect region, bool isFullScreen = false, DetectedElement? detectedElement = null, 
        bool isEditableSelection = false, Bitmap? compositeImage = null)
    {
        Region = region;
        IsFullScreen = isFullScreen;
        DetectedElement = detectedElement;
        IsEditableSelection = isEditableSelection;
        CompositeImage = compositeImage;
    }
}

/// <summary>
/// Event arguments for overlay window cancellation
/// </summary>
public class OverlayCancelledEventArgs : EventArgs
{
    /// <summary>
    /// Gets the reason for cancellation
    /// </summary>
    public string? Reason { get; }
    
    public OverlayCancelledEventArgs(string? reason = null)
    {
        Reason = reason;
    }
}
