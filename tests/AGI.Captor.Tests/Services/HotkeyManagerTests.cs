using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using AGI.Captor.Desktop.Services;
using AGI.Captor.Desktop.Services.Hotkeys;
using AGI.Captor.Desktop.Services.Overlay;
using AGI.Captor.Tests.TestHelpers;

namespace AGI.Captor.Tests.Services;

public class HotkeyManagerTests : TestBase
{
    private readonly IHotkeyProvider _hotkeyProvider;
    private readonly ISettingsService _settingsService;
    private readonly IOverlayController _overlayController;
    private readonly HotkeyManager _hotkeyManager;

    public HotkeyManagerTests(ITestOutputHelper output) : base(output)
    {
        _hotkeyProvider = Substitute.For<IHotkeyProvider>();
        _settingsService = Substitute.For<ISettingsService>();
        _overlayController = Substitute.For<IOverlayController>();
        
        _hotkeyManager = new HotkeyManager(_hotkeyProvider, _settingsService, _overlayController);
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Act & Assert
        _hotkeyManager.Should().NotBeNull();
    }

    [Fact]
    public void RegisterHotkey_WithValidHotkey_ShouldReturnTrue()
    {
        // Arrange
        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Any<Action>())
            .Returns(true);

        // Act
        var result = _hotkeyManager.RegisterHotkey("test_hotkey", HotkeyModifiers.None, 0x41, () => { });

        // Assert
        result.Should().BeTrue();
        _hotkeyProvider.Received(1).RegisterHotkey("test_hotkey", HotkeyModifiers.None, 0x41, Arg.Any<Action>());
    }

    [Fact]
    public void RegisterHotkey_WithInvalidHotkey_ShouldReturnFalse()
    {
        // Arrange
        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Any<Action>())
            .Returns(false);

        // Act
        var result = _hotkeyManager.RegisterHotkey("invalid_hotkey", HotkeyModifiers.None, 0x00, () => { });

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void RegisterHotkey_ShouldCallProviderWithCorrectParameters()
    {
        // Arrange
        var hotkeyId = "test_hotkey";
        var modifiers = HotkeyModifiers.Control;
        var keyCode = 0x53u; // 'S' key
        Action callback = () => { };

        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Any<Action>())
            .Returns(true);

        // Act
        _hotkeyManager.RegisterHotkey(hotkeyId, modifiers, keyCode, callback);

        // Assert
        _hotkeyProvider.Received(1).RegisterHotkey(hotkeyId, modifiers, keyCode, callback);
    }

    [Fact]
    public void UnregisterHotkey_ShouldCallProviderUnregister()
    {
        // Arrange
        var hotkeyId = "test_hotkey";

        // Act
        _hotkeyManager.UnregisterHotkey(hotkeyId);

        // Assert
        _hotkeyProvider.Received(1).UnregisterHotkey(hotkeyId);
    }

    [Fact]
    public void RegisterEscapeHotkey_ShouldRegisterEscapeKey()
    {
        // Arrange
        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Any<Action>())
            .Returns(true);

        // Act
        _hotkeyManager.RegisterEscapeHotkey();

        // Assert
        _hotkeyProvider.Received(1).RegisterHotkey("overlay_escape", HotkeyModifiers.None, 0x1B, Arg.Any<Action>());
    }

    [Fact]
    public void RegisterEscapeHotkey_WhenTriggered_ShouldCloseAllOverlays()
    {
        // Arrange
        Action? escapeCallback = null;
        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Do<Action>(callback => escapeCallback = callback))
            .Returns(true);

        _hotkeyManager.RegisterEscapeHotkey();

        // Act
        escapeCallback?.Invoke();

        // Assert
        _overlayController.Received(1).CloseAll();
    }

    [Fact]
    public void RegisterEscapeHotkey_WhenTriggered_ShouldUnregisterEscapeHotkey()
    {
        // Arrange
        Action? escapeCallback = null;
        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Do<Action>(callback => escapeCallback = callback))
            .Returns(true);

        _hotkeyManager.RegisterEscapeHotkey();

        // Act
        escapeCallback?.Invoke();

        // Assert
        _hotkeyProvider.Received(1).UnregisterHotkey("overlay_escape");
    }

    [Fact]
    public void UnregisterEscapeHotkey_WhenNotRegistered_ShouldNotCallProvider()
    {
        // Act
        _hotkeyManager.UnregisterEscapeHotkey();

        // Assert
        _hotkeyProvider.DidNotReceive().UnregisterHotkey("overlay_escape");
    }

    [Fact]
    public void UnregisterEscapeHotkey_WhenRegistered_ShouldCallProvider()
    {
        // Arrange
        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Any<Action>())
            .Returns(true);
        _hotkeyManager.RegisterEscapeHotkey();

        // Act
        _hotkeyManager.UnregisterEscapeHotkey();

        // Assert
        _hotkeyProvider.Received(1).UnregisterHotkey("overlay_escape");
    }

    [Fact]
    public void Dispose_ShouldDisposeHotkeyProvider()
    {
        // Act
        _hotkeyManager.Dispose();

        // Assert
        _hotkeyProvider.Received(1).Dispose();
    }

    [Theory]
    [InlineData(HotkeyModifiers.None)]
    [InlineData(HotkeyModifiers.Control)]
    [InlineData(HotkeyModifiers.Alt)]
    [InlineData(HotkeyModifiers.Shift)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Shift)]
    [InlineData(HotkeyModifiers.Alt | HotkeyModifiers.Shift)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift)]
    public void RegisterHotkey_WithDifferentModifiers_ShouldPassCorrectModifiers(HotkeyModifiers modifiers)
    {
        // Arrange
        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Any<Action>())
            .Returns(true);

        // Act
        _hotkeyManager.RegisterHotkey("test", modifiers, 0x41, () => { });

        // Assert
        _hotkeyProvider.Received(1).RegisterHotkey("test", modifiers, 0x41, Arg.Any<Action>());
    }

    [Theory]
    [InlineData(0x41)] // 'A' key
    [InlineData(0x42)] // 'B' key
    [InlineData(0x43)] // 'C' key
    [InlineData(0x1B)] // Escape key
    [InlineData(0x0D)] // Enter key
    [InlineData(0x20)] // Space key
    public void RegisterHotkey_WithDifferentKeyCodes_ShouldPassCorrectKeyCode(uint keyCode)
    {
        // Arrange
        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Any<Action>())
            .Returns(true);

        // Act
        _hotkeyManager.RegisterHotkey("test", HotkeyModifiers.None, keyCode, () => { });

        // Assert
        _hotkeyProvider.Received(1).RegisterHotkey("test", HotkeyModifiers.None, keyCode, Arg.Any<Action>());
    }

    [Fact]
    public void RegisterHotkey_WithNullCallback_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => _hotkeyManager.RegisterHotkey("test", HotkeyModifiers.None, 0x41, null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterHotkey_WithEmptyHotkeyId_ShouldThrowArgumentException()
    {
        // Act & Assert
        var action = () => _hotkeyManager.RegisterHotkey("", HotkeyModifiers.None, 0x41, () => { });
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UnregisterHotkey_WithEmptyHotkeyId_ShouldThrowArgumentException()
    {
        // Act & Assert
        var action = () => _hotkeyManager.UnregisterHotkey("");
        action.Should().Throw<ArgumentException>();
    }

    public override void Dispose()
    {
        _hotkeyManager?.Dispose();
        base.Dispose();
    }
}
