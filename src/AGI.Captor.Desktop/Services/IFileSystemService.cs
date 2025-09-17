using System.Threading.Tasks;

namespace AGI.Captor.Desktop.Services;

/// <summary>
/// File system service interface for dependency injection and testing
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// Check if a file exists
    /// </summary>
    bool FileExists(string path);
    
    /// <summary>
    /// Read all text from a file asynchronously
    /// </summary>
    Task<string> ReadAllTextAsync(string path);
    
    /// <summary>
    /// Write all text to a file asynchronously
    /// </summary>
    Task WriteAllTextAsync(string path, string content);
    
    /// <summary>
    /// Read all text from a file synchronously
    /// </summary>
    string ReadAllText(string path);
    
    /// <summary>
    /// Ensure directory exists
    /// </summary>
    void EnsureDirectoryExists(string path);
    
    /// <summary>
    /// Get application data directory path
    /// </summary>
    string GetApplicationDataPath();
}
