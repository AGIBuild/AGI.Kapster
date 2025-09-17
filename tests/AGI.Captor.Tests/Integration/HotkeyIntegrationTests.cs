using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using AGI.Captor.Desktop.Services;
using AGI.Captor.Desktop.Services.Hotkeys;
using AGI.Captor.Desktop.Services.Overlay;
using AGI.Captor.Tests.TestHelpers;
using SharpHook;
using SharpHook.Native;

namespace AGI.Captor.Tests.Integration;

/// <summary>
/// Integration tests for hotkey functionality using SharpHook for simulation
/// </summary>
public class HotkeyIntegrationTests : TestBase
{
    private readonly IHotkeyProvider _hotkeyProvider;
    private readonly ISettingsService _settingsService;
    private readonly IOverlayController _overlayController;
    private readonly HotkeyManager _hotkeyManager;
    private readonly SharpHookTestHelper _sharpHookHelper;

    public HotkeyIntegrationTests(ITestOutputHelper output) : base(output)
    {
        _hotkeyProvider = Substitute.For<IHotkeyProvider>();
        _settingsService = Substitute.For<ISettingsService>();
        _overlayController = Substitute.For<IOverlayController>();
        _hotkeyManager = new HotkeyManager(_hotkeyProvider, _settingsService, _overlayController);
        _sharpHookHelper = new SharpHookTestHelper();
    }

    [Fact]
    public async Task HotkeyManager_WithSharpHookSimulation_ShouldHandleKeyPress()
    {
        // Arrange
        var hotkeyTriggered = false;
        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Do<Action>(callback => callback()))
            .Returns(true);

        _hotkeyManager.RegisterHotkey("test_hotkey", HotkeyModifiers.Control, 0x41, () => hotkeyTriggered = true);

        // Act
        _sharpHookHelper.SimulateKeyPressAndRelease("VcA", "LeftCtrl");
        await _sharpHookHelper.WaitForEventsAsync(100);

        // Assert
        hotkeyTriggered.Should().BeTrue();
    }

    [Fact]
    public async Task HotkeyManager_WithSharpHookSimulation_ShouldHandleEscapeKey()
    {
        // Arrange
        var escapeTriggered = false;
        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Do<Action>(callback => callback()))
            .Returns(true);

        _hotkeyManager.RegisterEscapeHotkey();

        // Act
        _sharpHookHelper.SimulateKeyPressAndRelease("VcEscape");
        await _sharpHookHelper.WaitForEventsAsync(100);

        // Assert
        _overlayController.Received(1).CloseAll();
    }

    [Fact]
    public async Task HotkeyManager_WithSharpHookSimulation_ShouldHandleMultipleKeyCombinations()
    {
        // Arrange
        var ctrlA = false;
        var ctrlS = false;
        var altF4 = false;

        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Do<Action>(callback => callback()))
            .Returns(true);

        _hotkeyManager.RegisterHotkey("ctrl_a", HotkeyModifiers.Control, 0x41, () => ctrlA = true);
        _hotkeyManager.RegisterHotkey("ctrl_s", HotkeyModifiers.Control, 0x53, () => ctrlS = true);
        _hotkeyManager.RegisterHotkey("alt_f4", HotkeyModifiers.Alt, 0x73, () => altF4 = true);

        // Act
        _sharpHookHelper.SimulateKeyPressAndRelease("VcA", "LeftCtrl");
        await _sharpHookHelper.WaitForEventsAsync(50);

        _sharpHookHelper.SimulateKeyPressAndRelease("VcS", "LeftCtrl");
        await _sharpHookHelper.WaitForEventsAsync(50);

        _sharpHookHelper.SimulateKeyPressAndRelease("VcF4", "LeftAlt");
        await _sharpHookHelper.WaitForEventsAsync(50);

        // Assert
        ctrlA.Should().BeTrue();
        ctrlS.Should().BeTrue();
        altF4.Should().BeTrue();
    }

    [Fact]
    public async Task HotkeyManager_WithSharpHookSimulation_ShouldHandleMouseEvents()
    {
        // Arrange
        var mouseClickTriggered = false;
        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Do<Action>(callback => callback()))
            .Returns(true);

        _hotkeyManager.RegisterHotkey("mouse_click", HotkeyModifiers.None, 0x01, () => mouseClickTriggered = true);

        // Act
        _sharpHookHelper.SimulateMouseClick("Button1");
        await _sharpHookHelper.WaitForEventsAsync(100);

        // Assert
        mouseClickTriggered.Should().BeTrue();
    }

    [Fact]
    public async Task HotkeyManager_WithSharpHookSimulation_ShouldHandleMouseWheel()
    {
        // Arrange
        var wheelUpTriggered = false;
        var wheelDownTriggered = false;

        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Do<Action>(callback => callback()))
            .Returns(true);

        _hotkeyManager.RegisterHotkey("wheel_up", HotkeyModifiers.None, 0x78, () => wheelUpTriggered = true);
        _hotkeyManager.RegisterHotkey("wheel_down", HotkeyModifiers.None, 0x79, () => wheelDownTriggered = true);

        // Act
        _sharpHookHelper.SimulateMouseWheel(1, "Vertical");
        await _sharpHookHelper.WaitForEventsAsync(50);

        _sharpHookHelper.SimulateMouseWheel(-1, "Vertical");
        await _sharpHookHelper.WaitForEventsAsync(50);

        // Assert
        wheelUpTriggered.Should().BeTrue();
        wheelDownTriggered.Should().BeTrue();
    }

    [Fact]
    public async Task HotkeyManager_WithSharpHookSimulation_ShouldHandleComplexKeySequences()
    {
        // Arrange
        var sequenceCompleted = false;
        var step1 = false;
        var step2 = false;
        var step3 = false;

        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Do<Action>(callback => callback()))
            .Returns(true);

        _hotkeyManager.RegisterHotkey("step1", HotkeyModifiers.Control, 0x41, () => step1 = true);
        _hotkeyManager.RegisterHotkey("step2", HotkeyModifiers.Control, 0x42, () => step2 = true);
        _hotkeyManager.RegisterHotkey("step3", HotkeyModifiers.Control, 0x43, () => step3 = true);

        // Act
        _sharpHookHelper.SimulateKeyPressAndRelease("VcA", "LeftCtrl");
        await _sharpHookHelper.WaitForEventsAsync(50);

        _sharpHookHelper.SimulateKeyPressAndRelease("VcB", "LeftCtrl");
        await _sharpHookHelper.WaitForEventsAsync(50);

        _sharpHookHelper.SimulateKeyPressAndRelease("VcC", "LeftCtrl");
        await _sharpHookHelper.WaitForEventsAsync(50);

        sequenceCompleted = step1 && step2 && step3;

        // Assert
        step1.Should().BeTrue();
        step2.Should().BeTrue();
        step3.Should().BeTrue();
        sequenceCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task HotkeyManager_WithSharpHookSimulation_ShouldHandleRapidKeyPresses()
    {
        // Arrange
        var pressCount = 0;
        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Do<Action>(callback => callback()))
            .Returns(true);

        _hotkeyManager.RegisterHotkey("rapid_test", HotkeyModifiers.None, 0x41, () => pressCount++);

        // Act
        for (int i = 0; i < 5; i++)
        {
            _sharpHookHelper.SimulateKeyPressAndRelease("VcA");
            await _sharpHookHelper.WaitForEventsAsync(10);
        }

        // Assert
        pressCount.Should().Be(5);
    }

    [Fact]
    public async Task HotkeyManager_WithSharpHookSimulation_ShouldHandleMouseMovement()
    {
        // Arrange
        var mouseMoved = false;
        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Do<Action>(callback => callback()))
            .Returns(true);

        _hotkeyManager.RegisterHotkey("mouse_move", HotkeyModifiers.None, 0x02, () => mouseMoved = true);

        // Act
        _sharpHookHelper.SimulateMouseMovement(100, 200);
        await _sharpHookHelper.WaitForEventsAsync(100);

        // Assert
        mouseMoved.Should().BeTrue();
    }

    [Fact]
    public async Task HotkeyManager_WithSharpHookSimulation_ShouldHandleKeyHoldAndRelease()
    {
        // Arrange
        var keyPressed = false;
        var keyReleased = false;

        _hotkeyProvider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyModifiers>(), Arg.Any<uint>(), Arg.Do<Action>(callback => callback()))
            .Returns(true);

        _hotkeyManager.RegisterHotkey("key_press", HotkeyModifiers.None, 0x41, () => keyPressed = true);
        _hotkeyManager.RegisterHotkey("key_release", HotkeyModifiers.None, 0x42, () => keyReleased = true);

        // Act
        _sharpHookHelper.SimulateKeyPress("VcA");
        await _sharpHookHelper.WaitForEventsAsync(50);

        _sharpHookHelper.SimulateKeyRelease("VcA");
        await _sharpHookHelper.WaitForEventsAsync(50);

        // Assert
        keyPressed.Should().BeTrue();
        keyReleased.Should().BeTrue();
    }

    public override void Dispose()
    {
        _hotkeyManager?.Dispose();
        _sharpHookHelper?.Dispose();
        base.Dispose();
    }
}
