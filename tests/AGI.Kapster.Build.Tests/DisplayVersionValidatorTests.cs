using Xunit;
using AGI.Kapster.Build.Versioning;

namespace AGI.Kapster.Build.Tests;

public class DisplayVersionValidatorTests
{
    [Fact]
    public void ValidVersion_ReturnsTrue()
    {
        var v = "2025.9.24.3600";
        Assert.True(DisplayVersionValidator.IsValid(v));
    }

    [Fact]
    public void InvalidFormat_ReturnsFalse()
    {
        var v = "2025.09.24.3600";
        Assert.False(DisplayVersionValidator.IsValid(v));
    }

    [Fact]
    public void OutOfRangeSeconds_ReturnsFalse()
    {
        var v = "2025.9.24.999999";
        Assert.False(DisplayVersionValidator.IsValid(v));
    }
}
