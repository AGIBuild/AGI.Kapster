using System;
using Xunit;
using FluentAssertions;
using AGI.Kapster.Desktop.Models.Update;

namespace AGI.Kapster.Tests.Models.Update;

/// <summary>
/// Tests for Update-related models
/// </summary>
public class UpdateModelsTests
{
    [Fact]
    public void UpdateInfo_IsNewerThan_WithNewerVersion_ShouldReturnTrue()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = "1.3.0",
            Name = "Test Update",
            Description = "Test Description",
            PublishedAt = DateTime.UtcNow,
            DownloadUrl = "https://example.com/update.msi",
            FileSize = 1024,
            ReleaseUrl = "https://example.com/release"
        };
        var currentVersion = new System.Version("1.2.0");

        // Act
        var result = updateInfo.IsNewerThan(currentVersion);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void UpdateInfo_IsNewerThan_WithOlderVersion_ShouldReturnFalse()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = "1.1.0",
            Name = "Test Update",
            Description = "Test Description",
            PublishedAt = DateTime.UtcNow,
            DownloadUrl = "https://example.com/update.msi",
            FileSize = 1024,
            ReleaseUrl = "https://example.com/release"
        };
        var currentVersion = new System.Version("1.2.0");

        // Act
        var result = updateInfo.IsNewerThan(currentVersion);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void UpdateInfo_IsNewerThan_WithSameVersion_ShouldReturnFalse()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = "1.2.0",
            Name = "Test Update",
            Description = "Test Description",
            PublishedAt = DateTime.UtcNow,
            DownloadUrl = "https://example.com/update.msi",
            FileSize = 1024,
            ReleaseUrl = "https://example.com/release"
        };
        var currentVersion = new System.Version("1.2.0");

        // Act
        var result = updateInfo.IsNewerThan(currentVersion);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void UpdateInfo_IsNewerThan_WithInvalidVersion_ShouldReturnFalse()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = "invalid-version",
            Name = "Test Update",
            Description = "Test Description",
            PublishedAt = DateTime.UtcNow,
            DownloadUrl = "https://example.com/update.msi",
            FileSize = 1024,
            ReleaseUrl = "https://example.com/release"
        };
        var currentVersion = new System.Version("1.2.0");

        // Act
        var result = updateInfo.IsNewerThan(currentVersion);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("1.3.0", "1.2.0", true)]
    [InlineData("1.2.1", "1.2.0", true)]
    [InlineData("2.0.0", "1.9.9", true)]
    [InlineData("1.2.0", "1.2.0", false)]
    [InlineData("1.1.9", "1.2.0", false)]
    [InlineData("0.9.0", "1.0.0", false)]
    public void UpdateInfo_IsNewerThan_VariousVersions_ShouldReturnExpectedResult(
        string updateVersion, string currentVersion, bool expected)
    {
        // Arrange
        var updateInfo = new UpdateInfo { Version = updateVersion };
        var current = new System.Version(currentVersion);

        // Act
        var result = updateInfo.IsNewerThan(current);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void UpdateSettings_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var settings = new UpdateSettings();

        // Assert
        settings.Enabled.Should().BeTrue();
        settings.NotifyBeforeInstall.Should().BeFalse();
        settings.UsePreReleases.Should().BeFalse();
        settings.LastCheckTime.Should().Be(DateTime.MinValue);
        settings.RepositoryOwner.Should().BeNull();
        settings.RepositoryName.Should().BeNull();
    }

    [Fact]
    public void DownloadProgress_PercentComplete_ShouldCalculateCorrectly()
    {
        // Arrange
        var progress = new DownloadProgress
        {
            TotalBytes = 1000,
            DownloadedBytes = 250
        };

        // Act
        var percent = progress.PercentComplete;

        // Assert
        percent.Should().Be(25);
    }

    [Fact]
    public void DownloadProgress_PercentComplete_WithZeroTotal_ShouldReturnZero()
    {
        // Arrange
        var progress = new DownloadProgress
        {
            TotalBytes = 0,
            DownloadedBytes = 100
        };

        // Act
        var percent = progress.PercentComplete;

        // Assert
        percent.Should().Be(0);
    }

    [Fact]
    public void DownloadProgress_PercentComplete_WithCompleteDownload_ShouldReturn100()
    {
        // Arrange
        var progress = new DownloadProgress
        {
            TotalBytes = 1000,
            DownloadedBytes = 1000
        };

        // Act
        var percent = progress.PercentComplete;

        // Assert
        percent.Should().Be(100);
    }

    [Fact]
    public void UpdateAvailableEventArgs_Constructor_ShouldSetProperties()
    {
        // Arrange
        var updateInfo = new UpdateInfo { Version = "1.3.0" };
        var isAutomatic = true;

        // Act
        var eventArgs = new UpdateAvailableEventArgs(updateInfo, isAutomatic);

        // Assert
        eventArgs.UpdateInfo.Should().Be(updateInfo);
        eventArgs.IsAutomatic.Should().Be(isAutomatic);
    }

    [Fact]
    public void UpdateCompletedEventArgs_Constructor_ShouldSetProperties()
    {
        // Arrange
        var success = true;
        var updateInfo = new UpdateInfo { Version = "1.3.0" };
        var errorMessage = "Test error";

        // Act
        var eventArgs = new UpdateCompletedEventArgs(success, updateInfo, errorMessage);

        // Assert
        eventArgs.Success.Should().Be(success);
        eventArgs.UpdateInfo.Should().Be(updateInfo);
        eventArgs.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public void UpdateCompletedEventArgs_SuccessConstructor_ShouldSetSuccessTrue()
    {
        // Act
        var eventArgs = new UpdateCompletedEventArgs(true);

        // Assert
        eventArgs.Success.Should().BeTrue();
        eventArgs.UpdateInfo.Should().BeNull();
        eventArgs.ErrorMessage.Should().BeNull();
    }
}