using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using AGI.Captor.Desktop.Models.Update;
using AGI.Captor.Desktop.Services.Update.Platforms;
using Serilog;

namespace AGI.Captor.Desktop.Services.Update;

/// <summary>
/// GitHub releases update provider
/// </summary>
public class GitHubUpdateProvider
{
    private const string DefaultGitHubRepository = "AGIBuild/AGI.Captor";
    private const string UserAgent = "AGI.Captor-UpdateChecker/1.0";

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger = Log.ForContext<GitHubUpdateProvider>();
    private readonly string _gitHubApiUrl;

    public GitHubUpdateProvider(string? gitHubRepository = null)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

        // Build API URL from repository setting
        _gitHubApiUrl = BuildGitHubApiUrl(gitHubRepository);
        _logger.Debug("GitHub update provider initialized with URL: {ApiUrl}", _gitHubApiUrl);
    }

    /// <summary>
    /// Validates and constructs the GitHub API URL from the repository setting.
    /// </summary>
    /// <param name="gitHubRepository">The repository string or full API URL.</param>
    /// <returns>The GitHub API URL for releases.</returns>
    private static string BuildGitHubApiUrl(string? gitHubRepository)
    {
        var repository = !string.IsNullOrWhiteSpace(gitHubRepository) ? gitHubRepository : DefaultGitHubRepository;

        // Check if it's already a full API URL or just owner/repo format
        if (repository.StartsWith("https://api.github.com/"))
        {
            return repository;
        }
        else
        {
            // Assume it's owner/repo format
            return $"https://api.github.com/repos/{repository}/releases";
        }
    }

    /// <summary>
    /// Get latest release information from GitHub
    /// </summary>
    /// <param name="includePreReleases">Include pre-release versions</param>
    /// <returns>Latest release info or null if not found</returns>
    public async Task<UpdateInfo?> GetLatestReleaseAsync(bool includePreReleases = false)
    {
        try
        {
            _logger.Debug("Checking for updates from GitHub API");

            var response = await _httpClient.GetAsync(_gitHubApiUrl);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning("Failed to fetch releases from GitHub: {StatusCode}", response.StatusCode);
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var releases = JsonSerializer.Deserialize<GitHubRelease[]>(jsonContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            if (releases == null || releases.Length == 0)
            {
                _logger.Information("No releases found on GitHub");
                return null;
            }

            // Find the latest suitable release
            var latestRelease = releases
                .Where(r => includePreReleases || !r.Prerelease)
                .Where(r => !r.Draft)
                .OrderByDescending(r => r.PublishedAt)
                .FirstOrDefault();

            if (latestRelease == null)
            {
                _logger.Information("No suitable releases found");
                return null;
            }

            // Find appropriate asset for current platform
            var platformInfo = PlatformUpdateHelper.GetPlatformInfo();
            var asset = latestRelease.Assets
                .FirstOrDefault(a => a.Name.EndsWith($".{platformInfo.Extension}", StringComparison.OrdinalIgnoreCase) &&
                                   a.Name.Contains(platformInfo.Identifier, StringComparison.OrdinalIgnoreCase));

            if (asset == null)
            {
                _logger.Warning("No {Extension} installer found for platform {Platform} in release {Version}",
                    platformInfo.Extension, platformInfo.Identifier, latestRelease.TagName);
                return null;
            }

            var updateInfo = new UpdateInfo
            {
                Version = latestRelease.TagName.TrimStart('v'),
                Name = latestRelease.Name ?? latestRelease.TagName,
                Description = latestRelease.Body ?? "No release notes available",
                PublishedAt = latestRelease.PublishedAt,
                IsPreRelease = latestRelease.Prerelease,
                DownloadUrl = asset.BrowserDownloadUrl,
                FileSize = asset.Size,
                ReleaseUrl = latestRelease.HtmlUrl
            };

            _logger.Information("Found release: {Version} published at {PublishedAt}",
                updateInfo.Version, updateInfo.PublishedAt);

            return updateInfo;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking for updates from GitHub");
            return null;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// GitHub release JSON model
/// </summary>
internal class GitHubRelease
{
    public string TagName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public bool Prerelease { get; set; }
    public bool Draft { get; set; }
    public string HtmlUrl { get; set; } = string.Empty;
    public GitHubAsset[] Assets { get; set; } = Array.Empty<GitHubAsset>();
}

/// <summary>
/// GitHub asset JSON model
/// </summary>
internal class GitHubAsset
{
    public string Name { get; set; } = string.Empty;
    public string BrowserDownloadUrl { get; set; } = string.Empty;
    public long Size { get; set; }
    public string ContentType { get; set; } = string.Empty;
}