using System;
using System.Text.Json;
using AGI.Kapster.Desktop.Models.Update;
using AGI.Kapster.Desktop.Models;

namespace AGI.Kapster.Tests.TestHelpers;

/// <summary>
/// Factory class for creating test data
/// </summary>
public static class TestDataFactory
{
    /// <summary>
    /// Creates a test UpdateInfo instance
    /// </summary>
    public static UpdateInfo CreateUpdateInfo(
        string version = "1.3.0",
        string name = "Test Update",
        string description = "Test description",
        string downloadUrl = "https://example.com/update.msi",
        long fileSize = 1024000)
    {
        return new UpdateInfo
        {
            Version = version,
            Name = name,
            Description = description,
            PublishedAt = DateTime.UtcNow,
            DownloadUrl = downloadUrl,
            FileSize = fileSize,
            ReleaseUrl = $"https://github.com/test/repo/releases/tag/v{version}"
        };
    }

    /// <summary>
    /// Creates default app settings for testing
    /// </summary>
    public static AppSettings CreateDefaultAppSettings()
    {
        return new AppSettings
        {
            AutoUpdate = new AutoUpdateSettings
            {
                Enabled = true,
                NotifyBeforeInstall = false,
                UsePreReleases = false,
                LastCheckTime = DateTime.MinValue,
                RepositoryOwner = "AGIBuild",
                RepositoryName = "AGI.Kapster"
            }
        };
    }
    /// <summary>
    /// Creates a mock GitHub releases API response
    /// </summary>
    public static string CreateGitHubReleasesResponse(
        string version = "1.3.0",
        bool prerelease = false,
        string assetName = "AGI.Kapster-1.3.0-win-x64.msi",
        string downloadUrl = "https://github.com/AGIBuild/AGI.Kapster/releases/download/v1.3.0/AGI.Kapster-1.3.0-win-x64.msi",
        long assetSize = 10485760)
    {
        var release = new
        {
            tag_name = $"v{version}",
            name = $"AGI.Kapster {version}",
            body = "Release notes for version " + version,
            published_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            prerelease = prerelease,
            html_url = $"https://github.com/AGIBuild/AGI.Kapster/releases/tag/v{version}",
            assets = new[]
            {
                new
                {
                    name = assetName,
                    browser_download_url = downloadUrl,
                    size = assetSize,
                    content_type = "application/octet-stream"
                }
            }
        };

        var releases = new[] { release };
        return JsonSerializer.Serialize(releases, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
    }

    /// <summary>
    /// Creates a mock GitHub releases response with multiple assets for different platforms
    /// </summary>
    public static string CreateMultiPlatformGitHubReleasesResponse(string version = "1.3.0")
    {
        var release = new
        {
            tag_name = $"v{version}",
            name = $"AGI.Kapster {version}",
            body = "Multi-platform release",
            published_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            prerelease = false,
            html_url = $"https://github.com/AGIBuild/AGI.Kapster/releases/tag/v{version}",
            assets = new[]
            {
                new
                {
                    name = $"AGI.Kapster-{version}-win-x64.msi",
                    browser_download_url = $"https://github.com/AGIBuild/AGI.Kapster/releases/download/v{version}/AGI.Kapster-{version}-win-x64.msi",
                    size = 10485760L,
                    content_type = "application/octet-stream"
                },
                new
                {
                    name = $"AGI.Kapster-{version}-osx-x64.pkg",
                    browser_download_url = $"https://github.com/AGIBuild/AGI.Kapster/releases/download/v{version}/AGI.Kapster-{version}-osx-x64.pkg",
                    size = 8388608L,
                    content_type = "application/octet-stream"
                },
                new
                {
                    name = $"AGI.Kapster-{version}-linux-x64.deb",
                    browser_download_url = $"https://github.com/AGIBuild/AGI.Kapster/releases/download/v{version}/AGI.Kapster-{version}-linux-x64.deb",
                    size = 9437184L,
                    content_type = "application/octet-stream"
                }
            }
        };

        var releases = new[] { release };
        return JsonSerializer.Serialize(releases, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
    }

    /// <summary>
    /// Creates an empty GitHub releases response
    /// </summary>
    public static string CreateEmptyGitHubReleasesResponse()
    {
        return JsonSerializer.Serialize(Array.Empty<object>());
    }

    /// <summary>
    /// Creates an invalid JSON response
    /// </summary>
    public static string CreateInvalidJsonResponse()
    {
        return "{ invalid json content";
    }
}