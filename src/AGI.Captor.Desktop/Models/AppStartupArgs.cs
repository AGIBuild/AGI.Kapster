namespace AGI.Captor.Desktop.Models;

/// <summary>
/// Application startup arguments
/// </summary>
public class AppStartupArgs
{
    /// <summary>
    /// Whether the application should start minimized
    /// </summary>
    public bool StartMinimized { get; set; }
    
    /// <summary>
    /// Whether this is an automatic startup (e.g., from Windows startup)
    /// </summary>
    public bool IsAutoStart { get; set; }
    
    /// <summary>
    /// Original command line arguments
    /// </summary>
    public string[] Args { get; set; } = System.Array.Empty<string>();
}
