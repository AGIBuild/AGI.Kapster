using System.Text.Json;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Tests.TestHelpers;

namespace AGI.Kapster.Tests.Models;

public class HotkeyGestureTests : TestBase
{
    public HotkeyGestureTests(ITestOutputHelper output) : base(output)
    {
    }

    #region HotkeyGesture.FromChar Tests

    [Fact]
    public void FromChar_ShouldCreateGestureWithCharKeySpec()
    {
        // Act
        var gesture = HotkeyGesture.FromChar(HotkeyModifiers.Alt, 'A');

        // Assert
        gesture.Modifiers.Should().Be(HotkeyModifiers.Alt);
        gesture.KeySpec.Should().BeOfType<CharKeySpec>();
        ((CharKeySpec)gesture.KeySpec).Character.Should().Be('A');
    }

    [Fact]
    public void FromChar_WithMultipleModifiers_ShouldSetAllModifiers()
    {
        // Act
        var gesture = HotkeyGesture.FromChar(HotkeyModifiers.Control | HotkeyModifiers.Shift, 'S');

        // Assert
        gesture.Modifiers.Should().HaveFlag(HotkeyModifiers.Control);
        gesture.Modifiers.Should().HaveFlag(HotkeyModifiers.Shift);
    }

    [Fact]
    public void FromChar_WithNoModifiers_ShouldHaveNoneModifiers()
    {
        // Act
        var gesture = HotkeyGesture.FromChar(HotkeyModifiers.None, 'X');

        // Assert
        gesture.Modifiers.Should().Be(HotkeyModifiers.None);
    }

    #endregion

    #region HotkeyGesture.FromNamedKey Tests

    [Fact]
    public void FromNamedKey_ShouldCreateGestureWithNamedKeySpec()
    {
        // Act
        var gesture = HotkeyGesture.FromNamedKey(HotkeyModifiers.Control, NamedKey.F1);

        // Assert
        gesture.Modifiers.Should().Be(HotkeyModifiers.Control);
        gesture.KeySpec.Should().BeOfType<NamedKeySpec>();
        ((NamedKeySpec)gesture.KeySpec).NamedKey.Should().Be(NamedKey.F1);
    }

    [Fact]
    public void FromNamedKey_WithEscape_ShouldCreateCorrectGesture()
    {
        // Act
        var gesture = HotkeyGesture.FromNamedKey(HotkeyModifiers.None, NamedKey.Escape);

        // Assert
        gesture.Modifiers.Should().Be(HotkeyModifiers.None);
        ((NamedKeySpec)gesture.KeySpec).NamedKey.Should().Be(NamedKey.Escape);
    }

    #endregion

    #region HotkeyGesture.ToDisplayString Tests

    [Theory]
    [InlineData(HotkeyModifiers.Alt, 'A', "Alt+A")]
    [InlineData(HotkeyModifiers.Control, 'S', "Ctrl+S")]
    [InlineData(HotkeyModifiers.Shift, 'X', "Shift+X")]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Shift, 'A', "Ctrl+Shift+A")]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'C', "Ctrl+Alt+C")]
    [InlineData(HotkeyModifiers.None, 'Z', "Z")]
    public void ToDisplayString_WithCharKey_ShouldReturnCorrectFormat(HotkeyModifiers modifiers, char key, string expected)
    {
        // Arrange
        var gesture = HotkeyGesture.FromChar(modifiers, key);

        // Act
        var result = gesture.ToDisplayString();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(HotkeyModifiers.None, NamedKey.F1, "F1")]
    [InlineData(HotkeyModifiers.Control, NamedKey.F5, "Ctrl+F5")]
    [InlineData(HotkeyModifiers.Alt, NamedKey.Enter, "Alt+Enter")]
    [InlineData(HotkeyModifiers.None, NamedKey.Escape, "Esc")]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Shift, NamedKey.Delete, "Ctrl+Shift+Delete")]
    public void ToDisplayString_WithNamedKey_ShouldReturnCorrectFormat(HotkeyModifiers modifiers, NamedKey key, string expected)
    {
        // Arrange
        var gesture = HotkeyGesture.FromNamedKey(modifiers, key);

        // Act
        var result = gesture.ToDisplayString();

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region HotkeyGesture.Equals Tests

    [Fact]
    public void Equals_WithSameCharGestures_ShouldReturnTrue()
    {
        // Arrange
        var gesture1 = HotkeyGesture.FromChar(HotkeyModifiers.Alt, 'A');
        var gesture2 = HotkeyGesture.FromChar(HotkeyModifiers.Alt, 'A');

        // Act & Assert
        gesture1.Equals(gesture2).Should().BeTrue();
        gesture1.Should().Be(gesture2);
    }

    [Fact]
    public void Equals_WithDifferentCharacters_ShouldReturnFalse()
    {
        // Arrange
        var gesture1 = HotkeyGesture.FromChar(HotkeyModifiers.Alt, 'A');
        var gesture2 = HotkeyGesture.FromChar(HotkeyModifiers.Alt, 'B');

        // Act & Assert
        gesture1.Equals(gesture2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentModifiers_ShouldReturnFalse()
    {
        // Arrange
        var gesture1 = HotkeyGesture.FromChar(HotkeyModifiers.Alt, 'A');
        var gesture2 = HotkeyGesture.FromChar(HotkeyModifiers.Control, 'A');

        // Act & Assert
        gesture1.Equals(gesture2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithSameNamedKeyGestures_ShouldReturnTrue()
    {
        // Arrange
        var gesture1 = HotkeyGesture.FromNamedKey(HotkeyModifiers.Control, NamedKey.F1);
        var gesture2 = HotkeyGesture.FromNamedKey(HotkeyModifiers.Control, NamedKey.F1);

        // Act & Assert
        gesture1.Equals(gesture2).Should().BeTrue();
    }

    [Fact]
    public void Equals_CharAndNamedKey_ShouldReturnFalse()
    {
        // Arrange
        var charGesture = HotkeyGesture.FromChar(HotkeyModifiers.None, ' ');
        var namedGesture = HotkeyGesture.FromNamedKey(HotkeyModifiers.None, NamedKey.Space);

        // Act & Assert - Different types even if both represent "Space"
        charGesture.Equals(namedGesture).Should().BeFalse();
    }

    #endregion

    #region HotkeyGesture.GetHashCode Tests

    [Fact]
    public void GetHashCode_SameGestures_ShouldReturnSameHashCode()
    {
        // Arrange
        var gesture1 = HotkeyGesture.FromChar(HotkeyModifiers.Alt, 'A');
        var gesture2 = HotkeyGesture.FromChar(HotkeyModifiers.Alt, 'A');

        // Act & Assert
        gesture1.GetHashCode().Should().Be(gesture2.GetHashCode());
    }

    #endregion

    #region CharKeySpec Tests

    [Theory]
    [InlineData(' ', "Space")]
    [InlineData('-', "-")]
    [InlineData('=', "=")]
    [InlineData('A', "A")]
    [InlineData('1', "1")]
    public void CharKeySpec_ToDisplayString_ShouldReturnCorrectValue(char character, string expected)
    {
        // Arrange
        var spec = new CharKeySpec(character);

        // Act
        var result = spec.ToDisplayString();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CharKeySpec_Equals_SameCharacter_ShouldReturnTrue()
    {
        // Arrange
        var spec1 = new CharKeySpec('A');
        var spec2 = new CharKeySpec('A');

        // Act & Assert
        spec1.Equals(spec2).Should().BeTrue();
    }

    [Fact]
    public void CharKeySpec_Equals_DifferentCharacter_ShouldReturnFalse()
    {
        // Arrange
        var spec1 = new CharKeySpec('A');
        var spec2 = new CharKeySpec('B');

        // Act & Assert
        spec1.Equals(spec2).Should().BeFalse();
    }

    #endregion

    #region NamedKeySpec Tests

    [Theory]
    [InlineData(NamedKey.F1, "F1")]
    [InlineData(NamedKey.F12, "F12")]
    [InlineData(NamedKey.Enter, "Enter")]
    [InlineData(NamedKey.Escape, "Esc")]
    [InlineData(NamedKey.Tab, "Tab")]
    [InlineData(NamedKey.Space, "Space")]
    [InlineData(NamedKey.Up, "Up")]
    [InlineData(NamedKey.Down, "Down")]
    [InlineData(NamedKey.Left, "Left")]
    [InlineData(NamedKey.Right, "Right")]
    public void NamedKeySpec_ToDisplayString_ShouldReturnCorrectValue(NamedKey key, string expected)
    {
        // Arrange
        var spec = new NamedKeySpec(key);

        // Act
        var result = spec.ToDisplayString();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void NamedKeySpec_Equals_SameKey_ShouldReturnTrue()
    {
        // Arrange
        var spec1 = new NamedKeySpec(NamedKey.F1);
        var spec2 = new NamedKeySpec(NamedKey.F1);

        // Act & Assert
        spec1.Equals(spec2).Should().BeTrue();
    }

    [Fact]
    public void NamedKeySpec_Equals_DifferentKey_ShouldReturnFalse()
    {
        // Arrange
        var spec1 = new NamedKeySpec(NamedKey.F1);
        var spec2 = new NamedKeySpec(NamedKey.F2);

        // Act & Assert
        spec1.Equals(spec2).Should().BeFalse();
    }

    #endregion

    #region JSON Serialization Tests

    [Fact]
    public void JsonSerialization_CharKeySpec_ShouldRoundTrip()
    {
        // Arrange
        var original = HotkeyGesture.FromChar(HotkeyModifiers.Alt, 'A');
        var options = new JsonSerializerOptions { WriteIndented = false };

        // Act
        var json = JsonSerializer.Serialize(original, options);
        var deserialized = JsonSerializer.Deserialize<HotkeyGesture>(json, options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Modifiers.Should().Be(original.Modifiers);
        deserialized.KeySpec.Should().BeOfType<CharKeySpec>();
        ((CharKeySpec)deserialized.KeySpec).Character.Should().Be('A');
    }

    [Fact]
    public void JsonSerialization_NamedKeySpec_ShouldRoundTrip()
    {
        // Arrange
        var original = HotkeyGesture.FromNamedKey(HotkeyModifiers.Control, NamedKey.F5);
        var options = new JsonSerializerOptions { WriteIndented = false };

        // Act
        var json = JsonSerializer.Serialize(original, options);
        var deserialized = JsonSerializer.Deserialize<HotkeyGesture>(json, options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Modifiers.Should().Be(original.Modifiers);
        deserialized.KeySpec.Should().BeOfType<NamedKeySpec>();
        ((NamedKeySpec)deserialized.KeySpec).NamedKey.Should().Be(NamedKey.F5);
    }

    [Fact]
    public void JsonSerialization_CharKeySpec_ShouldContainTypeDiscriminator()
    {
        // Arrange
        var gesture = HotkeyGesture.FromChar(HotkeyModifiers.Alt, 'A');

        // Act
        var json = JsonSerializer.Serialize(gesture);

        // Assert
        json.Should().Contain("\"$type\":\"char\"");
    }

    [Fact]
    public void JsonSerialization_NamedKeySpec_ShouldContainTypeDiscriminator()
    {
        // Arrange
        var gesture = HotkeyGesture.FromNamedKey(HotkeyModifiers.None, NamedKey.Escape);

        // Act
        var json = JsonSerializer.Serialize(gesture);

        // Assert
        json.Should().Contain("\"$type\":\"named\"");
    }

    #endregion

    #region HotkeyModifiers Tests

    [Fact]
    public void HotkeyModifiers_FlagsEnum_ShouldSupportCombinations()
    {
        // Arrange
        var combined = HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift;

        // Act & Assert
        combined.HasFlag(HotkeyModifiers.Control).Should().BeTrue();
        combined.HasFlag(HotkeyModifiers.Alt).Should().BeTrue();
        combined.HasFlag(HotkeyModifiers.Shift).Should().BeTrue();
        combined.HasFlag(HotkeyModifiers.Win).Should().BeFalse();
    }

    [Fact]
    public void HotkeyModifiers_None_ShouldBeZero()
    {
        // Assert
        ((int)HotkeyModifiers.None).Should().Be(0);
    }

    #endregion
}

