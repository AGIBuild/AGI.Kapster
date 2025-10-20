using System.IO;
using System.Threading.Tasks;

namespace AGI.Kapster.Desktop.Services.Settings;

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
    /// Write all text to a file synchronously
    /// </summary>
    void WriteAllText(string path, string content);

    /// <summary>
    /// Ensure directory exists
    /// </summary>
    void EnsureDirectoryExists(string path);

    /// <summary>
    /// Get application data directory path
    /// </summary>
    string GetApplicationDataPath();

    // Update service specific methods
    /// <summary>
    /// Creates a directory if it doesn't exist
    /// </summary>
    void CreateDirectory(string path);

    /// <summary>
    /// Gets file information
    /// </summary>
    FileInfo GetFileInfo(string path);

    /// <summary>
    /// Deletes a file
    /// </summary>
    void DeleteFile(string path);

    /// <summary>
    /// Creates a file stream for writing
    /// </summary>
    Stream CreateFileStream(string path, FileMode mode, FileAccess access, FileShare share);

    /// <summary>
    /// Ensures a file path is writable by retrying with unique names if needed
    /// </summary>
    Task<string> EnsureWritablePathAsync(string initialPath);
}
