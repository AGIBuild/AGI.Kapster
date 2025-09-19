using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using NSubstitute;
using AGI.Captor.Desktop.Services.Update;
using AGI.Captor.Desktop.Services.Settings;
using AGI.Captor.Desktop.Models.Update;
using AGI.Captor.Tests.TestHelpers;

namespace AGI.Captor.Tests.Services.Update;

/// <summary>
/// Tests for UpdateService functionality
/// </summary>
public class UpdateServiceTests : IDisposable
{
    private readonly ISettingsService _mockSettingsService;
    private readonly UpdateService _updateService;

    public UpdateServiceTests()
    {
        _mockSettingsService = Substitute.For<ISettingsService>();

        // Setup default settings
        var settings = TestDataFactory.CreateDefaultAppSettings();
        _mockSettingsService.Settings.Returns(settings);

        _updateService = new UpdateService(_mockSettingsService);
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var service = new UpdateService(_mockSettingsService);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ShouldCompleteWithoutError()
    {
        // Act & Assert - We can't predict the exact result since it depends on GitHub API
        // but the method should complete without throwing
        Func<Task> act = async () => await _updateService.CheckForUpdatesAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void StartBackgroundChecking_ShouldNotThrow()
    {
        // Act & Assert
        Action act = () => _updateService.StartBackgroundChecking();
        act.Should().NotThrow();
    }

    [Fact]
    public void StopBackgroundChecking_ShouldNotThrow()
    {
        // Arrange
        _updateService.StartBackgroundChecking();

        // Act & Assert
        Action act = () => _updateService.StopBackgroundChecking();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task UpdateSettingsAsync_ShouldUpdateSettings()
    {
        // Arrange
        var newSettings = new UpdateSettings
        {
            Enabled = false,
            CheckFrequencyHours = 48,
            InstallAutomatically = false
        };

        // Act
        await _updateService.UpdateSettingsAsync(newSettings);

        // Assert
        var currentSettings = _updateService.GetSettings();
        currentSettings.Enabled.Should().Be(newSettings.Enabled);
        currentSettings.CheckFrequencyHours.Should().Be(newSettings.CheckFrequencyHours);
        currentSettings.InstallAutomatically.Should().Be(newSettings.InstallAutomatically);
    }

    [Fact]
    public void GetSettings_ShouldReturnCurrentSettings()
    {
        // Act
        var settings = _updateService.GetSettings();

        // Assert
        settings.Should().NotBeNull();
    }

    [Fact]
    public async Task DownloadUpdateAsync_WithValidUpdateInfo_ShouldReturnResult()
    {
        // Arrange
        var updateInfo = TestDataFactory.CreateUpdateInfo("1.3.0");

        // Act & Assert - Method should complete without throwing
        Func<Task> act = async () => await _updateService.DownloadUpdateAsync(updateInfo);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InstallUpdateAsync_WithValidPath_ShouldReturnResult()
    {
        // Arrange
        var testPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(testPath, "test content");

            // Act & Assert - Method should complete without throwing
            Func<Task> act = async () => await _updateService.InstallUpdateAsync(testPath);
            await act.Should().NotThrowAsync();
        }
        finally
        {
            if (File.Exists(testPath))
                File.Delete(testPath);
        }
    }

    [Fact]
    public void IsAutoUpdateEnabled_ShouldRespectDebugMode()
    {
        // Arrange
        var settings = TestDataFactory.CreateDefaultAppSettings();
        settings.AutoUpdate!.Enabled = true;
        _mockSettingsService.Settings.Returns(settings);

        var service = new UpdateService(_mockSettingsService);

        // Act & Assert
#if DEBUG
        // In debug mode, auto-update should be disabled regardless of settings
        service.IsAutoUpdateEnabled.Should().BeFalse();
#else
        // In release mode, auto-update should follow settings
        service.IsAutoUpdateEnabled.Should().BeTrue();
#endif
    }

    [Fact]
    public void IsAutoUpdateEnabled_ShouldRespectSettingsWhenNotDebugMode()
    {
        // Arrange
        var settings = TestDataFactory.CreateDefaultAppSettings();
        settings.AutoUpdate!.Enabled = false;
        _mockSettingsService.Settings.Returns(settings);

        var service = new UpdateService(_mockSettingsService);

        // Act & Assert
        // When auto-update is disabled in settings, it should always be false
        service.IsAutoUpdateEnabled.Should().BeFalse();
    }

    public void Dispose()
    {
        _updateService?.Dispose();
    }
}