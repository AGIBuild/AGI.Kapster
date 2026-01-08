using AGI.Kapster.Desktop.Services.Telemetry;
using FluentAssertions;
using Xunit;

namespace AGI.Kapster.Tests.Services.Telemetry;

/// <summary>
/// Tests for EnvironmentInfo utility class
/// </summary>
public class EnvironmentInfoTests
{
    [Fact]
    public void GetProperties_ShouldReturnNonEmptyDictionary()
    {
        // Act
        var props = EnvironmentInfo.GetProperties();

        // Assert
        props.Should().NotBeNull();
        props.Should().NotBeEmpty();
    }

    [Fact]
    public void GetProperties_ShouldContainOsPlatform()
    {
        // Act
        var props = EnvironmentInfo.GetProperties();

        // Assert
        props.Should().ContainKey("os_platform");
        props["os_platform"].Should().NotBeNullOrEmpty();
        props["os_platform"].Should().BeOneOf("Windows", "macOS", "Linux", "Unknown");
    }

    [Fact]
    public void GetProperties_ShouldContainOsVersion()
    {
        // Act
        var props = EnvironmentInfo.GetProperties();

        // Assert
        props.Should().ContainKey("os_version");
        props["os_version"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetProperties_ShouldContainDotnetVersion()
    {
        // Act
        var props = EnvironmentInfo.GetProperties();

        // Assert
        props.Should().ContainKey("dotnet_version");
        props["dotnet_version"].Should().NotBeNullOrEmpty();
        props["dotnet_version"].Should().Contain(".NET");
    }

    [Fact]
    public void GetProperties_ShouldContainArchitecture()
    {
        // Act
        var props = EnvironmentInfo.GetProperties();

        // Assert
        props.Should().ContainKey("os_architecture");
        props.Should().ContainKey("process_architecture");
    }

    [Fact]
    public void GetProperties_ShouldContainAppVersion()
    {
        // Act
        var props = EnvironmentInfo.GetProperties();

        // Assert
        props.Should().ContainKey("app_version");
        props["app_version"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetProperties_ShouldContainProcessorCount()
    {
        // Act
        var props = EnvironmentInfo.GetProperties();

        // Assert
        props.Should().ContainKey("processor_count");
        int.Parse(props["processor_count"]).Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetSummary_ShouldReturnNonEmptyString()
    {
        // Act
        var summary = EnvironmentInfo.GetSummary();

        // Assert
        summary.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetSummary_ShouldContainPlatform()
    {
        // Act
        var summary = EnvironmentInfo.GetSummary();

        // Assert
        summary.Should().ContainAny("Windows", "macOS", "Linux");
    }

    [Fact]
    public void GetSummary_ShouldContainDotNet()
    {
        // Act
        var summary = EnvironmentInfo.GetSummary();

        // Assert
        summary.Should().Contain(".NET");
    }

    [Fact]
    public void GetProperties_ShouldBeIdempotent()
    {
        // Act
        var props1 = EnvironmentInfo.GetProperties();
        var props2 = EnvironmentInfo.GetProperties();

        // Assert - Same keys should be present
        props1.Keys.Should().BeEquivalentTo(props2.Keys);

        // Static properties should have same values
        props1["os_platform"].Should().Be(props2["os_platform"]);
        props1["dotnet_version"].Should().Be(props2["dotnet_version"]);
        props1["processor_count"].Should().Be(props2["processor_count"]);
    }
}
