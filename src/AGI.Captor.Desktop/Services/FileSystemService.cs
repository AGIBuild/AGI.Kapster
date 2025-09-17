using System;
using System.IO;
using System.Threading.Tasks;

namespace AGI.Captor.Desktop.Services;

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
        await File.WriteAllTextAsync(path, content);
    }
    
    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }
    
    public void EnsureDirectoryExists(string path)
    {
        Directory.CreateDirectory(path);
    }
    
    public string GetApplicationDataPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "AGI.Captor");
    }
}
