using AGI.Captor.Desktop.Models;
using AGI.Captor.Desktop.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;
using Serilog;
using System;
using System.Collections.Generic;

namespace AGI.Captor.Desktop.Overlays;

public partial class NewAnnotationToolbar : UserControl
{
    public NewAnnotationOverlay? Target { get; set; }
    
    private readonly List<ToggleButton> _toolButtons = new();
    private Border? _currentColorDisplay;
    private Button? _colorPickerButton;
    private Color _currentColor = Colors.Red;
    private Window? _colorPickerWindow;
    public NewAnnotationToolbar()
    {
        InitializeComponent();
        SetupEventHandlers();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void SetupEventHandlers()
    {
        SetupToolButtons();
        SetupColorPicker();
        SetupEmojiPicker();
        SetupActionButtons();
        SetupSliders();
    }

    private void SetupToolButtons()
    {
        // Collect all tool buttons
        _toolButtons.Add(this.FindControl<ToggleButton>("SelectToolButton")!);
        _toolButtons.Add(this.FindControl<ToggleButton>("ArrowToolButton")!);
        _toolButtons.Add(this.FindControl<ToggleButton>("RectangleToolButton")!);
        _toolButtons.Add(this.FindControl<ToggleButton>("EllipseToolButton")!);
        _toolButtons.Add(this.FindControl<ToggleButton>("TextToolButton")!);
        _toolButtons.Add(this.FindControl<ToggleButton>("FreehandToolButton")!);

        // Setup event handlers for tool buttons
        foreach (var button in _toolButtons)
        {
            if (button != null)
            {
                button.Click += OnToolButtonClick;
            }
        }
    }

    private void OnToolButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not ToggleButton clickedButton || Target == null) return;

        // Uncheck all other tool buttons
        foreach (var button in _toolButtons)
        {
            if (button != clickedButton)
            {
                button.IsChecked = false;
                button.Background = Brushes.Transparent;
            }
        }
        
        // Also reset emoji button background when other tools are selected
        if (this.FindControl<Button>("CurrentEmojiDisplay") is { } emojiButton)
        {
            emojiButton.Background = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255)); // Default background
        }

        // Check the clicked button and set background
        clickedButton.IsChecked = true;
        clickedButton.Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)); // Semi-transparent white

        // Set tool based on tag
        var toolType = clickedButton.Tag?.ToString() switch
        {
            "None" => AnnotationToolType.None,
            "Arrow" => AnnotationToolType.Arrow,
            "Rectangle" => AnnotationToolType.Rectangle,
            "Ellipse" => AnnotationToolType.Ellipse,
            "Text" => AnnotationToolType.Text,
            "Freehand" => AnnotationToolType.Freehand,
            "Emoji" => AnnotationToolType.Emoji,
            _ => AnnotationToolType.None
        };

        Target.CurrentTool = toolType;
        
        // Ensure focus returns to overlay for immediate keyboard shortcuts
        Target.Focus();

        // Show/hide panels based on tool type
        if (this.FindControl<StackPanel>("FontSizePanel") is { } fontPanel)
        {
            fontPanel.IsVisible = toolType == AnnotationToolType.Text;
        }
        
        // Emoji panel is now always visible (simplified)
        
        // Trigger layout update and notify parent to reposition toolbar
        InvalidateArrange();
        InvalidateMeasure();
        
        // Use dispatcher to ensure layout is updated before repositioning
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Find parent overlay window and trigger toolbar repositioning
            if (this.FindAncestorOfType<OverlayWindow>() is { } overlayWindow && Target != null)
            {
                // Use the correct Target annotator instance instead of FindControl
                overlayWindow.GetType()
                    .GetMethod("UpdateToolbarPosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                    .Invoke(overlayWindow, new object[] { Target.SelectionRect });
            }
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void SetupColorPicker()
    {
        _currentColorDisplay = this.FindControl<Border>("CurrentColorDisplay");
        _colorPickerButton = this.FindControl<Button>("ColorPickerButton");
        
        if (_colorPickerButton != null)
        {
            _colorPickerButton.Click += OnColorPickerButtonClick;
        }
        
        // Initialize current color display
        UpdateCurrentColorDisplay();
    }

    private void OnColorPickerButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ShowColorPicker();
    }

    private void ShowColorPicker()
    {
        if (_colorPickerWindow != null)
        {
            _colorPickerWindow.Close();
            _colorPickerWindow = null;
            return;
        }

        _colorPickerWindow = CreateColorPickerWindow();
        
        // Position to the right of the color picker button
        if (this.GetVisualRoot() is Window parentWindow && _colorPickerButton != null)
        {
            var buttonBounds = _colorPickerButton.Bounds;
            var buttonScreenPos = _colorPickerButton.PointToScreen(new Point(buttonBounds.Right + 5, 0));
            
            _colorPickerWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            _colorPickerWindow.Position = new PixelPoint((int)buttonScreenPos.X, (int)buttonScreenPos.Y);
        }
        
        _colorPickerWindow.Show();
    }

    private Window CreateColorPickerWindow()
    {
        var window = new Window
        {
            Width = 160,
            Height = 120,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            SystemDecorations = SystemDecorations.None,
            Background = new SolidColorBrush(Color.FromArgb(240, 0, 0, 0)),
            Topmost = true
        };

        // Create color grid
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        
        for (int i = 0; i < 5; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        // Common colors array
        var colors = new[]
        {
            Colors.Red, Colors.Orange, Colors.Yellow, Colors.Green, Colors.Blue,
            Colors.Purple, Colors.Pink, Colors.Cyan, Colors.Magenta, Colors.Brown,
            Colors.Black, Colors.DarkGray, Colors.Gray, Colors.LightGray, Colors.White
        };

        // Create color buttons
        for (int i = 0; i < colors.Length && i < 15; i++)
        {
            var color = colors[i];
            var button = new Border
            {
                Background = new SolidColorBrush(color),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(2),
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            button.PointerPressed += (s, e) =>
            {
                SelectColor(color);
                _colorPickerWindow?.Close();
                _colorPickerWindow = null;
            };

            Grid.SetRow(button, i / 5);
            Grid.SetColumn(button, i % 5);
            grid.Children.Add(button);
        }

        var border = new Border
        {
            Child = grid,
            BorderBrush = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4)
        };

        window.Content = border;
        
        // Handle window focus lost
        window.Deactivated += (s, e) =>
        {
            window.Close();
            _colorPickerWindow = null;
        };

        return window;
    }

    private void SelectColor(Color color)
    {
        _currentColor = color;
        UpdateCurrentColorDisplay();
        Target?.SetStrokeColor(color);
    }

    private void UpdateCurrentColorDisplay()
    {
        if (_currentColorDisplay != null)
        {
            _currentColorDisplay.Background = new SolidColorBrush(_currentColor);
        }
    }

    private void SetupEmojiPicker()
    {
        var emojiPickerButton = this.FindControl<Button>("EmojiPickerButton");
        var currentEmojiDisplay = this.FindControl<Button>("CurrentEmojiDisplay");
        
        if (emojiPickerButton != null)
        {
            emojiPickerButton.Click += OnEmojiPickerButtonClick;
        }
        
        if (currentEmojiDisplay != null)
        {
            currentEmojiDisplay.Click += OnCurrentEmojiDisplayClick;
        }
    }

    private void OnEmojiPickerButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ShowEmojiPicker();
    }

    private void OnCurrentEmojiDisplayClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Switch to emoji tool mode
        if (Target != null)
        {
            Target.CurrentTool = AnnotationToolType.Emoji;
            
            // Update visual state - uncheck all tool buttons
            foreach (var button in _toolButtons)
            {
                button.IsChecked = false;
                button.Background = Brushes.Transparent;
            }
            
            // Highlight the emoji display button
            if (sender is Button emojiButton)
            {
                emojiButton.Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)); // Semi-transparent white
            }
            
            Log.Debug("Switched to emoji tool via CurrentEmojiDisplay click");
        }
    }

    private void ShowEmojiPicker()
    {
        var emojiPickerWindow = new Window
        {
            Width = 380,
            Height = 280,
            Title = "Select Emoji",
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            CanResize = false,
            SystemDecorations = SystemDecorations.BorderOnly,
            Topmost = true // Ensure it appears above selection overlays
        };

        // Create emoji grid
        var scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var wrapPanel = new WrapPanel
        {
            Margin = new Thickness(3),
            ItemWidth = 32,
            ItemHeight = 32
        };

        // Add common emojis
        foreach (var emoji in EmojiAnnotation.CommonEmojis)
        {
            var textBlock = new TextBlock
            {
                Text = emoji,
                FontSize = 18,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                FontFamily = new FontFamily("Segoe UI Emoji"),
                TextAlignment = Avalonia.Media.TextAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                LineHeight = 30
            };

            var button = new Button
            {
                Content = textBlock,
                Width = 32,
                Height = 32,
                Margin = new Thickness(1),
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromArgb(64, 128, 128, 128)),
                BorderThickness = new Thickness(1),
                Cursor = new Cursor(StandardCursorType.Hand),
                Padding = new Thickness(0, 6, 0, 0),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                CornerRadius = new CornerRadius(4)
            };

            button.Click += (s, e) =>
            {
                SelectEmoji(emoji);
                emojiPickerWindow?.Close();
            };

            wrapPanel.Children.Add(button);
        }

        scrollViewer.Content = wrapPanel;
        emojiPickerWindow.Content = scrollViewer;

        // Position the window to the right of the emoji button
        if (this.FindControl<Button>("EmojiPickerButton") is { } pickerButton)
        {
            var buttonBounds = pickerButton.Bounds;
            var parentWindow = this.GetVisualRoot() as Window;
            if (parentWindow != null)
            {
                // Get the button's position relative to screen
                var buttonPoint = pickerButton.PointToScreen(new Point(0, 0));
                
                // Position to the right of the button with a small gap
                var x = buttonPoint.X + (int)buttonBounds.Width + 5;
                var y = buttonPoint.Y;
                
                // Ensure the picker doesn't go off-screen
                var screen = parentWindow.Screens.ScreenFromPoint(buttonPoint);
                if (screen != null)
                {
                    var maxX = screen.WorkingArea.Width - 380; // picker width
                    var maxY = screen.WorkingArea.Height - 280; // picker height
                    
                    if (x > maxX)
                    {
                        // Position to the left of the button instead
                        x = buttonPoint.X - 380 - 5;
                    }
                    
                    if (y > maxY)
                    {
                        y = maxY;
                    }
                    
                    // Ensure minimum position
                    x = Math.Max(0, x);
                    y = Math.Max(0, y);
                }

                emojiPickerWindow.Position = new PixelPoint(x, y);
            }
        }

        // Handle close events
        emojiPickerWindow.Deactivated += (s, e) => emojiPickerWindow?.Close();

        emojiPickerWindow.Show();
    }

    private void SelectEmoji(string emoji)
    {
        // Update current emoji display
        if (this.FindControl<TextBlock>("CurrentEmojiText") is { } emojiText)
        {
            emojiText.Text = emoji;
        }

        // Notify annotation service to use this emoji
        if (Target != null)
        {
            // For now, we'll handle this in the NewAnnotationOverlay when creating emoji annotations
            Log.Information("Selected emoji: {Emoji}", emoji);
        }
    }

    private void SetupActionButtons()
    {
        // Clear button
        if (this.FindControl<Button>("ClearButton") is { } clearButton)
        {
            clearButton.Click += (_, __) =>
            {
                Target?.Clear();
                // Ensure focus returns to overlay for immediate undo/redo
                Target?.Focus();
            };
        }

        // Delete button
        if (this.FindControl<Button>("DeleteButton") is { } deleteButton)
        {
            deleteButton.Click += (_, __) =>
            {
                Target?.DeleteSelected();
                // Ensure focus returns to overlay for immediate undo/redo
                Target?.Focus();
            };
        }

        // Export button
        if (this.FindControl<Button>("ExportButton") is { } exportButton)
        {
            exportButton.Click += (_, __) =>
            {
                Target?.RequestExport();
                Target?.Focus();
            };
        }
    }

    private void SetupSliders()
    {
        // Stroke width slider
        if (this.FindControl<Slider>("WidthSlider") is { } widthSlider &&
            this.FindControl<TextBlock>("WidthValueText") is { } widthText)
        {
            widthSlider.ValueChanged += OnWidthSliderChanged;
        }

        // Font size slider
        if (this.FindControl<Slider>("FontSizeSlider") is { } fontSlider &&
            this.FindControl<TextBlock>("FontSizeValueText") is { } fontText)
        {
            fontSlider.ValueChanged += OnFontSizeSliderChanged;
        }
    }

    private void OnWidthSliderChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (this.FindControl<TextBlock>("WidthValueText") is { } widthText)
        {
            var value = (int)e.NewValue;
            widthText.Text = value.ToString();
            Target?.SetStrokeWidth(value);
        }
    }

    private void OnFontSizeSliderChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (this.FindControl<TextBlock>("FontSizeValueText") is { } fontText)
        {
            var value = (int)e.NewValue;
            fontText.Text = value.ToString();
            Target?.SetFontSize(value);
        }
    }

    /// <summary>
    /// Set target annotation overlay
    /// </summary>
    public void SetTarget(NewAnnotationOverlay target)
    {
        // Unsubscribe from previous target
        if (Target != null)
        {
            Target.StyleChanged -= OnTargetStyleChanged;
        }
        
        Target = target;
        
        // Subscribe to new target's style changes
        if (target != null)
        {
            target.StyleChanged += OnTargetStyleChanged;
            UpdateUIFromTarget();
        }
    }

    /// <summary>
    /// Handle style changes from target overlay
    /// </summary>
    private void OnTargetStyleChanged(object? sender, StyleChangedEventArgs e)
    {
        try
        {
            // Update UI to reflect the new style without triggering events
            UpdateUIFromStyle(e.NewStyle);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error handling target style change");
        }
    }

    /// <summary>
    /// Update UI state from target
    /// </summary>
    public void UpdateUIFromTarget()
    {
        if (Target == null) return;

        // Update tool selection
        var toolButton = Target.CurrentTool switch
        {
            AnnotationToolType.None => this.FindControl<ToggleButton>("SelectToolButton"),
            AnnotationToolType.Arrow => this.FindControl<ToggleButton>("ArrowToolButton"),
            AnnotationToolType.Rectangle => this.FindControl<ToggleButton>("RectangleToolButton"),
            AnnotationToolType.Ellipse => this.FindControl<ToggleButton>("EllipseToolButton"),
            AnnotationToolType.Text => this.FindControl<ToggleButton>("TextToolButton"),
            AnnotationToolType.Freehand => this.FindControl<ToggleButton>("FreehandToolButton"),
            AnnotationToolType.Emoji => this.FindControl<ToggleButton>("EmojiToolButton"),
            _ => this.FindControl<ToggleButton>("SelectToolButton")
        };

        if (toolButton != null)
        {
            // Update UI without simulating click to avoid changing the tool
            // Uncheck all other tool buttons
            foreach (var button in _toolButtons)
            {
                if (button != toolButton)
                {
                    button.IsChecked = false;
                    button.Background = Brushes.Transparent;
                }
            }

            // Check the current tool button and set background
            toolButton.IsChecked = true;
            toolButton.Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
        }

        // Update style-related UI
        UpdateUIFromStyle(Target.CurrentStyle);
    }

    /// <summary>
    /// Update UI elements from style (without triggering events)
    /// </summary>
    private void UpdateUIFromStyle(IAnnotationStyle style)
    {
        // Update color selection
        _currentColor = style.StrokeColor;
        UpdateCurrentColorDisplay();

        // Update stroke width (temporarily disable event to avoid recursion)
        if (this.FindControl<Slider>("WidthSlider") is { } widthSlider &&
            this.FindControl<TextBlock>("WidthValueText") is { } widthText)
        {
            var width = (int)style.StrokeWidth;
            
            // Temporarily disable event handler
            widthSlider.ValueChanged -= OnWidthSliderChanged;
            widthSlider.Value = width;
            widthText.Text = width.ToString();
            widthSlider.ValueChanged += OnWidthSliderChanged;
        }

        // Update font size (temporarily disable event to avoid recursion)
        if (this.FindControl<Slider>("FontSizeSlider") is { } fontSlider &&
            this.FindControl<TextBlock>("FontSizeValueText") is { } fontText)
        {
            var fontSize = (int)style.FontSize;
            
            // Temporarily disable event handler
            fontSlider.ValueChanged -= OnFontSizeSliderChanged;
            fontSlider.Value = fontSize;
            fontText.Text = fontSize.ToString();
            fontSlider.ValueChanged += OnFontSizeSliderChanged;
        }

        // Update panel visibility
        bool layoutChanged = false;
        
        if (this.FindControl<StackPanel>("FontSizePanel") is { } fontPanel && Target != null)
        {
            bool wasVisible = fontPanel.IsVisible;
            fontPanel.IsVisible = Target.CurrentTool == AnnotationToolType.Text;
            if (wasVisible != fontPanel.IsVisible) layoutChanged = true;
        }
        
        // Emoji panel is now always visible (no dynamic show/hide)
        
        // If visibility changed, trigger repositioning
        if (layoutChanged)
        {
            InvalidateArrange();
            InvalidateMeasure();
            
            // Use dispatcher to ensure layout is updated before repositioning
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Find parent overlay window and trigger toolbar repositioning
                if (this.FindAncestorOfType<OverlayWindow>() is { } overlayWindow && Target != null)
                {
                    // Use the correct Target annotator instance instead of FindControl
                    // Use dynamic to avoid AOT reflection warnings
                    dynamic dynamicOverlay = overlayWindow;
                    try
                    {
                        dynamicOverlay.UpdateToolbarPosition(Target.SelectionRect);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to update toolbar position");
                    }
                }
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
    }
}