namespace AGI.Kapster.Desktop.Services.Platforms;

/// <summary>
/// Platform-specific application appearance adjustments applied at startup.
/// </summary>
public interface IAppAppearanceService
{
    /// <summary>
    /// Apply platform appearance customizations early in startup.
    /// </summary>
    void ApplyOnAppStartup();
}


