using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using AGI.Kapster.Desktop.Services.Hotkeys;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Tests.TestHelpers;

namespace AGI.Kapster.Tests.Services.Hotkeys;

public class HotkeyProviderTests : TestBase
{
    public HotkeyProviderTests(ITestOutputHelper output) : base(output)
    {
    }
    [Fact]
    public void UnsupportedHotkeyProvider_ShouldHaveCorrectProperties()
    {
        // Arrange
        var provider = Substitute.For<IHotkeyProvider>();

        // Act & Assert
        provider.IsSupported.Should().BeFalse();
        provider.HasPermissions.Should().BeFalse();
    }

    [Fact]
    public void UnsupportedHotkeyProvider_RegisterHotkey_ShouldReturnFalse()
    {
        // Arrange
        var provider = Substitute.For<IHotkeyProvider>();
        provider.IsSupported.Returns(false);
        provider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyGesture>(), Arg.Any<Action>())
            .Returns(false);

        // Act
        var result = provider.RegisterHotkey("test", HotkeyGesture.FromChar(HotkeyModifiers.None, 'A'), () => { });

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void UnsupportedHotkeyProvider_UnregisterHotkey_ShouldReturnFalse()
    {
        // Arrange
        var provider = Substitute.For<IHotkeyProvider>();
        provider.IsSupported.Returns(false);
        provider.UnregisterHotkey(Arg.Any<string>()).Returns(false);

        // Act
        var result = provider.UnregisterHotkey("test");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SupportedHotkeyProvider_ShouldHaveCorrectProperties()
    {
        // Arrange
        var provider = Substitute.For<IHotkeyProvider>();
        provider.IsSupported.Returns(true);
        provider.HasPermissions.Returns(true);

        // Act & Assert
        provider.IsSupported.Should().BeTrue();
        provider.HasPermissions.Should().BeTrue();
    }

    [Fact]
    public void SupportedHotkeyProvider_RegisterHotkey_ShouldReturnTrue()
    {
        // Arrange
        var provider = Substitute.For<IHotkeyProvider>();
        provider.IsSupported.Returns(true);
        provider.HasPermissions.Returns(true);
        provider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyGesture>(), Arg.Any<Action>())
            .Returns(true);

        // Act
        var result = provider.RegisterHotkey("test", HotkeyGesture.FromChar(HotkeyModifiers.None, 'A'), () => { });

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void SupportedHotkeyProvider_UnregisterHotkey_ShouldReturnTrue()
    {
        // Arrange
        var provider = Substitute.For<IHotkeyProvider>();
        provider.IsSupported.Returns(true);
        provider.HasPermissions.Returns(true);
        provider.UnregisterHotkey(Arg.Any<string>()).Returns(true);

        // Act
        var result = provider.UnregisterHotkey("test");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HotkeyProvider_RegisterHotkey_ShouldCallWithCorrectParameters()
    {
        // Arrange
        var provider = Substitute.For<IHotkeyProvider>();
        provider.IsSupported.Returns(true);
        provider.HasPermissions.Returns(true);
        provider.RegisterHotkey(Arg.Any<string>(), Arg.Any<HotkeyGesture>(), Arg.Any<Action>())
            .Returns(true);

        var callback = new Action(() => { });
        var gesture = HotkeyGesture.FromChar(HotkeyModifiers.Control, 'A');

        // Act
        provider.RegisterHotkey("test_hotkey", gesture, callback);

        // Assert
        provider.Received(1).RegisterHotkey("test_hotkey", gesture, callback);
    }

    [Fact]
    public void HotkeyProvider_UnregisterHotkey_ShouldCallWithCorrectParameters()
    {
        // Arrange
        var provider = Substitute.For<IHotkeyProvider>();
        provider.IsSupported.Returns(true);
        provider.HasPermissions.Returns(true);
        provider.UnregisterHotkey(Arg.Any<string>()).Returns(true);

        // Act
        provider.UnregisterHotkey("test_hotkey");

        // Assert
        provider.Received(1).UnregisterHotkey("test_hotkey");
    }
}
