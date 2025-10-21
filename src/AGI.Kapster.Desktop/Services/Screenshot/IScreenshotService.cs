using System.Threading.Tasks;

namespace AGI.Kapster.Desktop.Services.Screenshot;

/// <summary>
/// High-level screenshot service for the application
/// Provides a simple API for taking screenshots
/// </summary>
public interface IScreenshotService
{
    /// <summary>
    /// Start a new screenshot operation
    /// </summary>
    Task TakeScreenshotAsync();

    /// <summary>
    /// Check if a screenshot operation is currently active
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Cancel the current screenshot operation
    /// </summary>
    void Cancel();
}

