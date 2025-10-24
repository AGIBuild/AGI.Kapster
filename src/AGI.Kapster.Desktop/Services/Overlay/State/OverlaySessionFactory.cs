using System;
using AGI.Kapster.Desktop.Services.Capture;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using Microsoft.Extensions.DependencyInjection;

namespace AGI.Kapster.Desktop.Services.Overlay.State;

/// <summary>
/// Default implementation of overlay session factory
/// Resolves capture dependencies and passes them to session
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
        // Resolve capture dependencies from DI container
        var captureStrategy = _serviceProvider.GetRequiredService<IScreenCaptureStrategy>();
        var coordinateMapper = _serviceProvider.GetRequiredService<IScreenCoordinateMapper>();
        
        return new OverlaySession(_serviceProvider, captureStrategy, coordinateMapper);
    }
}

