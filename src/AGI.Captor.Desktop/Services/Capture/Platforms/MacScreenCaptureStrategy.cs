using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.VisualTree;
using SkiaSharp;
using Serilog;

namespace AGI.Captor.Desktop.Services.Overlay.Platforms;

/// <summary>
/// macOS-specific screen capture implementation
/// </summary>
public class MacScreenCaptureStrategy : IScreenCaptureStrategy
{
    public bool SupportsWindowCapture => true;
    public bool SupportsElementCapture => false; // Not implemented yet
    public bool IsHardwareAccelerated => true; // macOS uses Core Graphics
    
    public async Task<SKBitmap?> CaptureFullScreenAsync(Screen screen)
    {
        // TODO: Implement using CGWindowListCreateImage
        // For now, return null to indicate not implemented
        Log.Warning("macOS screen capture not yet implemented");
        return await Task.FromResult<SKBitmap?>(null);
    }
    
    public async Task<SKBitmap?> CaptureWindowAsync(IntPtr windowHandle)
    {
        // TODO: Implement using CGWindowListCreateImage with specific window ID
        Log.Warning("macOS window capture not yet implemented");
        return await Task.FromResult<SKBitmap?>(null);
    }
    
    public async Task<SKBitmap?> CaptureRegionAsync(PixelRect region)
    {
        try
        {
            // Create temporary file for screenshot
            var tempFile = System.IO.Path.GetTempFileName() + ".png";
            
            // Use screencapture command to capture region
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "screencapture",
                    Arguments = $"-R {region.X},{region.Y},{region.Width},{region.Height} \"{tempFile}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && System.IO.File.Exists(tempFile))
            {
                try
                {
                    var data = System.IO.File.ReadAllBytes(tempFile);
                    return SKBitmap.Decode(data);
                }
                finally
                {
                    try { System.IO.File.Delete(tempFile); } catch { }
                }
            }
            else
            {
                Log.Error("screencapture command failed with exit code: {ExitCode}", process.ExitCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to capture region on macOS");
            return null;
        }
    }
    
    public async Task<SKBitmap?> CaptureElementAsync(IElementInfo element)
    {
        // Element capture not supported on macOS yet
        Log.Warning("macOS element capture not supported");
        return await Task.FromResult<SKBitmap?>(null);
    }
    
    public async Task<SKBitmap?> CaptureWindowRegionAsync(Avalonia.Rect windowRect, Avalonia.Visual window)
    {
        try
        {
            // Convert window coordinates to screen coordinates
            if (window is Window w)
            {
                var p1 = w.PointToScreen(new Point(windowRect.X, windowRect.Y));
                var p2 = w.PointToScreen(new Point(windowRect.Right, windowRect.Bottom));
                
                var screenRect = new PixelRect(
                    Math.Min(p1.X, p2.X),
                    Math.Min(p1.Y, p2.Y),
                    Math.Max(1, Math.Abs(p2.X - p1.X)),
                    Math.Max(1, Math.Abs(p2.Y - p1.Y)));
                
                return await CaptureRegionAsync(screenRect);
            }
            else
            {
                Log.Warning("CaptureWindowRegionAsync requires a Window instance");
                return null;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to capture window region");
            return null;
        }
    }
}


