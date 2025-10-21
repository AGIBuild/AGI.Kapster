using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Services.Overlay.State;
using Avalonia;
using Avalonia.Platform;
using Serilog;

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
    
    protected IOverlaySession? _currentSession;

    protected OverlayCoordinatorBase(
        IOverlaySessionFactory sessionFactory,
        IOverlayWindowFactory windowFactory,
        IScreenCoordinateMapper coordinateMapper)
    {
        _sessionFactory = sessionFactory;
        _windowFactory = windowFactory;
        _coordinateMapper = coordinateMapper;
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
    /// </summary>
    public abstract void CloseCurrentSession();

    /// <summary>
    /// Hook method: Get screen information for current session
    /// Handles screen hot-plug scenarios by fetching latest configuration
    /// </summary>
    /// <returns>List of available screens</returns>
    protected abstract Task<IReadOnlyList<Screen>> GetScreensAsync();

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
}

