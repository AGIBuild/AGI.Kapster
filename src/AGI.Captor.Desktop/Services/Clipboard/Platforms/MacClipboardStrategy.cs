using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using SkiaSharp;
using Serilog;

namespace AGI.Captor.Desktop.Services.Overlay.Platforms;

/// <summary>
/// macOS-specific clipboard implementation
/// </summary>
public class MacClipboardStrategy : IClipboardStrategy
{
    public bool SupportsMultipleFormats => true;
    public bool SupportsImages => true;

    public async Task<bool> SetImageAsync(SKBitmap bitmap)
    {
        try
        {
            // Convert SKBitmap to Avalonia Bitmap for better compatibility
            var avaloniaBitmap = BitmapConverter.ConvertToAvaloniaBitmap(bitmap);
            if (avaloniaBitmap == null)
            {
                Log.Warning("macOS: Failed to convert SKBitmap to Avalonia Bitmap");
                return false;
            }

            // Create DataObject with multiple formats for better compatibility
            var dataObject = new DataObject();

            // Set as Avalonia Bitmap directly (primary format)
            dataObject.Set("image/png", avaloniaBitmap);

            // Also convert to PNG byte array for additional compatibility
            using var stream = new System.IO.MemoryStream();
            avaloniaBitmap.Save(stream);
            stream.Position = 0;
            var pngData = stream.ToArray();

            // Set multiple formats for maximum compatibility
            dataObject.Set("public.png", pngData); // macOS UTI format
            dataObject.Set("PNG", pngData);
            dataObject.Set("image/x-png", pngData);
            dataObject.Set("CF_DIB", pngData); // Windows compatibility

            // Try to get clipboard from various sources
            var clipboardInfo = GetAvailableClipboardWithWindow();

            if (clipboardInfo.clipboard != null)
            {
                await clipboardInfo.clipboard.SetDataObjectAsync(dataObject);
                Log.Debug("macOS: Successfully set image to clipboard");

                // If we created a temporary window, keep it alive briefly to ensure clipboard operation completes
                if (clipboardInfo.tempWindow != null)
                {
                    await Task.Delay(500); // Give clipboard time to fully process the data

                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        try
                        {
                            clipboardInfo.tempWindow.Close();
                            Log.Debug("macOS: Closed temporary clipboard window");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "macOS: Failed to close temporary window");
                        }
                    });
                }

                return true;
            }

            Log.Warning("macOS: No clipboard access available");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "macOS: Failed to set image to clipboard");
            return false;
        }
    }

    public async Task<bool> SetTextAsync(string text)
    {
        try
        {
            var clipboard = GetAvailableClipboard();

            if (clipboard != null)
            {
                await clipboard.SetTextAsync(text);
                Log.Debug("macOS: Successfully set text to clipboard");
                return true;
            }

            Log.Warning("macOS: No clipboard access available");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "macOS: Failed to set text to clipboard");
            return false;
        }
    }

    public async Task<SKBitmap?> GetImageAsync()
    {
        // TODO: Implement native macOS clipboard access
        Log.Warning("macOS: GetImageAsync not yet implemented");
        return await Task.FromResult<SKBitmap?>(null);
    }

    public async Task<string?> GetTextAsync()
    {
        try
        {
            var clipboard = GetAvailableClipboard();

            if (clipboard != null)
            {
                return await clipboard.GetTextAsync();
            }

            Log.Warning("macOS: No clipboard access available");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "macOS: Failed to get text from clipboard");
            return null;
        }
    }

    public async Task<bool> ClearAsync()
    {
        try
        {
            var clipboard = GetAvailableClipboard();

            if (clipboard != null)
            {
                await clipboard.ClearAsync();
                Log.Debug("macOS: Successfully cleared clipboard");
                return true;
            }

            Log.Warning("macOS: No clipboard access available");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "macOS: Failed to clear clipboard");
            return false;
        }
    }

    /// <summary>
    /// Intelligently find an available clipboard instance
    /// </summary>
    private IClipboard? GetAvailableClipboard()
    {
        var clipboardInfo = GetAvailableClipboardWithWindow();

        // If we created a temporary window for non-image operations, clean it up after a delay
        if (clipboardInfo.tempWindow != null)
        {
            Task.Delay(100).ContinueWith(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        clipboardInfo.tempWindow.Close();
                        Log.Debug("macOS: Closed temporary clipboard window");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "macOS: Failed to close temporary window");
                    }
                });
            });
        }

        return clipboardInfo.clipboard;
    }

    /// <summary>
    /// Find an available clipboard and optionally return the window it came from
    /// </summary>
    private (IClipboard? clipboard, Window? tempWindow) GetAvailableClipboardWithWindow()
    {
        try
        {
            // Try to find any open window with clipboard access
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var windows = desktop.Windows;
                if (windows != null)
                {
                    foreach (var window in windows)
                    {
                        if (window?.Clipboard != null)
                        {
                            Log.Debug("macOS: Using clipboard from window: {WindowType}", window.GetType().Name);
                            return (window.Clipboard, null);
                        }
                    }
                }
            }

            // Try to get from TopLevel of any visible window
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktopApp)
            {
                var windows = desktopApp.Windows;
                if (windows != null)
                {
                    foreach (var window in windows)
                    {
                        var topLevel = TopLevel.GetTopLevel(window);
                        if (topLevel?.Clipboard != null)
                        {
                            Log.Debug("macOS: Using clipboard from TopLevel of: {WindowType}", window.GetType().Name);
                            return (topLevel.Clipboard, null);
                        }
                    }
                }
            }

            // As a last resort, create a temporary hidden window
            Log.Debug("macOS: Creating temporary window for clipboard access");
            var tempWindow = new Window
            {
                Width = 1,
                Height = 1,
                ShowInTaskbar = false,
                WindowState = WindowState.Minimized,
                SystemDecorations = SystemDecorations.None,
                Opacity = 0,
                Title = "Clipboard Helper"
            };

            tempWindow.Show();
            var clipboard = tempWindow.Clipboard;

            // Return both clipboard and window so caller can manage lifecycle
            return (clipboard, tempWindow);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "macOS: Failed to get available clipboard");
            return (null, null);
        }
    }
}


