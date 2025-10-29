using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Services.Annotation;
using AGI.Kapster.Desktop.Commands;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Serilog;
using System;

namespace AGI.Kapster.Desktop.Overlays.Handlers;

/// <summary>
/// Handles text editing for annotations
/// </summary>
public class AnnotationEditingHandler
{
    private readonly NewAnnotationOverlay _overlay;
    private readonly IAnnotationService _annotationService;
    private readonly CommandManager _commandManager;

    // Text editing state
    private TextBox? _editingTextBox;
    private TextAnnotation? _editingTextItem;
    private bool _isEditing;

    public AnnotationEditingHandler(
        NewAnnotationOverlay overlay,
        IAnnotationService annotationService,
        CommandManager commandManager)
    {
        _overlay = overlay;
        _annotationService = annotationService;
        _commandManager = commandManager;
    }

    /// <summary>
    /// Check if currently editing text
    /// </summary>
    public bool IsEditing => _isEditing;

    /// <summary>
    /// Start editing text annotation
    /// </summary>
    public void StartTextEditing(TextAnnotation textItem)
    {
        try
        {
            if (_isEditing)
            {
                EndTextEditing();
            }

            _editingTextItem = textItem;
            _isEditing = true;

            // Create text box for editing
            _editingTextBox = new TextBox
            {
                Text = textItem.Text,
                FontSize = textItem.Style.FontSize,
                Foreground = new SolidColorBrush(textItem.Style.StrokeColor),
                Background = Avalonia.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4),
                AcceptsReturn = true,
                AcceptsTab = true,
                IsReadOnly = false
            };

            // Position the text box
            Canvas.SetLeft(_editingTextBox, textItem.Position.X);
            Canvas.SetTop(_editingTextBox, textItem.Position.Y);
            _editingTextBox.Width = Math.Max(100, textItem.Bounds.Width);
            _editingTextBox.Height = Math.Max(20, textItem.Bounds.Height);

            // Add to overlay
            _overlay.Children.Add(_editingTextBox);

            // Focus and select all text
            _editingTextBox.Focus();
            _editingTextBox.SelectAll();

            // Subscribe to events
            _editingTextBox.TextChanged += OnTextEditingTextChanged;
            _editingTextBox.LostFocus += OnTextEditingLostFocus;
            _editingTextBox.KeyDown += OnTextEditingKeyDown;

            Log.Debug("Started editing text annotation: {Text}", textItem.Text);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error starting text editing for item {ItemId}", textItem.Id);
        }
    }

    /// <summary>
    /// End text editing
    /// </summary>
    public void EndTextEditing()
    {
        try
        {
            if (!_isEditing || _editingTextBox == null || _editingTextItem == null) return;

            // Get the final text
            var newText = _editingTextBox.Text?.Trim() ?? string.Empty;

            // Update the text item
            _editingTextItem.Text = newText;

            // If text is not empty, add it to the annotation manager
            if (!string.IsNullOrEmpty(newText))
            {
                // Add the text annotation to the manager
                _annotationService.Manager.AddItem(_editingTextItem);
                Log.Debug("Text annotation added to manager: {Text}", newText);
                
                // Trigger re-render to show the text annotation
                _overlay.RefreshRender();
            }
            else
            {
                Log.Debug("Empty text annotation discarded");
            }

            // Clean up
            CleanupTextEditing();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error ending text editing");
            CleanupTextEditing();
        }
    }

    /// <summary>
    /// Handle key down events during text editing
    /// </summary>
    public void HandleKeyDown(KeyEventArgs e)
    {
        if (!_isEditing || _editingTextBox == null) return;

        try
        {
            switch (e.Key)
            {
                case Key.Enter:
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    {
                        // Shift+Enter: new line
                        return;
                    }
                    else
                    {
                        // Enter: finish editing
                        EndTextEditing();
                        e.Handled = true;
                    }
                    break;

                case Key.Escape:
                    // Escape: cancel editing
                    CancelTextEditing();
                    e.Handled = true;
                    break;

                case Key.Tab:
                    // Tab: finish editing
                    EndTextEditing();
                    e.Handled = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling key down during text editing: {Key}", e.Key);
        }
    }

    /// <summary>
    /// Handle pointer pressed events during text editing
    /// </summary>
    public void HandlePointerPressed(PointerPressedEventArgs e)
    {
        if (!_isEditing) return;

        try
        {
            // Check if right mouse button was pressed
            if (e.GetCurrentPoint(_overlay).Properties.IsRightButtonPressed)
            {
                // Right click: finish editing
                EndTextEditing();
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling pointer pressed during text editing");
        }
    }

    /// <summary>
    /// Cancel text editing without saving changes
    /// </summary>
    private void CancelTextEditing()
    {
        try
        {
            if (!_isEditing || _editingTextBox == null || _editingTextItem == null) return;

            Log.Debug("Text editing cancelled");

            // Clean up without saving changes
            CleanupTextEditing();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error cancelling text editing");
            CleanupTextEditing();
        }
    }

    /// <summary>
    /// Clean up text editing resources
    /// </summary>
    private void CleanupTextEditing()
    {
        try
        {
            if (_editingTextBox != null)
            {
                // Unsubscribe from events
                _editingTextBox.TextChanged -= OnTextEditingTextChanged;
                _editingTextBox.LostFocus -= OnTextEditingLostFocus;
                _editingTextBox.KeyDown -= OnTextEditingKeyDown;

                // Remove from overlay
                _overlay.Children.Remove(_editingTextBox);
                _editingTextBox = null;
            }

            _editingTextItem = null;
            _isEditing = false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error cleaning up text editing resources");
        }
    }

    /// <summary>
    /// Text editing box content change event - implement auto-expansion
    /// </summary>
    private void OnTextEditingTextChanged(object? sender, TextChangedEventArgs e)
    {
        try
        {
            if (_editingTextBox == null || _editingTextItem == null) return;

            // Auto-expand text box based on content
            var text = _editingTextBox.Text ?? string.Empty;
            var formattedText = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(_editingTextItem.Style.FontFamily, _editingTextItem.Style.FontStyle, _editingTextItem.Style.FontWeight),
                _editingTextItem.Style.FontSize,
                new SolidColorBrush(_editingTextItem.Style.StrokeColor));

            // Calculate required size
            var requiredWidth = Math.Max(100, formattedText.Width + 8); // Add padding
            var requiredHeight = Math.Max(20, formattedText.Height + 8);

            // Update text box size
            _editingTextBox.Width = requiredWidth;
            _editingTextBox.Height = requiredHeight;

            // Update text item text content for real-time display
            _editingTextItem.Text = text;

            // Update text item bounds
            // TextAnnotation doesn't have Position/Width/Height properties, just update the text box
            // The actual bounds will be updated when the text editing is completed

            // Refresh the overlay to show updated text
            _overlay.RefreshRender();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnTextEditingTextChanged");
        }
    }

    /// <summary>
    /// Text editing box lost focus event
    /// </summary>
    private void OnTextEditingLostFocus(object? sender, EventArgs e)
    {
        try
        {
            EndTextEditing();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnTextEditingLostFocus");
        }
    }

    /// <summary>
    /// Text editing box key press event
    /// </summary>
    private void OnTextEditingKeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            HandleKeyDown(e);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnTextEditingKeyDown");
        }
    }
}
