using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AGI.Kapster.Desktop.Services.Settings;

namespace AGI.Kapster.Tests.TestHelpers;

/// <summary>
/// In-memory file system service for testing
/// </summary>
public class MemoryFileSystemService : IFileSystemService, IDisposable
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();
    private readonly HashSet<string> _directories = new();
    private readonly HashSet<string> _tempFiles = new();
    private string _applicationDataPath = Path.Combine(Path.GetTempPath(), "AGI.Kapster.Test");

    public bool FileExists(string path)
    {
        return _files.ContainsKey(NormalizePath(path));
    }

    public async Task<string> ReadAllTextAsync(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (_files.TryGetValue(normalizedPath, out var content))
        {
            return await Task.FromResult(System.Text.Encoding.UTF8.GetString(content));
        }
        throw new FileNotFoundException($"File not found: {path}");
    }

    public async Task WriteAllTextAsync(string path, string content)
    {
        var normalizedPath = NormalizePath(path);
        _files[normalizedPath] = System.Text.Encoding.UTF8.GetBytes(content);
        await Task.CompletedTask;
    }

    public string ReadAllText(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (_files.TryGetValue(normalizedPath, out var content))
        {
            return System.Text.Encoding.UTF8.GetString(content);
        }
        throw new FileNotFoundException($"File not found: {path}");
    }

    public void WriteAllText(string path, string content)
    {
        var normalizedPath = NormalizePath(path);
        _files[normalizedPath] = System.Text.Encoding.UTF8.GetBytes(content);
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

    // Update service specific methods
    public void CreateDirectory(string path)
    {
        EnsureDirectoryExists(path);
    }

    public FileInfo GetFileInfo(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (_files.TryGetValue(normalizedPath, out var content))
        {
            // Create a temporary file that persists during the FileInfo's usage
            var tempFile = Path.GetTempFileName();
            File.WriteAllBytes(tempFile, content);
            
            // Store temp file for cleanup later
            _tempFiles.Add(tempFile);
            
            return new FileInfo(tempFile);
        }
        throw new FileNotFoundException($"File not found: {path}");
    }

    public void DeleteFile(string path)
    {
        var normalizedPath = NormalizePath(path);
        _files.TryRemove(normalizedPath, out _);
    }

    public Stream CreateFileStream(string path, FileMode mode, FileAccess access, FileShare share)
    {
        var normalizedPath = NormalizePath(path);
        return new MemoryFileStream(normalizedPath, this);
    }

    public Task<string> EnsureWritablePathAsync(string initialPath)
    {
        return Task.FromResult(initialPath);
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
        CleanupTempFiles();
        _files.Clear();
        _directories.Clear();
    }
    
    /// <summary>
    /// Cleanup temporary files created for FileInfo
    /// </summary>
    private void CleanupTempFiles()
    {
        foreach (var tempFile in _tempFiles)
        {
            try { File.Delete(tempFile); } catch { /* ignore cleanup errors */ }
        }
        _tempFiles.Clear();
    }
    
    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        CleanupTempFiles();
    }


    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).ToLowerInvariant();
    }

    internal void WriteFileBytes(string normalizedPath, byte[] data)
    {
        _files[normalizedPath] = data;
    }
}

/// <summary>
/// Memory stream that writes to the MemoryFileSystemService
/// </summary>
internal class MemoryFileStream : MemoryStream
{
    private readonly string _path;
    private readonly MemoryFileSystemService _fileSystem;

    public MemoryFileStream(string path, MemoryFileSystemService fileSystem)
    {
        _path = path;
        _fileSystem = fileSystem;
    }

    public override void Flush()
    {
        base.Flush();
        // Immediately update the file system when flushed
        _fileSystem.WriteFileBytes(_path, ToArray());
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await base.FlushAsync(cancellationToken);
        // Immediately update the file system when flushed
        _fileSystem.WriteFileBytes(_path, ToArray());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fileSystem.WriteFileBytes(_path, ToArray());
        }
        base.Dispose(disposing);
    }
}

