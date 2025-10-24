using System;

namespace AGI.Kapster.Desktop.Services.Overlay.State;

/// <summary>
/// Default implementation of overlay session factory
/// </summary>
public class OverlaySessionFactory : IOverlaySessionFactory
{
    private readonly IServiceProvider _serviceProvider;

    public OverlaySessionFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IOverlaySession CreateSession()
    {
        return new OverlaySession(_serviceProvider);
    }
}

