using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Platform;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Overlays.Layers;
using AGI.Kapster.Desktop.Services.Annotation;
using AGI.Kapster.Desktop.Services.ElementDetection;

namespace AGI.Kapster.Desktop.Overlays.Events;

/// <summary>
/// Base interface for all overlay events
/// </summary>
public interface IOverlayEvent
{
}

/// <summary>
/// Event raised when overlay mode changes
/// </summary>
public record ModeChangedEvent(OverlayMode OldMode, OverlayMode NewMode) : IOverlayEvent;

/// <summary>
/// Event raised when selection rect changes
/// </summary>
public record SelectionChangedEvent(Rect Selection) : IOverlayEvent;

/// <summary>
/// Event raised when selection is confirmed
/// </summary>
public record SelectionConfirmedEvent(Rect Selection, DetectedElement? Element) : IOverlayEvent;

/// <summary>
/// Event raised when element is highlighted
/// </summary>
public record ElementHighlightedEvent(DetectedElement? Element, Rect HighlightRect) : IOverlayEvent;

/// <summary>
/// Event raised when mask cutout changes
/// </summary>
public record CutoutChangedEvent(Rect OldCutout, Rect NewCutout) : IOverlayEvent;

/// <summary>
/// Event raised when a layer is activated
/// </summary>
public record LayerActivatedEvent(string LayerId) : IOverlayEvent;

/// <summary>
/// Event raised when a layer is deactivated
/// </summary>
public record LayerDeactivatedEvent(string LayerId) : IOverlayEvent;

/// <summary>
/// Event raised when selection is finished (user finished dragging selection)
/// </summary>
public record SelectionFinishedEvent(Rect Selection, bool IsEditableSelection) : IOverlayEvent;

/// <summary>
/// Event raised when annotation tool changes
/// </summary>
public record ToolChangedEvent(AnnotationToolType OldTool, AnnotationToolType NewTool) : IOverlayEvent;

/// <summary>
/// Event raised when annotation style changes
/// </summary>
public record StyleChangedEvent(IAnnotationStyle Style) : IOverlayEvent;

/// <summary>
/// Request to change annotation tool (e.g., hotkeys)
/// </summary>
public record ToolChangeRequestedEvent(AnnotationToolType Tool) : IOverlayEvent;

/// <summary>
/// Request to undo last action
/// </summary>
public record UndoRequestedEvent() : IOverlayEvent;

/// <summary>
/// Request to redo last undone action
/// </summary>
public record RedoRequestedEvent() : IOverlayEvent;

/// <summary>
/// Request to delete current selection
/// </summary>
public record DeleteRequestedEvent() : IOverlayEvent;

/// <summary>
/// Request to clear all annotations
/// </summary>
public record ClearAnnotationsRequestedEvent() : IOverlayEvent;

/// <summary>
/// Request to select all annotations
/// </summary>
public record SelectAllRequestedEvent() : IOverlayEvent;

/// <summary>
/// Request to nudge selected annotations by delta vector
/// </summary>
public record NudgeRequestedEvent(Vector Delta) : IOverlayEvent;

/// <summary>
/// Request to copy selected annotations to clipboard
/// </summary>
public record CopyRequestedEvent() : IOverlayEvent;

/// <summary>
/// Request to paste annotations from clipboard
/// </summary>
public record PasteRequestedEvent() : IOverlayEvent;

/// <summary>
/// Request to duplicate selected annotations (copy + offset)
/// </summary>
public record DuplicateRequestedEvent() : IOverlayEvent;

/// <summary>
/// Event raised when an annotation is created
/// </summary>
public record AnnotationCreatedEvent(IAnnotationItem Annotation) : IOverlayEvent;

/// <summary>
/// Event raised when an annotation is modified (move/resize/edit)
/// </summary>
public record AnnotationModifiedEvent(IAnnotationItem Annotation) : IOverlayEvent;

/// <summary>
/// Event raised when an annotation is deleted
/// </summary>
public record AnnotationDeletedEvent(Guid AnnotationId) : IOverlayEvent;

/// <summary>
/// Event raised when export is requested (Ctrl+S or button click)
/// </summary>
public record ExportRequestedEvent(Rect Region) : IOverlayEvent;

/// <summary>
/// Event raised when color picker is requested (C key or button click)
/// </summary>
public record ColorPickerRequestedEvent() : IOverlayEvent;

/// <summary>
/// Event raised when confirm is requested (Enter/Double-click)
/// </summary>
public record ConfirmRequestedEvent(Rect Region) : IOverlayEvent;

/// <summary>
/// Event raised when overlay context (size/position/screens) changes
/// </summary>
public record OverlayContextChangedEvent(Size OverlaySize, PixelPoint OverlayPosition, IReadOnlyList<Screen> Screens) : IOverlayEvent;

/// <summary>
/// Event raised when cancel is requested (e.g., ESC key)
/// </summary>
public record CancelRequestedEvent(string Reason) : IOverlayEvent;

/// <summary>
/// Event for requesting IME state change (enable/disable for text editing)
/// </summary>
public record ImeChangeRequestedEvent(bool Enabled) : IOverlayEvent;

