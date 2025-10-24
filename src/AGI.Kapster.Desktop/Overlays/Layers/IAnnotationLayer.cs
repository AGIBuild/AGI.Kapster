using System;
using System.Collections.Generic;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Services.Annotation;
using Avalonia;

namespace AGI.Kapster.Desktop.Overlays.Layers;

/// <summary>
/// Annotation layer interface for managing annotation drawing and editing
/// </summary>
public interface IAnnotationLayer : IOverlayLayer
{
    /// <summary>
    /// Set the selection rectangle (working area for annotations)
    /// </summary>
    void SetSelectionRect(Rect rect);
    
    /// <summary>
    /// Get all annotation items
    /// </summary>
    IEnumerable<IAnnotationItem> GetAnnotations();
    
    /// <summary>
    /// Clear all annotations
    /// </summary>
    void ClearAnnotations();
    
    /// <summary>
    /// Delete currently selected annotations (if any)
    /// </summary>
    /// <returns>True if anything was deleted</returns>
    bool DeleteSelected();
    
    /// <summary>
    /// Set current annotation tool
    /// </summary>
    void SetTool(AnnotationToolType tool);
    
    /// <summary>
    /// Set current annotation style
    /// </summary>
    void SetStyle(IAnnotationStyle style);
    
    /// <summary>
    /// End text editing (if currently editing text annotation)
    /// </summary>
    void EndTextEditing();
    
    /// <summary>
    /// Undo last annotation action
    /// </summary>
    /// <returns>True if undo was successful</returns>
    bool Undo();
    
    /// <summary>
    /// Redo last undone annotation action
    /// </summary>
    /// <returns>True if redo was successful</returns>
    bool Redo();

    /// <summary>
    /// Select all annotations (for editor operations)
    /// </summary>
    void SelectAll();
    
    /// <summary>
    /// Nudge/move selected annotations by delta
    /// </summary>
    void NudgeSelected(Vector delta);
    
    /// <summary>
    /// Copy selected annotations to clipboard (future)
    /// </summary>
    void CopySelected();
    
    /// <summary>
    /// Paste annotations from clipboard (future)
    /// </summary>
    void Paste();
    
    /// <summary>
    /// Duplicate selected annotations (future)
    /// </summary>
    void DuplicateSelected();
}

