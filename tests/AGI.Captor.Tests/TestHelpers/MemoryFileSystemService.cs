using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AGI.Captor.Desktop.Services.Settings;

namespace AGI.Captor.Tests.TestHelpers;

/// <summary>
/// In-memory file system service for testing
/// </summary>
public class MemoryFileSystemService : IFileSystemService
{
    private readonly ConcurrentDictionary<string, string> _files = new();
    private readonly HashSet<string> _directories = new();
    private string _applicationDataPath = Path.Combine(Path.GetTempPath(), "AGI.Captor.Test");

    public bool FileExists(string path)
    {
        return _files.ContainsKey(NormalizePath(path));
    }
    
    public async Task<string> ReadAllTextAsync(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (_files.TryGetValue(normalizedPath, out var content))
        {
            return await Task.FromResult(content);
        }
        throw new FileNotFoundException($"File not found: {path}");
    }
    
    public async Task WriteAllTextAsync(string path, string content)
    {
        var normalizedPath = NormalizePath(path);
        _files[normalizedPath] = content;
        await Task.CompletedTask;
    }
    
    public string ReadAllText(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (_files.TryGetValue(normalizedPath, out var content))
        {
            return content;
        }
        throw new FileNotFoundException($"File not found: {path}");
    }
    
    public void EnsureDirectoryExists(string path)
    {
        var normalizedPath = NormalizePath(path);
        _directories.Add(normalizedPath);
    }
    
    public string GetApplicationDataPath()
    {
        return _applicationDataPath;
    }
    
    /// <summary>
    /// Set custom application data path for testing
    /// </summary>
    public void SetApplicationDataPath(string path)
    {
        _applicationDataPath = path;
    }
    
    /// <summary>
    /// Clear all files and directories (useful for test cleanup)
    /// </summary>
    public void Clear()
    {
        _files.Clear();
        _directories.Clear();
    }
    
    /// <summary>
    /// Get all file paths for debugging
    /// </summary>
    public IEnumerable<string> GetAllFilePaths()
    {
        return _files.Keys;
    }
    
    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).ToLowerInvariant();
    }
}
