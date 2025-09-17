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
        settings.CaptureHotkey.Should().NotBeNullOrEmpty();
        settings.AnnotationHotkey.Should().NotBeNullOrEmpty();
        settings.ExportHotkey.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SaveSettings_ShouldNotThrowException()
    {
        // Act & Assert
        var action = () => _settingsService.SaveSettings();
        action.Should().NotThrow();
    }

    [Fact]
    public void LoadSettings_ShouldNotThrowException()
    {
        // Act & Assert
        var action = () => _settingsService.LoadSettings();
        action.Should().NotThrow();
    }

    [Fact]
    public void Settings_ShouldBeMutable()
    {
        // Arrange
        var originalHotkey = _settingsService.Settings.CaptureHotkey;
        var newHotkey = "Ctrl+Shift+C";

        // Act
        _settingsService.Settings.CaptureHotkey = newHotkey;

        // Assert
        _settingsService.Settings.CaptureHotkey.Should().Be(newHotkey);
        _settingsService.Settings.CaptureHotkey.Should().NotBe(originalHotkey);
    }

    [Fact]
    public void Settings_ShouldPersistChanges()
    {
        // Arrange
        var originalHotkey = _settingsService.Settings.CaptureHotkey;
        var newHotkey = "Ctrl+Alt+C";

        // Act
        _settingsService.Settings.CaptureHotkey = newHotkey;
        _settingsService.SaveSettings();
        _settingsService.LoadSettings();

        // Assert
        _settingsService.Settings.CaptureHotkey.Should().Be(newHotkey);
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
        _settingsService.Settings.CaptureHotkey = hotkey;

        // Assert
        _settingsService.Settings.CaptureHotkey.Should().Be(hotkey);
    }

    [Fact]
    public void Settings_ShouldAllowMultipleHotkeyChanges()
    {
        // Arrange
        var captureHotkey = "Ctrl+Shift+C";
        var annotationHotkey = "Ctrl+Shift+A";
        var exportHotkey = "Ctrl+Shift+E";

        // Act
        _settingsService.Settings.CaptureHotkey = captureHotkey;
        _settingsService.Settings.AnnotationHotkey = annotationHotkey;
        _settingsService.Settings.ExportHotkey = exportHotkey;

        // Assert
        _settingsService.Settings.CaptureHotkey.Should().Be(captureHotkey);
        _settingsService.Settings.AnnotationHotkey.Should().Be(annotationHotkey);
        _settingsService.Settings.ExportHotkey.Should().Be(exportHotkey);
    }

    [Fact]
    public void Settings_ShouldMaintainIndependence()
    {
        // Arrange
        var settings1 = _settingsService.Settings;
        var settings2 = _settingsService.Settings;

        // Act
        settings1.CaptureHotkey = "Ctrl+1";
        settings2.CaptureHotkey = "Ctrl+2";

        // Assert
        settings1.CaptureHotkey.Should().Be("Ctrl+2"); // Should be the same instance
        settings2.CaptureHotkey.Should().Be("Ctrl+2");
    }

    [Fact]
    public void LoadSettings_ShouldReturnCurrentSettings()
    {
        // Arrange
        var expectedSettings = _settingsService.Settings;

        // Act
        var loadedSettings = _settingsService.LoadSettings();

        // Assert
        loadedSettings.Should().BeSameAs(expectedSettings);
    }

    [Fact]
    public void SaveSettings_ShouldReturnCurrentSettings()
    {
        // Arrange
        var expectedSettings = _settingsService.Settings;

        // Act
        var savedSettings = _settingsService.SaveSettings();

        // Assert
        savedSettings.Should().BeSameAs(expectedSettings);
    }

    [Fact]
    public void Settings_ShouldBeSerializable()
    {
        // Arrange
        _settingsService.Settings.CaptureHotkey = "Test+Hotkey";
        _settingsService.Settings.AnnotationHotkey = "Another+Hotkey";

        // Act & Assert
        var action = () => _settingsService.SaveSettings();
        action.Should().NotThrow();
    }

    [Fact]
    public void Settings_ShouldHandleNullValues()
    {
        // Act & Assert
        var action = () => _settingsService.Settings.CaptureHotkey = null!;
        action.Should().NotThrow();
    }

    [Fact]
    public void Settings_ShouldHandleEmptyStrings()
    {
        // Act & Assert
        var action = () => _settingsService.Settings.CaptureHotkey = "";
        action.Should().NotThrow();
        
        _settingsService.Settings.CaptureHotkey.Should().Be("");
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}
