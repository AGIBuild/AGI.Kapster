using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using AGI.Captor.Desktop.Services.Update;

namespace AGI.Captor.Tests.Services.Update;

/// <summary>
/// Tests for GitHubUpdateProvider functionality
/// </summary>
public class GitHubUpdateProviderTests : IDisposable
{
    private readonly GitHubUpdateProvider _provider;

    public GitHubUpdateProviderTests()
    {
        _provider = new GitHubUpdateProvider();
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Act
        var provider = new GitHubUpdateProvider();

        // Assert
        provider.Should().NotBeNull();
    }

    [Fact]
    public async Task GetLatestReleaseAsync_ShouldCompleteWithoutError()
    {
        // Act & Assert - Method should complete without throwing
        Func<Task> act = async () => await _provider.GetLatestReleaseAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetLatestReleaseAsync_WithPreReleases_ShouldCompleteWithoutError()
    {
        // Act & Assert - Method should complete without throwing
        Func<Task> act = async () => await _provider.GetLatestReleaseAsync(includePreReleases: true);
        await act.Should().NotThrowAsync();
    }

    public void Dispose()
    {
        _provider?.Dispose();
    }
}