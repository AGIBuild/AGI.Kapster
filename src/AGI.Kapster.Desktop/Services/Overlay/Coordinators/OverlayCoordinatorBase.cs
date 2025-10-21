using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.Clipboard;
using AGI.Kapster.Desktop.Services.Export.Imaging;
using AGI.Kapster.Desktop.Services.Overlay.State;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Serilog;
using SkiaSharp;

namespace AGI.Kapster.Desktop.Services.Overlay.Coordinators;

/// <summary>
/// Abstract base class for overlay coordinators using Template Method pattern
/// Defines common session lifecycle while allowing platform-specific window creation
/// </summary>
public abstract class OverlayCoordinatorBase : IOverlayCoordinator
{
    protected readonly IOverlaySessionFactory _sessionFactory;
    protected readonly IOverlayWindowFactory _windowFactory;
    protected readonly IScreenCoordinateMapper _coordinateMapper;
    protected readonly IScreenCaptureStrategy? _captureStrategy;
    protected readonly IClipboardStrategy? _clipboardStrategy;
    
    protected IOverlaySession? _currentSession;

    protected OverlayCoordinatorBase(
        IOverlaySessionFactory sessionFactory,
        IOverlayWindowFactory windowFactory,
        IScreenCoordinateMapper coordinateMapper,
        IScreenCaptureStrategy? captureStrategy,
        IClipboardStrategy? clipboardStrategy)
    {
        _sessionFactory = sessionFactory;
        _windowFactory = windowFactory;
        _coordinateMapper = coordinateMapper;
        _captureStrategy = captureStrategy;
        _clipboardStrategy = clipboardStrategy;
    }

    public bool HasActiveSession => _currentSession != null;

    /// <summary>
    /// Template method: defines the overall session creation flow
    /// </summary>
    public async Task<IOverlaySession> StartSessionAsync()
    {
        try
        {
            LogSessionStart();
            
            // Step 1: Close any existing session
            CloseCurrentSession();

            // Step 2: Create new session
            var session = _sessionFactory.CreateSession();

            // Step 3: Get screen information for this session (hook method)
            var screens = await GetScreensAsync();
            LogScreensInitialized(screens.Count);

            // Step 4: Calculate target regions (hook method)
            var targetRegions = CalculateTargetRegions(screens);

            // Step 5: Platform-specific window creation (hook method)
            await CreateAndConfigureWindowsAsync(session, screens, targetRegions);

            // Step 6: Show all windows
            session.ShowAll();

            // Step 7: Store current session
            _currentSession = session;
            
            LogSessionCreated(session.Windows.Count);
            return session;
        }
        catch (Exception ex)
        {
            LogSessionError(ex);
            CloseCurrentSession();
            throw;
        }
    }

    /// <summary>
    /// Close the current session if active
    /// Unsubscribes from events and disposes the session
    /// </summary>
    public virtual void CloseCurrentSession()
    {
        if (_currentSession == null)
            return;

        try
        {
            Log.Information("[{Platform}] Closing current session", PlatformName);

            // Unsubscribe from all windows
            foreach (var window in _currentSession.Windows)
            {
                if (window is IOverlayWindow overlayWindow)
                {
                    overlayWindow.RegionSelected -= OnRegionSelected;
                    overlayWindow.Cancelled -= OnCancelled;
                }
            }

            // Dispose session (will close all windows automatically)
            _currentSession.Dispose();
            _currentSession = null;

            Log.Debug("[{Platform}] Session closed", PlatformName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Platform}] Error closing session", PlatformName);
            _currentSession = null;
        }
    }

    /// <summary>
    /// Get screen information for current session
    /// Handles screen hot-plug scenarios by fetching latest configuration
    /// Uses temporary window approach (cross-platform)
    /// </summary>
    /// <returns>List of available screens</returns>
    protected async Task<IReadOnlyList<Screen>> GetScreensAsync()
    {
        return await GetScreensUsingTempWindowAsync();
    }

    /// <summary>
    /// Hook method: Calculate target regions for overlay windows
    /// Different strategies: single virtual desktop vs per-screen regions
    /// </summary>
    /// <param name="screens">Available screens</param>
    /// <returns>Target regions for overlay windows</returns>
    protected abstract IEnumerable<Rect> CalculateTargetRegions(IReadOnlyList<Screen> screens);

    /// <summary>
    /// Hook method: Platform-specific window creation and configuration
    /// Subclasses implement their specific logic (single window vs multi-window)
    /// </summary>
    /// <param name="session">The session to add windows to</param>
    /// <param name="screens">Available screens</param>
    /// <param name="targetRegions">Target regions for windows</param>
    protected abstract Task CreateAndConfigureWindowsAsync(
        IOverlaySession session, 
        IReadOnlyList<Screen> screens,
        IEnumerable<Rect> targetRegions);

    /// <summary>
    /// Hook method: Get platform name for logging
    /// </summary>
    protected abstract string PlatformName { get; }

    // Logging hooks with default implementations
    protected virtual void LogSessionStart()
    {
        Log.Information("[{Platform}] Creating new screenshot session", PlatformName);
    }

    protected virtual void LogScreensInitialized(int screenCount)
    {
        Log.Debug("[{Platform}] Initialized {Count} screen(s) for session", PlatformName, screenCount);
    }

    protected virtual void LogSessionCreated(int windowCount)
    {
        Log.Information("[{Platform}] Session created with {Count} window(s)", PlatformName, windowCount);
    }

    protected virtual void LogSessionError(Exception ex)
    {
        Log.Error(ex, "[{Platform}] Failed to create session", PlatformName);
    }

    // Static helper methods

    /// <summary>
    /// Get screens using a temporary window (cross-platform approach)
    /// </summary>
    protected static async Task<IReadOnlyList<Screen>> GetScreensUsingTempWindowAsync()
    {
        try
        {
            // Create minimal temporary window to access screen information
            var tempWindow = new Avalonia.Controls.Window
            {
                Width = 1,
                Height = 1,
                ShowInTaskbar = false,
                WindowState = Avalonia.Controls.WindowState.Minimized,
                SystemDecorations = Avalonia.Controls.SystemDecorations.None,
                Opacity = 0
            };

            tempWindow.Show();
            var screens = tempWindow.Screens?.All?.ToList() ?? (IReadOnlyList<Screen>)Array.Empty<Screen>();
            tempWindow.Close();
            
            return await Task.FromResult(screens);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to get screens using temporary window");
            return Array.Empty<Screen>();
        }
    }

    /// <summary>
    /// Calculate virtual desktop bounds (bounding box of all screens)
    /// </summary>
    protected static Rect CalculateVirtualDesktopBounds(IReadOnlyList<Screen> screens)
    {
        if (screens.Count == 0)
        {
            // Fallback to default
            return new Rect(0, 0, 1920, 1080);
        }

        // Calculate bounding box of all screens
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        foreach (var screen in screens)
        {
            var bounds = screen.Bounds;
            minX = Math.Min(minX, bounds.X);
            minY = Math.Min(minY, bounds.Y);
            maxX = Math.Max(maxX, bounds.X + bounds.Width);
            maxY = Math.Max(maxY, bounds.Y + bounds.Height);
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    // Event handling methods (shared by all platforms)

    /// <summary>
    /// Handle region selection event from overlay window
    /// </summary>
    protected async void OnRegionSelected(object? sender, RegionSelectedEventArgs e)
    {
        try
        {
            // If this is an editable selection (for annotation), don't close the session
            if (e.IsEditableSelection)
            {
                Log.Debug("[{Platform}] Editable selection created, keeping session open for annotation", PlatformName);
                return;
            }

            // Only process final image (from double-click or export)
            if (e.FinalImage != null)
            {
                Log.Information("[{Platform}] Region selected: {Region}, copying to clipboard", PlatformName, e.SelectedRegion);
                await CopyImageToClipboardAsync(e.FinalImage);
            }

            // Close session after final image processing
            CloseCurrentSession();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Platform}] Error handling region selection", PlatformName);
        }
    }

    /// <summary>
    /// Handle cancellation event from overlay window
    /// </summary>
    protected void OnCancelled(object? sender, OverlayCancelledEventArgs e)
    {
        Log.Information("[{Platform}] Screenshot cancelled", PlatformName);
        CloseCurrentSession();
    }

    /// <summary>
    /// Copy image to clipboard
    /// </summary>
    protected async Task CopyImageToClipboardAsync(Bitmap image)
    {
        var skBitmap = BitmapConverter.ConvertToSKBitmap(image);
        if (skBitmap != null)
        {
            if (_clipboardStrategy != null)
            {
                var success = await _clipboardStrategy.SetImageAsync(skBitmap);
                if (success)
                {
                    Log.Information("[{Platform}] Image copied to clipboard successfully", PlatformName);
                }
                else
                {
                    Log.Warning("[{Platform}] Failed to copy image to clipboard", PlatformName);
                }
            }
            else
            {
                Log.Warning("[{Platform}] Clipboard strategy not available", PlatformName);
            }

            skBitmap.Dispose();
        }
        else
        {
            Log.Warning("[{Platform}] Failed to convert image for clipboard", PlatformName);
        }
    }

    /// <summary>
    /// Pre-capture background for a region
    /// </summary>
    protected async Task<Bitmap?> PrecaptureBackgroundAsync(Rect bounds, Screen screen)
    {
        if (_captureStrategy == null)
        {
            Log.Warning("[{Platform}] No capture strategy available for pre-capture", PlatformName);
            return null;
        }

        try
        {
            var physicalBounds = _coordinateMapper.MapToPhysicalRect(bounds, screen);
            Log.Debug("[{Platform}] Pre-capturing region: {PhysicalBounds}", PlatformName, physicalBounds);

            var skBitmap = await _captureStrategy.CaptureRegionAsync(physicalBounds);
            if (skBitmap == null)
            {
                Log.Warning("[{Platform}] Screen capture returned null", PlatformName);
                return null;
            }

            return BitmapConverter.ConvertToAvaloniaBitmap(skBitmap);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[{Platform}] Failed to pre-capture background", PlatformName);
            return null;
        }
    }
}

