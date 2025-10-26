using System;
using System.Collections.Generic;
using AGI.Kapster.Desktop.Services.Overlay.Coordinators;
using Avalonia;
using Avalonia.Platform;
using FluentAssertions;
using Xunit;

#pragma warning disable CA1416 // Validate platform compatibility - tests intentionally test platform-specific mappers

namespace AGI.Kapster.Tests.Services.Overlay;

/// <summary>
/// Tests for coordinate mapper implementations
/// Note: Full coordinate transformation testing requires real Screen objects
/// and is better suited for integration tests. These tests focus on basic behavior.
/// </summary>
public class CoordinateMapperTests
{
    #region WindowsCoordinateMapper Tests

    [Fact]
    public void WindowsMapper_CanInstantiate()
    {
        // Arrange & Act
        var mapper = new WindowsCoordinateMapper();

        // Assert
        mapper.Should().NotBeNull();
        mapper.Should().BeAssignableTo<IScreenCoordinateMapper>();
    }

    [Fact]
    public void WindowsMapper_GetScreenFromPoint_WithEmptyScreenList_ShouldReturnNull()
    {
        // Arrange
        var mapper = new WindowsCoordinateMapper();
        var screens = new List<Screen>();
        var point = new PixelPoint(100, 100);

        // Act
        var result = mapper.GetScreenFromPoint(point, screens);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void WindowsMapper_GetScreenFromPoint_WithNullScreenList_ShouldThrow()
    {
        // Arrange
        var mapper = new WindowsCoordinateMapper();
        var point = new PixelPoint(100, 100);

        // Act
        Action act = () => mapper.GetScreenFromPoint(point, null!);

        // Assert
        // Null parameter should throw NullReferenceException or ArgumentNullException
        act.Should().Throw<Exception>();
    }

    #endregion

    #region MacCoordinateMapper Tests

    [Fact]
    public void MacMapper_CanInstantiate()
    {
        // Arrange & Act
        var mapper = new MacCoordinateMapper();

        // Assert
        mapper.Should().NotBeNull();
        mapper.Should().BeAssignableTo<IScreenCoordinateMapper>();
    }

    [Fact]
    public void MacMapper_GetScreenFromPoint_WithEmptyScreenList_ShouldReturnNull()
    {
        // Arrange
        var mapper = new MacCoordinateMapper();
        var screens = new List<Screen>();
        var point = new PixelPoint(100, 100);

        // Act
        var result = mapper.GetScreenFromPoint(point, screens);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void MacMapper_GetScreenFromPoint_WithNullScreenList_ShouldThrow()
    {
        // Arrange
        var mapper = new MacCoordinateMapper();
        var point = new PixelPoint(100, 100);

        // Act
        Action act = () => mapper.GetScreenFromPoint(point, null!);

        // Assert
        // Null parameter should throw NullReferenceException or ArgumentNullException
        act.Should().Throw<Exception>();
    }

    #endregion

    #region Cross-Platform Tests

    [Fact]
    public void BothMappers_ShouldImplementInterface()
    {
        // Arrange & Act
        var windowsMapper = new WindowsCoordinateMapper();
        var macMapper = new MacCoordinateMapper();

        // Assert
        windowsMapper.Should().BeAssignableTo<IScreenCoordinateMapper>();
        macMapper.Should().BeAssignableTo<IScreenCoordinateMapper>();
    }

    [Fact]
    public void BothMappers_GetScreenFromPoint_WithEmptyList_ShouldBehaveConsistently()
    {
        // Arrange
        var windowsMapper = new WindowsCoordinateMapper();
        var macMapper = new MacCoordinateMapper();
        var emptyScreens = new List<Screen>();
        var point = new PixelPoint(100, 100);

        // Act
        var windowsResult = windowsMapper.GetScreenFromPoint(point, emptyScreens);
        var macResult = macMapper.GetScreenFromPoint(point, emptyScreens);

        // Assert
        windowsResult.Should().BeNull();
        macResult.Should().BeNull();
    }

    [Fact]
    public void WindowsMapper_MultipleInstances_ShouldBeIndependent()
    {
        // Arrange & Act
        var mapper1 = new WindowsCoordinateMapper();
        var mapper2 = new WindowsCoordinateMapper();

        // Assert
        mapper1.Should().NotBeSameAs(mapper2);
        mapper1.Should().BeOfType<WindowsCoordinateMapper>();
        mapper2.Should().BeOfType<WindowsCoordinateMapper>();
    }

    [Fact]
    public void MacMapper_MultipleInstances_ShouldBeIndependent()
    {
        // Arrange & Act
        var mapper1 = new MacCoordinateMapper();
        var mapper2 = new MacCoordinateMapper();

        // Assert
        mapper1.Should().NotBeSameAs(mapper2);
        mapper1.Should().BeOfType<MacCoordinateMapper>();
        mapper2.Should().BeOfType<MacCoordinateMapper>();
    }

    #endregion
}
