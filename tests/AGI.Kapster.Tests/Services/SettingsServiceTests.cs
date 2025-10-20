using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Services;
using AGI.Kapster.Desktop.Services.Settings;
using AGI.Kapster.Tests.TestHelpers;

namespace AGI.Kapster.Tests.Services;

public class SettingsServiceTests : TestBase
{
    private readonly SettingsService _settingsService;
    private readonly MemoryFileSystemService _fileSystemService;
    private readonly IConfiguration? _configuration;

    public SettingsServiceTests(ITestOutputHelper output) : base(output)
    {
        _fileSystemService = new MemoryFileSystemService();
        _configuration = null; // Use default configuration for most tests
        _settingsService = new SettingsService(_fileSystemService, _configuration);
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
    public void Settings_ShouldMaintainSingleInstance()
    {
        // Arrange
        var settings1 = _settingsService.Settings;
        var settings2 = _settingsService.Settings;

        // Act
        settings1.Hotkeys.CaptureRegion = "Ctrl+1";
        settings2.Hotkeys.CaptureRegion = "Ctrl+2";

        // Assert - Should be the same instance (singleton pattern)
        settings1.Hotkeys.CaptureRegion.Should().Be("Ctrl+2");
        settings2.Hotkeys.CaptureRegion.Should().Be("Ctrl+2");
        settings1.Should().BeSameAs(settings2);
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
        // Arrange
        _settingsService.Settings.Hotkeys.CaptureRegion = "Test+Hotkey";
        _settingsService.Settings.Hotkeys.OpenSettings = "Another+Hotkey";

        // Act & Assert
        var action = async () => await _settingsService.SaveAsync();
        await action.Should().NotThrowAsync();

        // Verify the settings were saved to memory file system
        _fileSystemService.FileExists(_settingsService.GetSettingsFilePath()).Should().BeTrue();
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

    [Fact]
    public async Task Settings_ShouldLoadAndSaveCorrectly()
    {
        // Arrange
        var newHotkey = "Ctrl+Shift+S";

        // Act - Change setting
        _settingsService.Settings.Hotkeys.CaptureRegion = newHotkey;
        await _settingsService.SaveAsync();

        // Create new service instance to test loading (simulating singleton behavior)
        var newFileSystem = new MemoryFileSystemService();
        // Copy the saved data to the new file system
        var savedData = _fileSystemService.ReadAllText(_settingsService.GetSettingsFilePath());
        await newFileSystem.WriteAllTextAsync(_settingsService.GetSettingsFilePath(), savedData);

        var newSettingsService = new SettingsService(newFileSystem, _configuration);

        // Assert
        newSettingsService.Settings.Hotkeys.CaptureRegion.Should().Be(newHotkey);
    }

    [Fact]
    public async Task UpdateSettingsAsync_ShouldUpdateAndSave()
    {
        // Arrange
        var newSettings = new AppSettings();
        newSettings.Hotkeys.CaptureRegion = "Alt+X";
        newSettings.General.StartWithWindows = false;

        // Act
        await _settingsService.UpdateSettingsAsync(newSettings);

        // Assert
        _settingsService.Settings.Hotkeys.CaptureRegion.Should().Be("Alt+X");
        _settingsService.Settings.General.StartWithWindows.Should().BeFalse();
    }

    [Fact]
    public void ResetToDefaults_ShouldResetAllSettings()
    {
        // Arrange
        _settingsService.Settings.Hotkeys.CaptureRegion = "Custom+Hotkey";
        var originalDefaults = new AppSettings();

        // Act
        _settingsService.ResetToDefaults();

        // Assert
        _settingsService.Settings.Hotkeys.CaptureRegion.Should().Be(originalDefaults.Hotkeys.CaptureRegion);
    }

    [Fact]
    public void GetSettingsFilePath_ShouldReturnValidPath()
    {
        // Act
        var path = _settingsService.GetSettingsFilePath();

        // Assert
        path.Should().NotBeNullOrEmpty();
        path.Should().EndWith("settings.json");
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}
