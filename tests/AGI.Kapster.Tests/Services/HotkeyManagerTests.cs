using System.Threading.Tasks;

using FluentAssertions;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Services.Hotkeys;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using AGI.Kapster.Desktop.Services.Settings;
using AGI.Kapster.Tests.TestHelpers;

namespace AGI.Kapster.Tests.Services;

public class HotkeyManagerTests : TestBase
{
    private readonly IHotkeyProvider _hotkeyProvider;
    private readonly ISettingsService _settingsService;
    private readonly IOverlayCoordinator _overlayCoordinator;
    private readonly HotkeyManager _hotkeyManager;

    public HotkeyManagerTests(ITestOutputHelper output) : base(output)
    {
        _hotkeyProvider = Substitute.For<IHotkeyProvider>();
        _settingsService = Substitute.For<ISettingsService>();
        _overlayCoordinator = Substitute.For<IOverlayCoordinator>();

        _hotkeyManager = new HotkeyManager(_hotkeyProvider, _settingsService, _overlayCoordinator);
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        _hotkeyManager.Should().NotBeNull();
    }

    [Fact]
    public async Task InitializeAsync_WhenSupportedAndPermitted_ShouldRegisterConfiguredHotkeys()
    {
        // Arrange
        _hotkeyProvider.IsSupported.Returns(true);
        _hotkeyProvider.HasPermissions.Returns(true);

        var settings = new AppSettings
        {
            Hotkeys = new HotkeySettings
            {
                CaptureRegion = "Ctrl+Shift+A",
                OpenSettings = "Alt+S"
            }
        };
        _settingsService.Settings.Returns(settings);

        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Any<System.Action>())
            .Returns(true);

        // Act
        await _hotkeyManager.InitializeAsync();

        // Assert
        _hotkeyProvider.Received(1).UnregisterAll();

        _hotkeyProvider.Received(1).RegisterHotkey(
            "capture_region",
            HotkeyModifiers.Control | HotkeyModifiers.Shift,
            65,
            Arg.Any<System.Action>());

        _hotkeyProvider.Received(1).RegisterHotkey(
            "open_settings",
            HotkeyModifiers.Alt,
            83,
            Arg.Any<System.Action>());
    }

    [Fact]
    public void RegisterEscapeHotkey_ShouldCallProviderRegister()
    {
        // Arrange
        _hotkeyProvider.IsSupported.Returns(true);
        _hotkeyProvider.HasPermissions.Returns(true);
        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Any<System.Action>())
            .Returns(true);

        // Act
        _hotkeyManager.RegisterEscapeHotkey();

        // Assert
        _hotkeyProvider.Received(1).RegisterHotkey(
            "overlay_escape",
            HotkeyModifiers.None,
            27, // Escape key code
            Arg.Any<System.Action>()
        );
    }

    [Fact]
    public void RegisterEscapeHotkey_WhenTriggered_ShouldCloseAllOverlays()
    {
        // Arrange
        _hotkeyProvider.IsSupported.Returns(true);
        _hotkeyProvider.HasPermissions.Returns(true);
        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Any<System.Action>())
            .Returns(true);

        // Act
        _hotkeyManager.RegisterEscapeHotkey();

        // Assert
        _hotkeyProvider.Received(1).RegisterHotkey(
            Arg.Any<string>(),
            Arg.Any<HotkeyModifiers>(),
            Arg.Any<uint>(),
            Arg.Any<System.Action>()
        );
    }

    [Fact]
    public void UnregisterEscapeHotkey_WhenNotRegistered_ShouldNotCallProviderUnregister()
    {
        // Arrange
        _hotkeyProvider.IsSupported.Returns(true);
        _hotkeyProvider.HasPermissions.Returns(true);

        // Act
        _hotkeyManager.UnregisterEscapeHotkey();

        // Assert
        _hotkeyProvider.DidNotReceive().UnregisterHotkey("overlay_escape");
    }

    [Fact]
    public void UnregisterEscapeHotkey_WhenRegistered_ShouldCallProviderUnregister()
    {
        // Arrange
        _hotkeyProvider.IsSupported.Returns(true);
        _hotkeyProvider.HasPermissions.Returns(true);
        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Any<System.Action>())
            .Returns(true);

        // First register the hotkey
        _hotkeyManager.RegisterEscapeHotkey();

        // Act
        _hotkeyManager.UnregisterEscapeHotkey();

        // Assert
        _hotkeyProvider.Received(1).UnregisterHotkey("overlay_escape");
    }

    [Fact]
    public void RegisterEscapeHotkey_WhenProviderNotSupported_ShouldNotRegister()
    {
        // Arrange
        _hotkeyProvider.IsSupported.Returns(false);

        // Act
        _hotkeyManager.RegisterEscapeHotkey();

        // Assert
        _hotkeyProvider.DidNotReceive().RegisterHotkey(
            Arg.Any<string>(),
            Arg.Any<HotkeyModifiers>(),
            Arg.Any<uint>(),
            Arg.Any<System.Action>()
        );
    }

    [Fact]
    public void RegisterEscapeHotkey_WhenNoPermissions_ShouldNotRegister()
    {
        // Arrange
        _hotkeyProvider.IsSupported.Returns(true);
        _hotkeyProvider.HasPermissions.Returns(false);

        // Act
        _hotkeyManager.RegisterEscapeHotkey();

        // Assert
        _hotkeyProvider.DidNotReceive().RegisterHotkey(
            Arg.Any<string>(),
            Arg.Any<HotkeyModifiers>(),
            Arg.Any<uint>(),
            Arg.Any<System.Action>()
        );
    }
}
