using System;
using System.IO;
using System.Threading.Tasks;

namespace AGI.Kapster.Desktop.Services.Settings;

/// <summary>
/// Real file system service implementation
/// </summary>
public class FileSystemService : IFileSystemService
{
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public async Task<string> ReadAllTextAsync(string path)
    {
        return await File.ReadAllTextAsync(path);
    }

    public async Task WriteAllTextAsync(string path, string content)
    {
        await WriteAllTextWithRetryAsync(path, content);
    }

    public string ReadAllText(string path)
    {
        return ReadAllTextWithRetry(path);
    }

    public void WriteAllText(string path, string content)
    {
        WriteAllTextWithRetry(path, content);
    }

    public void EnsureDirectoryExists(string path)
    {
        Directory.CreateDirectory(path);
    }

    public string GetApplicationDataPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "AGI.Kapster");
    }

    // Update service specific methods
    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public FileInfo GetFileInfo(string path)
    {
        return new FileInfo(path);
    }

    public void DeleteFile(string path)
    {
        File.Delete(path);
    }

    public Stream CreateFileStream(string path, FileMode mode, FileAccess access, FileShare share)
    {
        return new FileStream(path, mode, access, share);
    }

    public async Task<string> EnsureWritablePathAsync(string initialPath)
    {
        var directory = Path.GetDirectoryName(initialPath)!;
        var baseName = Path.GetFileNameWithoutExtension(initialPath);
        var extension = Path.GetExtension(initialPath);
        var candidate = initialPath;
        var attempt = 0;

        while (true)
        {
            try
            {
                if (File.Exists(candidate))
                {
                    using (File.Open(candidate, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                    {
                        File.Delete(candidate);
                    }
                }

                using (File.Open(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                }

                File.Delete(candidate);
                return candidate;
            }
            catch (IOException ioEx) when (IsSharingViolation(ioEx) || File.Exists(candidate))
            {
                attempt++;
                var suffix = attempt switch
                {
                    0 => string.Empty,
                    1 => DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"),
                    _ => Guid.NewGuid().ToString("N")
                };

                if (!string.IsNullOrEmpty(suffix))
                {
                    candidate = Path.Combine(directory, $"{baseName}-{suffix}{extension}");
                }

                await Task.Delay(Math.Min(1500, attempt * 250)).ConfigureAwait(false);
            }
        }
    }

    private static bool IsSharingViolation(IOException ex)
        => ex.HResult == unchecked((int)0x80070020);

    private string ReadAllTextWithRetry(string path)
    {
        // Use FileShare.ReadWrite to allow concurrent access
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void WriteAllTextWithRetry(string path, string content)
    {
        // Ensure directory exists
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Direct write with FileShare.Read - allows concurrent reads, exclusive write
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream);
        writer.Write(content);
        writer.Flush();
    }

    private async Task WriteAllTextWithRetryAsync(string path, string content)
    {
        // Ensure directory exists
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Direct write with FileShare.Read - allows concurrent reads, exclusive write
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content);
        await writer.FlushAsync();
    }
}
