namespace AGI.Kapster.Desktop.Overlays;

/// <summary>
/// Represents the selection mode of the overlay window
/// </summary>
internal enum OverlaySelectionMode
{
    /// <summary>
    /// Free selection mode using crosshair cursor (default)
    /// </summary>
    FreeSelection,

    /// <summary>
    /// Element picker mode for detecting UI elements
    /// </summary>
    ElementPicker,

    /// <summary>
    /// Editing mode with an existing editable selection
    /// </summary>
    Editing
}

