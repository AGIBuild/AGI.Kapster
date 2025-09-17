using System;
using Avalonia;
using Serilog;

namespace AGI.Captor.Desktop.Services.ElementDetection;

/// <summary>
/// Null implementation of IElementDetector for platforms that don't support element detection
/// </summary>
public class NullElementDetector : IElementDetector
{
    public bool IsDetectionActive { get; set; }
    public bool IsWindowMode => false;
    
    public event Action<bool>? DetectionModeChanged
    {
        add { /* No-op */ }
        remove { /* No-op */ }
    }
    
    public DetectedElement? DetectElementAt(int x, int y, IntPtr ignoreWindow = default)
    {
        Log.Debug("Element detection not supported on this platform");
        return null;
    }
    
    public void ToggleDetectionMode()
    {
        // No-op
    }

    public bool IsSupported => false;
    public bool HasPermissions => false;

    public void Dispose()
    {
        // No-op
    }
}
