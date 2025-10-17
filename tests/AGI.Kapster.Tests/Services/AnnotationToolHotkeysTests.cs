using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using AGI.Kapster.Desktop.Models;
using AGI.Kapster.Desktop.Services;
using AGI.Kapster.Desktop.Services.Annotation;
using AGI.Kapster.Tests.TestHelpers;

namespace AGI.Kapster.Tests.Services;

public class AnnotationToolHotkeysTests : TestBase
{
    public AnnotationToolHotkeysTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void GetHotkey_ShouldReturnCorrectHotkeyForEachTool()
    {
        // Act & Assert
        AnnotationToolHotkeys.GetHotkey(AnnotationToolType.None).Should().Be("S"); // None is used for selection tool
        AnnotationToolHotkeys.GetHotkey(AnnotationToolType.Arrow).Should().Be("A");
        AnnotationToolHotkeys.GetHotkey(AnnotationToolType.Rectangle).Should().Be("R");
        AnnotationToolHotkeys.GetHotkey(AnnotationToolType.Ellipse).Should().Be("E");
        AnnotationToolHotkeys.GetHotkey(AnnotationToolType.Text).Should().Be("T");
        AnnotationToolHotkeys.GetHotkey(AnnotationToolType.Freehand).Should().Be("F");
        AnnotationToolHotkeys.GetHotkey(AnnotationToolType.Mosaic).Should().Be("M");
        AnnotationToolHotkeys.GetHotkey(AnnotationToolType.Emoji).Should().Be("J");
    }

    [Fact]
    public void GetHotkey_WithNoneTool_ShouldReturnSelectionHotkey()
    {
        // Act
        var result = AnnotationToolHotkeys.GetHotkey(AnnotationToolType.None);

        // Assert
        result.Should().Be("S"); // None is used for selection tool
    }

    [Fact]
    public void GetToolFromHotkey_ShouldReturnCorrectToolForEachHotkey()
    {
        // Act & Assert
        AnnotationToolHotkeys.GetToolFromHotkey("S").Should().Be(AnnotationToolType.None); // None is used for selection tool
        AnnotationToolHotkeys.GetToolFromHotkey("A").Should().Be(AnnotationToolType.Arrow);
        AnnotationToolHotkeys.GetToolFromHotkey("R").Should().Be(AnnotationToolType.Rectangle);
        AnnotationToolHotkeys.GetToolFromHotkey("E").Should().Be(AnnotationToolType.Ellipse);
        AnnotationToolHotkeys.GetToolFromHotkey("T").Should().Be(AnnotationToolType.Text);
        AnnotationToolHotkeys.GetToolFromHotkey("F").Should().Be(AnnotationToolType.Freehand);
        AnnotationToolHotkeys.GetToolFromHotkey("M").Should().Be(AnnotationToolType.Mosaic);
        AnnotationToolHotkeys.GetToolFromHotkey("J").Should().Be(AnnotationToolType.Emoji);
    }

    [Fact]
    public void GetToolFromHotkey_ShouldBeCaseInsensitive()
    {
        // Act & Assert
        AnnotationToolHotkeys.GetToolFromHotkey("s").Should().Be(AnnotationToolType.None); // None is used for selection tool
        AnnotationToolHotkeys.GetToolFromHotkey("S").Should().Be(AnnotationToolType.None);
        AnnotationToolHotkeys.GetToolFromHotkey("a").Should().Be(AnnotationToolType.Arrow);
        AnnotationToolHotkeys.GetToolFromHotkey("A").Should().Be(AnnotationToolType.Arrow);
    }

    [Fact]
    public void GetToolFromHotkey_WithInvalidHotkey_ShouldReturnNull()
    {
        // Act
        var result = AnnotationToolHotkeys.GetToolFromHotkey("X");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetToolFromHotkey_WithEmptyHotkey_ShouldReturnNull()
    {
        // Act
        var result = AnnotationToolHotkeys.GetToolFromHotkey("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetToolFromHotkey_WithNullHotkey_ShouldReturnNull()
    {
        // Act
        var result = AnnotationToolHotkeys.GetToolFromHotkey(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void IsToolHotkey_WithValidHotkeys_ShouldReturnTrue()
    {
        // Act & Assert
        AnnotationToolHotkeys.IsToolHotkey("S").Should().BeTrue();
        AnnotationToolHotkeys.IsToolHotkey("A").Should().BeTrue();
        AnnotationToolHotkeys.IsToolHotkey("R").Should().BeTrue();
        AnnotationToolHotkeys.IsToolHotkey("E").Should().BeTrue();
        AnnotationToolHotkeys.IsToolHotkey("T").Should().BeTrue();
        AnnotationToolHotkeys.IsToolHotkey("F").Should().BeTrue();
        AnnotationToolHotkeys.IsToolHotkey("M").Should().BeTrue();
        AnnotationToolHotkeys.IsToolHotkey("J").Should().BeTrue();
    }

    [Fact]
    public void IsToolHotkey_WithInvalidHotkey_ShouldReturnFalse()
    {
        // Act & Assert
        AnnotationToolHotkeys.IsToolHotkey("X").Should().BeFalse();
        AnnotationToolHotkeys.IsToolHotkey("1").Should().BeFalse();
        AnnotationToolHotkeys.IsToolHotkey("").Should().BeFalse();
        AnnotationToolHotkeys.IsToolHotkey(null!).Should().BeFalse();
    }

    [Fact]
    public void GetToolDescriptions_ShouldReturnAllToolDescriptions()
    {
        // Act
        var descriptions = AnnotationToolHotkeys.GetToolDescriptions();

        // Assert
        descriptions.Should().HaveCount(8);
        descriptions[AnnotationToolType.None].Should().Be("Select and edit annotation elements");
        descriptions[AnnotationToolType.Arrow].Should().Be("Draw pointing arrows");
        descriptions[AnnotationToolType.Rectangle].Should().Be("Draw rectangle frames");
        descriptions[AnnotationToolType.Ellipse].Should().Be("Draw ellipses");
        descriptions[AnnotationToolType.Text].Should().Be("Add text annotations");
        descriptions[AnnotationToolType.Freehand].Should().Be("Free drawing");
        descriptions[AnnotationToolType.Mosaic].Should().Be("Pixelate and blur regions");
        descriptions[AnnotationToolType.Emoji].Should().Be("Insert emoji symbols");
    }

    [Fact]
    public void ToolHotkeys_ShouldContainAllExpectedMappings()
    {
        // Act & Assert
        AnnotationToolHotkeys.ToolHotkeys.Should().HaveCount(8);
        AnnotationToolHotkeys.ToolHotkeys.Should().ContainKey(AnnotationToolType.None); // None is used for selection tool
        AnnotationToolHotkeys.ToolHotkeys.Should().ContainKey(AnnotationToolType.Arrow);
        AnnotationToolHotkeys.ToolHotkeys.Should().ContainKey(AnnotationToolType.Rectangle);
        AnnotationToolHotkeys.ToolHotkeys.Should().ContainKey(AnnotationToolType.Ellipse);
        AnnotationToolHotkeys.ToolHotkeys.Should().ContainKey(AnnotationToolType.Text);
        AnnotationToolHotkeys.ToolHotkeys.Should().ContainKey(AnnotationToolType.Freehand);
        AnnotationToolHotkeys.ToolHotkeys.Should().ContainKey(AnnotationToolType.Mosaic);
        AnnotationToolHotkeys.ToolHotkeys.Should().ContainKey(AnnotationToolType.Emoji);
    }
}
