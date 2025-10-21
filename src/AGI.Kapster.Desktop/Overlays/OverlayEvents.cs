using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Services.ElementDetection;
using Avalonia;
using Avalonia.Media.Imaging;
using System;

namespace AGI.Kapster.Desktop.Overlays;

/// <summary>
/// Event arguments for region selection events
/// </summary>
public class RegionSelectedEventArgs : EventArgs
{
    public Rect SelectedRegion { get; }
    public bool IsConfirmed { get; }
    public DetectedElement? DetectedElement { get; }
    public bool IsEditableSelection { get; }
    public Bitmap? FinalImage { get; }

    public RegionSelectedEventArgs(
        Rect selectedRegion,
        bool isConfirmed,
        DetectedElement? detectedElement = null,
        bool isEditableSelection = false,
        Bitmap? finalImage = null)
    {
        SelectedRegion = selectedRegion;
        IsConfirmed = isConfirmed;
        DetectedElement = detectedElement;
        IsEditableSelection = isEditableSelection;
        FinalImage = finalImage;
    }
}

/// <summary>
/// Event arguments for overlay cancellation events
/// </summary>
public class OverlayCancelledEventArgs : EventArgs
{
    public string Reason { get; }

    public OverlayCancelledEventArgs(string reason)
    {
        Reason = reason;
    }
}

