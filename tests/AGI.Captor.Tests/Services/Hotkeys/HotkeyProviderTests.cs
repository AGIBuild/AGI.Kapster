using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using AGI.Captor.Desktop.Services.Hotkeys;
using AGI.Captor.Tests.TestHelpers;

namespace AGI.Captor.Tests.Services.Hotkeys;

public class HotkeyProviderTests : TestBase
{
    public HotkeyProviderTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void UnsupportedHotkeyProvider_ShouldHaveCorrectProperties()
    {
        // Arrange
        var provider = new UnsupportedHotkeyProvider();

        // Act & Assert
        provider.IsSupported.Should().BeFalse();
        provider.HasPermissions.Should().BeFalse();
    }

    [Fact]
    public void UnsupportedHotkeyProvider_RegisterHotkey_ShouldReturnFalse()
    {
        // Arrange
        var provider = new UnsupportedHotkeyProvider();

        // Act
        var result = provider.RegisterHotkey("test", HotkeyModifiers.None, 0x41, () => { });

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void UnsupportedHotkeyProvider_UnregisterHotkey_ShouldReturnFalse()
    {
        // Arrange
        var provider = new UnsupportedHotkeyProvider();

        // Act
        var result = provider.UnregisterHotkey("test");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void UnsupportedHotkeyProvider_Dispose_ShouldNotThrow()
    {
        // Arrange
        var provider = new UnsupportedHotkeyProvider();

        // Act & Assert
        var action = () => provider.Dispose();
        action.Should().NotThrow();
    }

    [Fact]
    public void UnsupportedHotkeyProvider_MultipleDispose_ShouldNotThrow()
    {
        // Arrange
        var provider = new UnsupportedHotkeyProvider();

        // Act & Assert
        var action = () =>
        {
            provider.Dispose();
            provider.Dispose();
        };
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("test")]
    [InlineData("complex_hotkey_id")]
    public void UnsupportedHotkeyProvider_RegisterHotkey_WithDifferentIds_ShouldReturnFalse(string hotkeyId)
    {
        // Arrange
        var provider = new UnsupportedHotkeyProvider();

        // Act
        var result = provider.RegisterHotkey(hotkeyId, HotkeyModifiers.None, 0x41, () => { });

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(HotkeyModifiers.None)]
    [InlineData(HotkeyModifiers.Control)]
    [InlineData(HotkeyModifiers.Alt)]
    [InlineData(HotkeyModifiers.Shift)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt)]
    public void UnsupportedHotkeyProvider_RegisterHotkey_WithDifferentModifiers_ShouldReturnFalse(HotkeyModifiers modifiers)
    {
        // Arrange
        var provider = new UnsupportedHotkeyProvider();

        // Act
        var result = provider.RegisterHotkey("test", modifiers, 0x41, () => { });

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(0x41)] // 'A' key
    [InlineData(0x42)] // 'B' key
    [InlineData(0x1B)] // Escape key
    [InlineData(0x0D)] // Enter key
    [InlineData(0x00)] // Invalid key
    public void UnsupportedHotkeyProvider_RegisterHotkey_WithDifferentKeyCodes_ShouldReturnFalse(uint keyCode)
    {
        // Arrange
        var provider = new UnsupportedHotkeyProvider();

        // Act
        var result = provider.RegisterHotkey("test", HotkeyModifiers.None, keyCode, () => { });

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void UnsupportedHotkeyProvider_RegisterHotkey_WithNullCallback_ShouldReturnFalse()
    {
        // Arrange
        var provider = new UnsupportedHotkeyProvider();

        // Act
        var result = provider.RegisterHotkey("test", HotkeyModifiers.None, 0x41, null!);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("test")]
    [InlineData("complex_hotkey_id")]
    public void UnsupportedHotkeyProvider_UnregisterHotkey_WithDifferentIds_ShouldReturnFalse(string hotkeyId)
    {
        // Arrange
        var provider = new UnsupportedHotkeyProvider();

        // Act
        var result = provider.UnregisterHotkey(hotkeyId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void UnsupportedHotkeyProvider_ShouldImplementIDisposable()
    {
        // Arrange
        var provider = new UnsupportedHotkeyProvider();

        // Act & Assert
        provider.Should().BeAssignableTo<IDisposable>();
    }

    [Fact]
    public void UnsupportedHotkeyProvider_ShouldImplementIHotkeyProvider()
    {
        // Arrange
        var provider = new UnsupportedHotkeyProvider();

        // Act & Assert
        provider.Should().BeAssignableTo<IHotkeyProvider>();
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}
