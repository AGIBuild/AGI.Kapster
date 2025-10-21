namespace AGI.Kapster.Desktop.Services.Overlay.State;

/// <summary>
/// Default implementation of overlay session factory
/// </summary>
public class OverlaySessionFactory : IOverlaySessionFactory
{
    public IOverlaySession CreateSession()
    {
        return new OverlaySession();
    }
}

