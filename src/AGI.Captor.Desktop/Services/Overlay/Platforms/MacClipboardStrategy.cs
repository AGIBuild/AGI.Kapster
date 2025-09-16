using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
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
            // For now, use Avalonia's clipboard API
            // TODO: Implement native macOS clipboard access using NSPasteboard
            var dataObject = new DataObject();
            
            // Convert SKBitmap to Avalonia Bitmap
            using var stream = new System.IO.MemoryStream();
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            data.SaveTo(stream);
            stream.Position = 0;
            
            dataObject.Set("image/png", stream.ToArray());
            
            // Try to get clipboard from app
            var clipboard = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow?.Clipboard
                : null;
                
            if (clipboard != null)
            {
                await clipboard.SetDataObjectAsync(dataObject);
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
            var clipboard = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow?.Clipboard
                : null;
                
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(text);
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
            var clipboard = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow?.Clipboard
                : null;
                
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
            var clipboard = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow?.Clipboard
                : null;
                
            if (clipboard != null)
            {
                await clipboard.ClearAsync();
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
}
