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

    private string ReadAllTextWithRetry(string path, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                // Use FileShare.ReadWrite to allow concurrent access
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (IOException ex) when (IsSharingViolation(ex) && i < maxRetries - 1)
            {
                System.Threading.Thread.Sleep(100 * (i + 1));
            }
        }
        // Final attempt without retry
        using var finalStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var finalReader = new StreamReader(finalStream);
        return finalReader.ReadToEnd();
    }

    private async Task WriteAllTextWithRetryAsync(string path, string content, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Use FileShare.Read to allow reading while writing
                await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(content);
                await writer.FlushAsync();
                return;
            }
            catch (IOException ex) when (IsSharingViolation(ex) && i < maxRetries - 1)
            {
                await Task.Delay(100 * (i + 1));
            }
            catch (UnauthorizedAccessException ex) when (i < maxRetries - 1)
            {
                // Handle permission issues - wait and retry
                await Task.Delay(200 * (i + 1));
            }
        }
        // Final attempt without retry - let exception propagate
        await using var finalStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var finalWriter = new StreamWriter(finalStream);
        await finalWriter.WriteAsync(content);
        await finalWriter.FlushAsync();
    }
}
