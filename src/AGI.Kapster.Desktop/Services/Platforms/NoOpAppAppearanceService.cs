namespace AGI.Kapster.Desktop.Services.Platforms;

/// <summary>
/// Default no-op implementation for non-macOS platforms.
/// </summary>
public class NoOpAppAppearanceService : IAppAppearanceService
{
    public void ApplyOnAppStartup()
    {
        // Intentionally no-op
    }
}


