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
        settings.Hotkeys.Should().NotBeNull();
        settings.Hotkeys.CaptureRegion.Should().NotBeNull();
        settings.Hotkeys.OpenSettings.Should().NotBeNull();
        settings.Hotkeys.CaptureRegion.KeySpec.Should().NotBeNull();
        settings.Hotkeys.OpenSettings.KeySpec.Should().NotBeNull();
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
        var newHotkey = HotkeyGesture.FromChar(HotkeyModifiers.Control | HotkeyModifiers.Shift, 'C');

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
        var newHotkey = HotkeyGesture.FromChar(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'C');

        // Act
        _settingsService.Settings.Hotkeys.CaptureRegion = newHotkey;
        await _settingsService.SaveAsync();

        // Assert
        _settingsService.Settings.Hotkeys.CaptureRegion.Should().Be(newHotkey);
        _settingsService.Settings.Hotkeys.CaptureRegion.Should().NotBe(originalHotkey);
    }

    [Fact]
    public void Settings_ShouldAllowMultipleHotkeyChanges()
    {
        // Arrange
        var captureHotkey = HotkeyGesture.FromChar(HotkeyModifiers.Control | HotkeyModifiers.Shift, 'C');
        var openSettingsHotkey = HotkeyGesture.FromChar(HotkeyModifiers.Control | HotkeyModifiers.Shift, 'S');

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
        settings1.Hotkeys.CaptureRegion = HotkeyGesture.FromChar(HotkeyModifiers.Control, '1');
        settings2.Hotkeys.CaptureRegion = HotkeyGesture.FromChar(HotkeyModifiers.Control, '2');

        // Assert - Should be the same instance (singleton pattern)
        settings1.Hotkeys.CaptureRegion.Should().Be(HotkeyGesture.FromChar(HotkeyModifiers.Control, '2'));
        settings2.Hotkeys.CaptureRegion.Should().Be(HotkeyGesture.FromChar(HotkeyModifiers.Control, '2'));
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
        _settingsService.Settings.Hotkeys.CaptureRegion = HotkeyGesture.FromChar(HotkeyModifiers.Control, 'A');
        _settingsService.Settings.Hotkeys.OpenSettings = HotkeyGesture.FromChar(HotkeyModifiers.Alt, 'S');

        // Act & Assert
        var action = async () => await _settingsService.SaveAsync();
        await action.Should().NotThrowAsync();

        // Verify the settings were saved to memory file system
        _fileSystemService.FileExists(_settingsService.GetSettingsFilePath()).Should().BeTrue();
    }

    [Fact]
    public async Task Settings_ShouldLoadAndSaveCorrectly()
    {
        // Arrange
        var newHotkey = HotkeyGesture.FromChar(HotkeyModifiers.Control | HotkeyModifiers.Shift, 'S');

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
        newSettings.Hotkeys.CaptureRegion = HotkeyGesture.FromChar(HotkeyModifiers.Alt, 'X');
        newSettings.General.StartWithWindows = false;

        // Act
        await _settingsService.UpdateSettingsAsync(newSettings);

        // Assert
        _settingsService.Settings.Hotkeys.CaptureRegion.Should().Be(HotkeyGesture.FromChar(HotkeyModifiers.Alt, 'X'));
        _settingsService.Settings.General.StartWithWindows.Should().BeFalse();
    }

    [Fact]
    public void ResetToDefaults_ShouldResetAllSettings()
    {
        // Arrange
        _settingsService.Settings.Hotkeys.CaptureRegion = HotkeyGesture.FromChar(HotkeyModifiers.Control, 'Z');
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
