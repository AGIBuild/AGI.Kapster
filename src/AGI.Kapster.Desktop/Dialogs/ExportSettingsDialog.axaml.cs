using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AGI.Kapster.Desktop.Models;
using Serilog;

namespace AGI.Kapster.Desktop.Dialogs;

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
            ExportFormat.BMP => EstimateBmpSize(pixelCount),
            ExportFormat.TIFF => EstimateTiffSize(pixelCount),
            ExportFormat.GIF => EstimateGifSize(pixelCount),
            _ => pixelCount * 3 // Default RGB estimation
        };

        return Math.Max(estimatedBytes, 1024); // Minimum 1KB
    }

    private long EstimatePngSize(int pixelCount)
    {
        // PNG compression for screenshots with annotations
        // Screenshots typically have large uniform areas (better compression)
        // But annotations add complexity (worse compression)
        var baseSize = pixelCount * 4; // RGBA for transparency support

        // Adjusted PNG compression ratios based on feedback (increase by ~2.5x)
        var compressionRatio = _settings.Compression switch
        {
            >= 8 => 0.08, // Very high compression - mostly uniform areas
            >= 6 => 0.10, // High compression - typical screenshots
            >= 4 => 0.15, // Medium compression - screenshots with annotations
            >= 2 => 0.20, // Low compression - complex images
            _ => 0.30      // Minimal compression
        };

        // Add overhead for PNG headers and metadata
        var overhead = Math.Min(pixelCount / 1000, 2048); // 0.1% overhead, max 2KB
        return (long)(baseSize * compressionRatio) + overhead;
    }

    private long EstimateJpegSize(int pixelCount, int quality)
    {
        // JPEG size estimation for screenshots
        // Screenshots compress differently than photos due to sharp edges and text
        var baseSize = pixelCount * 3; // RGB 24-bit

        // Much more conservative compression ratios to match actual JPEG file sizes
        var compressionRatio = quality switch
        {
            >= 95 => 0.13, // Very high quality - minimal compression
            >= 90 => 0.08, // High quality - light compression
            >= 80 => 0.05, // Good quality - moderate compression
            >= 70 => 0.03, // Medium quality - noticeable compression
            >= 60 => 0.025, // Lower quality - significant compression
            >= 50 => 0.02, // Low quality - heavy compression
            _ => 0.015     // Very low quality - maximum compression
        };

        // Screenshots with text and UI elements don't compress as well as photos
        var screenshotFactor = 1.0; // Remove screenshot penalty for more accurate estimation

        // Add JPEG headers and metadata overhead
        var overhead = Math.Min(pixelCount / 2000, 1024); // 0.05% overhead, max 1KB

        return (long)(baseSize * compressionRatio * screenshotFactor) + overhead;
    }

    private long EstimateWebpSize(int pixelCount, int quality)
    {
        // WebP compression for screenshots
        // WebP is more efficient than JPEG for screenshots due to better lossless compression
        var baseSize = pixelCount * 3; // RGB 24-bit

        // WebP quality-based compression (extremely conservative to match reality)
        var compressionRatio = quality switch
        {
            >= 95 => 0.08, // Very high quality
            >= 90 => 0.06, // High quality
            >= 80 => 0.04, // Good quality
            >= 70 => 0.025, // Medium quality
            >= 60 => 0.02, // Lower quality
            >= 50 => 0.015, // Low quality
            _ => 0.01      // Very low quality
        };

        // WebP handles screenshots better than JPEG
        var screenshotFactor = 1.0; // No penalty for more accurate estimation

        // Add WebP headers overhead
        var overhead = Math.Min(pixelCount / 3000, 512); // Smaller overhead than JPEG

        return (long)(baseSize * compressionRatio * screenshotFactor) + overhead;
    }

    private long EstimateTiffSize(int pixelCount)
    {
        // TIFF with LZW compression for screenshots
        var baseSize = pixelCount * 4; // RGBA with transparency support

        // TIFF compression is similar to PNG but with more overhead
        var compressionRatio = 0.05; // Much more conservative estimate for screenshot content

        // TIFF has significant metadata overhead
        var overhead = Math.Min(pixelCount / 500, 4096); // 0.2% overhead, max 4KB

        return (long)(baseSize * compressionRatio) + overhead;
    }

    private long EstimateGifSize(int pixelCount)
    {
        // GIF with 256 color palette and LZW compression
        // Screenshots often exceed 256 colors, so quality will be reduced
        var baseSize = pixelCount; // 8-bit per pixel after color reduction

        // GIF compression ratio depends on color complexity
        var compressionRatio = 0.1; // Much more conservative estimate for GIF compression

        // GIF has palette and metadata overhead
        var paletteSize = 256 * 3; // 256 colors * 3 bytes (RGB)
        var overhead = paletteSize + 256; // Palette + headers (reduced)

        return (long)(baseSize * compressionRatio) + overhead;
    }

    private long EstimateBmpSize(int pixelCount)
    {
        // BMP is uncompressed format
        var width = (int)_imageSize.Width;
        var height = (int)_imageSize.Height;

        // BMP header overhead
        var bmpHeaderSize = 54; // Standard BMP header

        // Row padding - BMP rows must be aligned to 4-byte boundaries
        var bytesPerRow = width * 3; // 3 bytes per pixel (RGB)
        var paddingPerRow = (4 - (bytesPerRow % 4)) % 4; // Padding to align to 4 bytes
        var totalRowSize = bytesPerRow + paddingPerRow;
        var imageDataSize = totalRowSize * height;

        return imageDataSize + bmpHeaderSize;
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
