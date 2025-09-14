using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Serilog;

namespace AGI.Captor.Desktop.Dialogs;

public partial class ExportProgressDialog : Window
{
    private string? _exportedFilePath;
    
    public ExportProgressDialog()
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
        // Open folder button
        if (this.FindControl<Button>("OpenFolderButton") is { } openButton)
        {
            openButton.Click += OnOpenFolderClick;
        }

        // Close button
        if (this.FindControl<Button>("CloseButton") is { } closeButton)
        {
            closeButton.Click += OnCloseClick;
        }
    }

    /// <summary>
    /// Update export progress
    /// </summary>
    public void UpdateProgress(int percentage, string status)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (this.FindControl<ProgressBar>("ProgressBar") is { } progressBar)
                progressBar.Value = percentage;
                
            if (this.FindControl<TextBlock>("StatusText") is { } statusText)
                statusText.Text = status;
        });
    }

    /// <summary>
    /// Set file information
    /// </summary>
    public void SetFileInfo(string fileName, string format, long fileSize = 0)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (this.FindControl<TextBlock>("FileInfoText") is { } fileInfoText)
            {
                var sizeText = fileSize > 0 ? $" ({FormatFileSize(fileSize)})" : "";
                fileInfoText.Text = $"{fileName} • {format}{sizeText}";
            }
        });
    }

    /// <summary>
    /// Show export success
    /// </summary>
    public void ShowSuccess(string filePath)
    {
        _exportedFilePath = filePath;
        
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Update title
            if (this.FindControl<TextBlock>("TitleText") is { } titleText)
                titleText.Text = "Export Completed";

            // Hide progress bar
            if (this.FindControl<ProgressBar>("ProgressBar") is { } progressBar)
                progressBar.IsVisible = false;
                
            if (this.FindControl<TextBlock>("StatusText") is { } statusText)
                statusText.IsVisible = false;

            // Show success message
            if (this.FindControl<Border>("ResultPanel") is { } resultPanel)
            {
                resultPanel.IsVisible = true;
                resultPanel.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(40, 0, 120, 0)); // Green tint
            }

            if (this.FindControl<TextBlock>("ResultTitle") is { } resultTitle)
            {
                resultTitle.Text = "✓ Export Successful";
                resultTitle.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.LightGreen);
            }

            if (this.FindControl<TextBlock>("ResultMessage") is { } resultMessage)
            {
                var fileName = Path.GetFileName(filePath);
                var fileSize = new FileInfo(filePath).Length;
                resultMessage.Text = $"Saved {fileName} ({FormatFileSize(fileSize)}) to:\n{Path.GetDirectoryName(filePath)}";
            }

            // Show buttons
            if (this.FindControl<StackPanel>("ButtonPanel") is { } buttonPanel)
                buttonPanel.IsVisible = true;
        });
    }

    /// <summary>
    /// Show export error
    /// </summary>
    public void ShowError(string errorMessage)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Update title
            if (this.FindControl<TextBlock>("TitleText") is { } titleText)
                titleText.Text = "Export Failed";

            // Hide progress bar
            if (this.FindControl<ProgressBar>("ProgressBar") is { } progressBar)
                progressBar.IsVisible = false;
                
            if (this.FindControl<TextBlock>("StatusText") is { } statusText)
                statusText.IsVisible = false;

            // Show error message
            if (this.FindControl<Border>("ResultPanel") is { } resultPanel)
            {
                resultPanel.IsVisible = true;
                resultPanel.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(40, 120, 0, 0)); // Red tint
            }

            if (this.FindControl<TextBlock>("ResultTitle") is { } resultTitle)
            {
                resultTitle.Text = "✗ Export Failed";
                resultTitle.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.LightCoral);
            }

            if (this.FindControl<TextBlock>("ResultMessage") is { } resultMessage)
            {
                resultMessage.Text = errorMessage;
            }

            // Show close button only
            if (this.FindControl<StackPanel>("ButtonPanel") is { } buttonPanel)
            {
                buttonPanel.IsVisible = true;
                if (this.FindControl<Button>("OpenFolderButton") is { } openButton)
                    openButton.IsVisible = false;
            }
        });
    }

    private void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_exportedFilePath) || !File.Exists(_exportedFilePath))
            return;

        try
        {
            // Try to select the file in explorer
            var directoryPath = Path.GetDirectoryName(_exportedFilePath);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    // Windows: Open explorer and select the file
                    Process.Start("explorer.exe", $"/select,\"{_exportedFilePath}\"");
                }
                else if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    // Linux: Open file manager (try common ones)
                    try { Process.Start("nautilus", directoryPath); }
                    catch { try { Process.Start("dolphin", directoryPath); } catch { } }
                }
                else if (Environment.OSVersion.Platform == PlatformID.MacOSX)
                {
                    // macOS: Open finder and select the file
                    Process.Start("open", $"-R \"{_exportedFilePath}\"");
                }
                else
                {
                    // Fallback: just open the directory
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = directoryPath,
                        UseShellExecute = true
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open file location: {FilePath}", _exportedFilePath);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }
}
