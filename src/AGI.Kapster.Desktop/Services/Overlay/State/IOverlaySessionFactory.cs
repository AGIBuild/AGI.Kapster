namespace AGI.Kapster.Desktop.Services.Overlay.State;

/// <summary>
/// Factory for creating overlay sessions
/// </summary>
public interface IOverlaySessionFactory
{
    /// <summary>
    /// Create a new overlay session for a screenshot operation
    /// </summary>
    IOverlaySession CreateSession();
}

