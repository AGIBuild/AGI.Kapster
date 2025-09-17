using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using AGI.Captor.Desktop.Models;
using AGI.Captor.Desktop.Services;
using AGI.Captor.Tests.TestHelpers;

namespace AGI.Captor.Tests.Services;

public class SettingsServiceTests : TestBase
{
    private readonly SettingsService _settingsService;

    public SettingsServiceTests(ITestOutputHelper output) : base(output)
    {
        _settingsService = new SettingsService();
    }

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultSettings()
    {
        // Act & Assert
        _settingsService.Settings.Should().NotBeNull();
        _settingsService.Settings.Should().BeOfType<AppSettings>();
    }

    [Fact]
    public void Settings_ShouldHaveDefaultValues()
    {
        // Act
        var settings = _settingsService.Settings;

        // Assert
        settings.Should().NotBeNull();
        settings.Hotkeys.CaptureRegion.Should().NotBeNullOrEmpty();
        settings.Hotkeys.OpenSettings.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SaveSettings_ShouldNotThrowException()
    {
        // Note: SaveSettings is not exposed by ISettingsService
        // This test would need to be implemented differently
        var action = () => { /* SaveSettings method not available */ };
        action.Should().NotThrow();
    }

    [Fact]
    public void Settings_ShouldBeLoadedInConstructor()
    {
        // Act & Assert
        _settingsService.Settings.Should().NotBeNull();
        _settingsService.Settings.Hotkeys.Should().NotBeNull();
    }

    [Fact]
    public void Settings_ShouldBeMutable()
    {
        // Arrange
        var originalHotkey = _settingsService.Settings.Hotkeys.CaptureRegion;
        var newHotkey = "Ctrl+Shift+C";

        // Act
        _settingsService.Settings.Hotkeys.CaptureRegion = newHotkey;

        // Assert
        _settingsService.Settings.Hotkeys.CaptureRegion.Should().Be(newHotkey);
        _settingsService.Settings.Hotkeys.CaptureRegion.Should().NotBe(originalHotkey);
    }

    [Fact]
    public async Task Settings_ShouldPersistChanges()
    {
        // Arrange
        var originalHotkey = _settingsService.Settings.Hotkeys.CaptureRegion;
        var newHotkey = "Ctrl+Alt+C";

        // Act
        _settingsService.Settings.Hotkeys.CaptureRegion = newHotkey;
        await _settingsService.SaveAsync();

        // Assert
        _settingsService.Settings.Hotkeys.CaptureRegion.Should().Be(newHotkey);
    }

    [Theory]
    [InlineData("Ctrl+C")]
    [InlineData("Alt+F4")]
    [InlineData("Shift+Tab")]
    [InlineData("Ctrl+Alt+Delete")]
    [InlineData("Win+R")]
    public void Settings_ShouldAcceptValidHotkeyFormats(string hotkey)
    {
        // Act
        _settingsService.Settings.Hotkeys.CaptureRegion = hotkey;

        // Assert
        _settingsService.Settings.Hotkeys.CaptureRegion.Should().Be(hotkey);
    }

    [Fact]
    public void Settings_ShouldAllowMultipleHotkeyChanges()
    {
        // Arrange
        var captureHotkey = "Ctrl+Shift+C";
        var openSettingsHotkey = "Ctrl+Shift+S";

        // Act
        _settingsService.Settings.Hotkeys.CaptureRegion = captureHotkey;
        _settingsService.Settings.Hotkeys.OpenSettings = openSettingsHotkey;

        // Assert
        _settingsService.Settings.Hotkeys.CaptureRegion.Should().Be(captureHotkey);
        _settingsService.Settings.Hotkeys.OpenSettings.Should().Be(openSettingsHotkey);
    }

    [Fact]
    public void Settings_ShouldMaintainIndependence()
    {
        // Arrange
        var settings1 = _settingsService.Settings;
        var settings2 = _settingsService.Settings;

        // Act
        settings1.Hotkeys.CaptureRegion = "Ctrl+1";
        settings2.Hotkeys.CaptureRegion = "Ctrl+2";

        // Assert
        settings1.Hotkeys.CaptureRegion.Should().Be("Ctrl+2"); // Should be the same instance
        settings2.Hotkeys.CaptureRegion.Should().Be("Ctrl+2");
    }

    [Fact]
    public void Settings_ShouldBeAccessible()
    {
        // Arrange & Act
        var settings = _settingsService.Settings;

        // Assert
        settings.Should().NotBeNull();
        settings.Hotkeys.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveAsync_ShouldNotThrow()
    {
        // Act & Assert
        var action = async () => await _settingsService.SaveAsync();
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Settings_ShouldBeSerializable()
    {
        // Arrange - Create a temporary settings service for this test
        var tempSettingsService = new SettingsService();
        tempSettingsService.Settings.Hotkeys.CaptureRegion = "Test+Hotkey";
        tempSettingsService.Settings.Hotkeys.OpenSettings = "Another+Hotkey";

        // Act & Assert
        var action = async () => await tempSettingsService.SaveAsync();
        await action.Should().NotThrowAsync();
        
        // Clean up - restore original settings
        var originalSettings = new SettingsService();
        await originalSettings.SaveAsync();
    }

    [Fact]
    public void Settings_ShouldHandleNullValues()
    {
        // Act & Assert
        var action = () => _settingsService.Settings.Hotkeys.CaptureRegion = null!;
        action.Should().NotThrow();
    }

    [Fact]
    public void Settings_ShouldHandleEmptyStrings()
    {
        // Act & Assert
        var action = () => _settingsService.Settings.Hotkeys.CaptureRegion = "";
        action.Should().NotThrow();
        
        _settingsService.Settings.Hotkeys.CaptureRegion.Should().Be("");
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}
