using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Serilog;

namespace AGI.Kapster.Desktop.Overlays;

/// <summary>
/// Overlay that displays selection size and pixel color information
/// </summary>
public sealed class SelectionInfoOverlay : Canvas
{
    private readonly Border _infoBorder;
    private readonly StackPanel _infoPanel;
    private readonly TextBlock _sizeText;
    private readonly TextBlock _colorText;
    private readonly Border _colorPreview;
    private bool _isVisible;

    public SelectionInfoOverlay()
    {
        Background = Brushes.Transparent;
        IsHitTestVisible = false;

        // Create info panel
        _infoPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4
        };

        // Size text
        _sizeText = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Text = "0 × 0"
        };

        // Color text (temporarily hidden)
        _colorText = new TextBlock
        {
            FontSize = 10,
            Foreground = Brushes.White,
            Text = "RGB(0, 0, 0)",
            IsVisible = false // Hide color text
        };

        // Color preview (temporarily hidden)
        _colorPreview = new Border
        {
            Width = 16,
            Height = 16,
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            Background = Brushes.Black,
            IsVisible = false // Hide color preview
        };

        // Add color info to horizontal panel (temporarily hidden)
        var colorPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            IsVisible = false // Hide entire color panel
        };
        colorPanel.Children.Add(_colorPreview);
        colorPanel.Children.Add(_colorText);

        // Add to main panel
        _infoPanel.Children.Add(_sizeText);
        _infoPanel.Children.Add(colorPanel);

        // Create border container
        _infoBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), // Semi-transparent black
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 4),
            Child = _infoPanel
        };

        Children.Add(_infoBorder);

        // Initially hidden
        IsVisible = false;
    }

    /// <summary>
    /// Update the selection info display
    /// </summary>
    /// <param name="selectionRect">Current selection rectangle</param>
    /// <param name="mousePosition">Current mouse position</param>
    public void UpdateInfo(Rect selectionRect, Point mousePosition)
    {
        if (!_isVisible) return;

        // Update size information (only if there's a valid selection)
        if (selectionRect.Width > 0 && selectionRect.Height > 0)
        {
            var width = Math.Round(selectionRect.Width);
            var height = Math.Round(selectionRect.Height);
            _sizeText.Text = $"{width} × {height}";
        }
        else
        {
            _sizeText.Text = "0 × 0";
        }

        // Color information temporarily hidden - no updates needed

        // Calculate optimal position for info panel
        var infoPosition = CalculateOptimalPosition(selectionRect);

        Canvas.SetLeft(_infoBorder, infoPosition.X);
        Canvas.SetTop(_infoBorder, infoPosition.Y);
    }

    /// <summary>
    /// Show the info overlay
    /// </summary>
    public void Show()
    {
        _isVisible = true;
        IsVisible = true;
    }

    /// <summary>
    /// Hide the info overlay
    /// </summary>
    public void Hide()
    {
        _isVisible = false;
        IsVisible = false;
    }


    /// <summary>
    /// Calculate optimal position for info panel based on available space
    /// </summary>
    private Point CalculateOptimalPosition(Rect selectionRect)
    {
        const double offset = 8; // 8px offset from selection edge
        var infoSize = _infoBorder.DesiredSize;

        // If info size is not available yet, use default position
        if (infoSize.Width <= 0 || infoSize.Height <= 0)
        {
            // If no selection, position near mouse cursor
            if (selectionRect.Width <= 0 || selectionRect.Height <= 0)
            {
                return new Point(offset, offset);
            }
            return new Point(selectionRect.Right + offset, selectionRect.Top - offset);
        }

        var parent = this.GetVisualParent() as Control;
        if (parent == null)
        {
            // If no selection, position near top-left corner
            if (selectionRect.Width <= 0 || selectionRect.Height <= 0)
            {
                return new Point(offset, offset);
            }
            return new Point(selectionRect.Right + offset, selectionRect.Top - offset);
        }

        var parentBounds = new Rect(0, 0, parent.Bounds.Width, parent.Bounds.Height);

        // If no selection, position near top-left corner
        if (selectionRect.Width <= 0 || selectionRect.Height <= 0)
        {
            return new Point(offset, offset);
        }

        // Try different positions in order of preference
        var positions = new[]
        {
            // 1. Top-right (preferred)
            new Point(selectionRect.Right + offset, selectionRect.Top - offset),
            
            // 2. Top-left
            new Point(selectionRect.Left - infoSize.Width - offset, selectionRect.Top - offset),
            
            // 3. Bottom-right
            new Point(selectionRect.Right + offset, selectionRect.Bottom + offset),
            
            // 4. Bottom-left
            new Point(selectionRect.Left - infoSize.Width - offset, selectionRect.Bottom + offset),
            
            // 5. Right side (center vertically)
            new Point(selectionRect.Right + offset, selectionRect.Center.Y - infoSize.Height / 2),
            
            // 6. Left side (center vertically)
            new Point(selectionRect.Left - infoSize.Width - offset, selectionRect.Center.Y - infoSize.Height / 2),
            
            // 7. Top side (center horizontally)
            new Point(selectionRect.Center.X - infoSize.Width / 2, selectionRect.Top - infoSize.Height - offset),
            
            // 8. Bottom side (center horizontally)
            new Point(selectionRect.Center.X - infoSize.Width / 2, selectionRect.Bottom + offset),
            
            // 9. Inside selection (top-right corner)
            new Point(selectionRect.Right - infoSize.Width - offset, selectionRect.Top + offset)
        };

        // Find the first position that fits within bounds
        foreach (var position in positions)
        {
            var infoBounds = new Rect(position, infoSize);

            // Check if the info panel fits within parent bounds
            if (infoBounds.Left >= parentBounds.Left &&
                infoBounds.Right <= parentBounds.Right &&
                infoBounds.Top >= parentBounds.Top &&
                infoBounds.Bottom <= parentBounds.Bottom)
            {
                return position;
            }
        }

        // If no position fits, use the last fallback (inside selection)
        return positions[positions.Length - 1];
    }

}
