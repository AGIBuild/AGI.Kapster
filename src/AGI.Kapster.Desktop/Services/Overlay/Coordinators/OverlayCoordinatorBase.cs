using System;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Services.Overlay.State;
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

            // Step 3: Initialize screen information for this session
            _coordinateMapper.InitializeScreens();
            LogScreensInitialized(_coordinateMapper.Screens.Count);

            // Step 4: Platform-specific window creation (hook method)
            await CreateAndConfigureWindowsAsync(session);

            // Step 5: Show all windows
            session.ShowAll();

            // Step 6: Store current session
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
    /// Hook method: Platform-specific window creation and configuration
    /// Subclasses implement their specific logic (single window vs multi-window)
    /// </summary>
    /// <param name="session">The session to add windows to</param>
    protected abstract Task CreateAndConfigureWindowsAsync(IOverlaySession session);

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
}

