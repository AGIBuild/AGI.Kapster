using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AGI.Captor.App.Models;
using Serilog;

namespace AGI.Captor.App.Dialogs;

public partial class ExportSettingsDialog : Window
{
    private ExportSettings _settings;
    private Avalonia.Size _imageSize;
    
    public ExportSettings Settings => _settings;
    public bool? DialogResult { get; private set; }

    public ExportSettingsDialog()
    {
        InitializeComponent();
        _settings = new ExportSettings();
        SetupUI();
        SetupEventHandlers();
        UpdatePreview();
        
        // Handle window closing event
        Closing += (_, e) =>
        {
            if (!DialogResult.HasValue)
                DialogResult = false;
        };
    }

    public ExportSettingsDialog(ExportSettings initialSettings)
    {
        InitializeComponent();
        _settings = initialSettings ?? new ExportSettings();
        _imageSize = new Avalonia.Size(1920, 1080); // Default size
        SetupUI();
        SetupEventHandlers();
        LoadSettings();
        UpdatePreview();
        
        // Handle window closing event
        Closing += (_, e) =>
        {
            if (!DialogResult.HasValue)
                DialogResult = false;
        };
    }

    public ExportSettingsDialog(ExportSettings initialSettings, Avalonia.Size imageSize)
    {
        InitializeComponent();
        _settings = initialSettings ?? new ExportSettings();
        _imageSize = imageSize;
        SetupUI();
        SetupEventHandlers();
        LoadSettings();
        UpdatePreview();
        
        // Handle window closing event
        Closing += (_, e) =>
        {
            if (!DialogResult.HasValue)
                DialogResult = false;
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void SetupUI()
    {
        // Setup format combo box
        var formatCombo = this.FindControl<ComboBox>("FormatComboBox");
        if (formatCombo != null)
        {
            var formats = Enum.GetValues<ExportFormat>()
                .Select(f => new ComboBoxItem 
                { 
                    Content = GetFormatDescription(f), 
                    Tag = f 
                })
                .ToArray();
            
            formatCombo.ItemsSource = formats;
            formatCombo.SelectedIndex = 0;
        }
    }

    private void SetupEventHandlers()
    {
        // Format selection change
        if (this.FindControl<ComboBox>("FormatComboBox") is { } formatCombo)
        {
            formatCombo.SelectionChanged += OnFormatChanged;
        }

        // Quality slider
        if (this.FindControl<Slider>("QualitySlider") is { } qualitySlider &&
            this.FindControl<TextBlock>("QualityText") is { } qualityText)
        {
            qualitySlider.ValueChanged += (_, e) =>
            {
                var value = (int)e.NewValue;
                qualityText.Text = value.ToString();
                _settings.Quality = value;
                UpdatePreview();
            };
        }

        // Compression slider
        if (this.FindControl<Slider>("CompressionSlider") is { } compressionSlider &&
            this.FindControl<TextBlock>("CompressionText") is { } compressionText)
        {
            compressionSlider.ValueChanged += (_, e) =>
            {
                var value = (int)e.NewValue;
                compressionText.Text = value.ToString();
                _settings.Compression = value;
                UpdatePreview();
            };
        }

        // DPI selection
        if (this.FindControl<ComboBox>("DpiComboBox") is { } dpiCombo)
        {
            dpiCombo.SelectionChanged += (_, e) =>
            {
                if (dpiCombo.SelectedItem is ComboBoxItem item && 
                    item.Tag is string dpiValue && 
                    int.TryParse(dpiValue, out int dpi))
                {
                    _settings.DPI = dpi;
                    UpdatePreview();
                }
            };
        }

        // Transparency checkbox
        if (this.FindControl<CheckBox>("PreserveTransparencyCheckBox") is { } transparencyCheck)
        {
            transparencyCheck.IsCheckedChanged += (_, e) =>
            {
                _settings.PreserveTransparency = transparencyCheck.IsChecked ?? false;
                UpdatePreview();
            };
        }

        // Dialog buttons
        if (this.FindControl<Button>("OkButton") is { } okButton)
        {
            okButton.Click += OnOkClick;
        }

        if (this.FindControl<Button>("CancelButton") is { } cancelButton)
        {
            cancelButton.Click += OnCancelClick;
        }
    }

    private void LoadSettings()
    {
        // Load format
        if (this.FindControl<ComboBox>("FormatComboBox") is { } formatCombo)
        {
            for (int i = 0; i < formatCombo.ItemCount; i++)
            {
                if (formatCombo.Items[i] is ComboBoxItem item && 
                    item.Tag is ExportFormat format && 
                    format == _settings.Format)
                {
                    formatCombo.SelectedIndex = i;
                    break;
                }
            }
        }

        // Load quality
        if (this.FindControl<Slider>("QualitySlider") is { } qualitySlider &&
            this.FindControl<TextBlock>("QualityText") is { } qualityText)
        {
            qualitySlider.Value = _settings.Quality;
            qualityText.Text = _settings.Quality.ToString();
        }

        // Load compression
        if (this.FindControl<Slider>("CompressionSlider") is { } compressionSlider &&
            this.FindControl<TextBlock>("CompressionText") is { } compressionText)
        {
            compressionSlider.Value = _settings.Compression;
            compressionText.Text = _settings.Compression.ToString();
        }

        // Load DPI
        if (this.FindControl<ComboBox>("DpiComboBox") is { } dpiCombo)
        {
            for (int i = 0; i < dpiCombo.ItemCount; i++)
            {
                if (dpiCombo.Items[i] is ComboBoxItem item && 
                    item.Tag is string dpiValue && 
                    int.TryParse(dpiValue, out int dpi) && 
                    dpi == _settings.DPI)
                {
                    dpiCombo.SelectedIndex = i;
                    break;
                }
            }
        }

        // Load transparency
        if (this.FindControl<CheckBox>("PreserveTransparencyCheckBox") is { } transparencyCheck)
        {
            transparencyCheck.IsChecked = _settings.PreserveTransparency;
        }

        // Load background color
        UpdateFormatSpecificUI();
    }

    private void OnFormatChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && 
            combo.SelectedItem is ComboBoxItem item && 
            item.Tag is ExportFormat format)
        {
            _settings.Format = format;
            UpdateFormatSpecificUI();
            UpdatePreview();
        }
    }

    private void UpdateFormatSpecificUI()
    {
        var qualityPanel = this.FindControl<Border>("QualityPanel");
        var compressionPanel = this.FindControl<Grid>("CompressionPanel");
        var transparencyPanel = this.FindControl<CheckBox>("PreserveTransparencyCheckBox");

        if (qualityPanel != null)
            qualityPanel.IsVisible = _settings.SupportsQuality();

        if (compressionPanel != null)
            compressionPanel.IsVisible = _settings.SupportsCompression();

        if (transparencyPanel != null)
            transparencyPanel.IsVisible = _settings.SupportsTransparency();
    }


    private void UpdatePreview()
    {
        if (this.FindControl<TextBlock>("PreviewText") is { } previewText)
        {
            var format = _settings.Format.ToString();
            var quality = _settings.SupportsQuality() ? $", Quality: {_settings.Quality}%" : "";
            var compression = _settings.SupportsCompression() ? $", Compression: {_settings.Compression}" : "";
            var transparency = _settings.SupportsTransparency() && _settings.PreserveTransparency ? ", With Transparency" : "";
            
            previewText.Text = $"Format: {format}{quality}{compression}, DPI: {_settings.DPI}{transparency}";
        }
        
        UpdateFileSizeEstimation();
    }

    private void UpdateFileSizeEstimation()
    {
        try
        {
            var estimatedSize = CalculateEstimatedFileSize();
            var sizeText = FormatFileSize(estimatedSize);
            
            // Update file size text
            if (this.FindControl<TextBlock>("FileSizeText") is { } fileSizeText)
            {
                fileSizeText.Text = $"~{sizeText}";
            }
            
            // Update image dimensions
            if (this.FindControl<TextBlock>("ImageDimensionsText") is { } dimensionsText)
            {
                dimensionsText.Text = $"{(int)_imageSize.Width}×{(int)_imageSize.Height}";
            }
            
            // Update compression info based on format
            if (this.FindControl<TextBlock>("CompressionInfoText") is { } compressionText)
            {
                compressionText.Text = _settings.Format switch
                {
                    ExportFormat.JPEG => "Higher quality = larger file size, no transparency",
                    ExportFormat.PNG => "Lossless compression, supports transparency",
                    ExportFormat.WebP => "Modern format, good compression with transparency",
                    ExportFormat.BMP => "Uncompressed format, largest file size",
                    ExportFormat.TIFF => "Professional format, lossless with compression",
                    ExportFormat.GIF => "Limited colors, supports transparency and animation",
                    _ => "File size varies with settings"
                };
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to update file size estimation");
        }
    }

    private long CalculateEstimatedFileSize()
    {
        var width = (int)_imageSize.Width;
        var height = (int)_imageSize.Height;
        var pixelCount = width * height;
        
        // Base size calculation in bytes
        long estimatedBytes = _settings.Format switch
        {
            ExportFormat.PNG => EstimatePngSize(pixelCount),
            ExportFormat.JPEG => EstimateJpegSize(pixelCount, _settings.Quality),
            ExportFormat.WebP => EstimateWebpSize(pixelCount, _settings.Quality),
            ExportFormat.BMP => pixelCount * 3, // RGB 24-bit
            ExportFormat.TIFF => EstimateTiffSize(pixelCount),
            ExportFormat.GIF => EstimateGifSize(pixelCount),
            _ => pixelCount * 3 // Default RGB estimation
        };
        
        return Math.Max(estimatedBytes, 1024); // Minimum 1KB
    }
    
    private long EstimatePngSize(int pixelCount)
    {
        // PNG compression typically achieves 30-70% compression for screenshots
        // Screenshots with annotations tend to compress less due to complexity
        var baseSize = pixelCount * 3; // RGB
        var compressionRatio = _settings.Compression switch
        {
            >= 7 => 0.4, // High compression
            >= 4 => 0.5, // Medium compression  
            _ => 0.6      // Low compression
        };
        return (long)(baseSize * compressionRatio);
    }
    
    private long EstimateJpegSize(int pixelCount, int quality)
    {
        // JPEG size estimation based on quality
        var qualityFactor = quality / 100.0;
        var baseSize = pixelCount * 0.5; // JPEG is generally smaller than PNG for photos
        return (long)(baseSize * (0.1 + qualityFactor * 0.9));
    }
    
    private long EstimateWebpSize(int pixelCount, int quality)
    {
        // WebP typically 25-35% smaller than JPEG
        var jpegSize = EstimateJpegSize(pixelCount, quality);
        return (long)(jpegSize * 0.7);
    }
    
    private long EstimateTiffSize(int pixelCount)
    {
        // TIFF with compression, similar to PNG but slightly larger
        return (long)(pixelCount * 3 * 0.7);
    }
    
    private long EstimateGifSize(int pixelCount)
    {
        // GIF limited to 256 colors, usually smaller for simple images
        return pixelCount; // 8-bit per pixel
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close(true); // Pass the result to Close method
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close(false); // Pass the result to Close method
    }

    private static string GetFormatDescription(ExportFormat format)
    {
        var field = typeof(ExportFormat).GetField(format.ToString());
        if (field?.GetCustomAttributes(typeof(DescriptionAttribute), false) is DescriptionAttribute[] attributes && 
            attributes.Length > 0)
        {
            return attributes[0].Description;
        }
        return format.ToString();
    }
}
