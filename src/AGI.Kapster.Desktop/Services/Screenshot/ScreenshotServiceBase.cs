using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Overlays;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.Clipboard;
using AGI.Kapster.Desktop.Services.Export.Imaging;
using AGI.Kapster.Desktop.Services.Overlay;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using AGI.Kapster.Desktop.Services.Overlay.State;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Serilog;
using SkiaSharp;

namespace AGI.Kapster.Desktop.Services.Screenshot;

/// <summary>
/// Abstract base class for screenshot services using Template Method pattern
/// Defines common session lifecycle while allowing platform-specific window creation
/// </summary>
public abstract class ScreenshotServiceBase : IScreenshotService
{
    protected readonly IScreenMonitorService _screenMonitor;
    protected readonly IOverlaySessionFactory _sessionFactory;
    protected readonly IScreenCoordinateMapper _coordinateMapper;
    protected readonly IScreenCaptureStrategy? _captureStrategy;
    protected readonly IClipboardStrategy? _clipboardStrategy;
    
    protected IOverlaySession? _currentSession;
    
    // Synchronization: Prevent concurrent session creation
    private readonly SemaphoreSlim _sessionLock = new SemaphoreSlim(1, 1);
    
    // Track if we're in the middle of an async cleanup operation
    private volatile bool _isDisposing = false;

    protected ScreenshotServiceBase(
        IScreenMonitorService screenMonitor,
        IOverlaySessionFactory sessionFactory,
        IScreenCoordinateMapper coordinateMapper,
        IScreenCaptureStrategy? captureStrategy,
        IClipboardStrategy? clipboardStrategy)
    {
        _screenMonitor = screenMonitor ?? throw new ArgumentNullException(nameof(screenMonitor));
        _sessionFactory = sessionFactory;
        _coordinateMapper = coordinateMapper;
        _captureStrategy = captureStrategy;
        _clipboardStrategy = clipboardStrategy;
    }

    public bool IsActive => _currentSession != null && !_isDisposing;

    /// <summary>
    /// Template method: defines the overall screenshot session creation flow
    /// Uses semaphore to prevent concurrent session creation
    /// </summary>
    public async Task TakeScreenshotAsync()
    {
        // Wait for any ongoing session cleanup to complete
        // Timeout after 5 seconds to prevent indefinite blocking
        if (!await _sessionLock.WaitAsync(TimeSpan.FromSeconds(5)))
        {
            Log.Warning("[{Platform}] Failed to acquire session lock within timeout, forcing cleanup", PlatformName);
            // Force cleanup if lock can't be acquired
            ForceCleanup();
            return;
        }

        try
        {
            LogSessionStart();
            
            // Step 1: Close any existing session (synchronous, within lock)
            Cancel();

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

            // Step 7: Subscribe to session-level events (unified for all windows)
            session.RegionSelected += OnRegionSelected;
            session.Closed += OnSessionClosed;
            
            // Step 8: Store current session
            _currentSession = session;
            
            LogSessionCreated(session.Windows.Count);
        }
        catch (Exception ex)
        {
            LogSessionError(ex);
            Cancel();
            throw;
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    /// <summary>
    /// Cancel the current screenshot operation
    /// Thread-safe and idempotent - safe to call multiple times
    /// </summary>
    public virtual void Cancel()
    {
        // Atomically check and mark as disposing
        if (_currentSession == null || _isDisposing)
            return;

        _isDisposing = true;

        try
        {
            Log.Information("[{Platform}] Cancelling screenshot session", PlatformName);

            // Capture session reference before nulling
            var sessionToDispose = _currentSession;
            
            // Null out immediately to prevent race conditions
            _currentSession = null;

            if (sessionToDispose != null)
            {
                // Unsubscribe from session-level events
                try
                {
                    sessionToDispose.RegionSelected -= OnRegionSelected;
                    sessionToDispose.Closed -= OnSessionClosed;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "[{Platform}] Error unsubscribing from session events", PlatformName);
                }

                // Dispose session (will close all windows automatically)
                try
                {
                    sessionToDispose.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[{Platform}] Error disposing session", PlatformName);
                }
            }

            Log.Debug("[{Platform}] Session cancelled and closed", PlatformName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Platform}] Error in Cancel()", PlatformName);
        }
        finally
        {
            _isDisposing = false;
        }
    }

    /// <summary>
    /// Force cleanup when normal cancellation fails (emergency recovery)
    /// </summary>
    private void ForceCleanup()
    {
        Log.Warning("[{Platform}] Force cleanup triggered", PlatformName);
        _isDisposing = false;
        _currentSession = null;
    }

    /// <summary>
    /// Get screen information for current session
    /// Returns cached screen information from ScreenMonitorService (always up-to-date)
    /// No delay - screen info is maintained in real-time through event subscription
    /// </summary>
    /// <returns>List of available screens</returns>
    protected Task<IReadOnlyList<Screen>> GetScreensAsync()
    {
        var screens = _screenMonitor.GetCurrentScreens();
        Log.Debug("Retrieved {Count} screen(s) from screen monitor", screens.Count);
        return Task.FromResult(screens);
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
    /// Uses fire-and-forget async pattern with proper exception handling
    /// </summary>
    protected async void OnRegionSelected(object? sender, RegionSelectedEventArgs e)
    {
        // Track if we need to cleanup (for finally block)
        bool shouldCleanup = false;
        
        try
        {
            // If this is an editable selection (for annotation), don't close the session
            if (e.IsEditableSelection)
            {
                Log.Debug("[{Platform}] Editable selection created, keeping session open for annotation", PlatformName);
                return;
            }

            // Mark that we should cleanup after processing
            shouldCleanup = true;

            // Only process final image (from double-click or export)
            if (e.FinalImage != null)
            {
                Log.Information("[{Platform}] Region selected: {Region}, copying to clipboard", PlatformName, e.SelectedRegion);
                
                // Add timeout to prevent indefinite blocking
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try
                {
                    await CopyImageToClipboardAsync(e.FinalImage).WaitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    Log.Warning("[{Platform}] Clipboard operation timed out after 10 seconds", PlatformName);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Platform}] Error handling region selection", PlatformName);
            // Always cleanup on error
            shouldCleanup = true;
        }
        finally
        {
            // Ensure cleanup always happens for non-editable selections
            if (shouldCleanup)
            {
                try
                {
                    Cancel();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[{Platform}] Error during cleanup in OnRegionSelected", PlatformName);
                    // Last resort: force cleanup
                    ForceCleanup();
                }
            }
        }
    }

    /// <summary>
    /// Handle cancellation event from overlay window
    /// </summary>
    protected void OnCancelled(object? sender, OverlayCancelledEventArgs e)
    {
        Log.Information("[{Platform}] Screenshot cancelled", PlatformName);
        Cancel();
    }
    
    /// <summary>
    /// Handle session closed event
    /// This is the key cleanup point that ensures _currentSession is cleared
    /// Triggered when any window closes (which closes the entire session)
    /// </summary>
    private void OnSessionClosed()
    {
        Log.Debug("[{Platform}] Session closed, cleaning up", PlatformName);
        
        // Clear _currentSession to allow new sessions
        var sessionToDispose = Interlocked.Exchange(ref _currentSession, null);
        
        if (sessionToDispose != null)
        {
            try
            {
                // Unsubscribe to prevent memory leak
                sessionToDispose.RegionSelected -= OnRegionSelected;
                sessionToDispose.Closed -= OnSessionClosed;
                
                // Dispose session
                sessionToDispose.Dispose();
                
                Log.Debug("[{Platform}] Session cleaned up successfully", PlatformName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[{Platform}] Error disposing session in OnSessionClosed", PlatformName);
            }
        }
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

            var result = BitmapConverter.ConvertToAvaloniaBitmapFast(skBitmap);
            Log.Debug("[{Platform}] Pre-capture completed: {Width}x{Height}", 
                PlatformName, skBitmap.Width, skBitmap.Height);
            
            return result;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[{Platform}] Failed to pre-capture background", PlatformName);
            return null;
        }
    }
}

