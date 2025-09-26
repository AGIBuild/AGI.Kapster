using System;
using System.Runtime.InteropServices;
using Xunit;
using FluentAssertions;
using AGI.Kapster.Desktop.Services.Update.Platforms;

namespace AGI.Kapster.Tests.Services.Update.Platforms;

/// <summary>
/// Tests for PlatformUpdateHelper functionality
/// </summary>
public class PlatformUpdateHelperTests
{
    [Fact]
    public void GetPackageExtension_ShouldReturnCorrectExtension()
    {
        // Act
        var extension = PlatformUpdateHelper.GetPackageExtension();

        // Assert
        extension.Should().NotBeNullOrEmpty();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            extension.Should().Be("msi");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            extension.Should().Be("pkg");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            extension.Should().Be("deb");
        }
    }

    [Fact]
    public void GetPlatformIdentifier_ShouldReturnCorrectIdentifier()
    {
        // Act
        var identifier = PlatformUpdateHelper.GetPlatformIdentifier();

        // Assert
        identifier.Should().NotBeNullOrEmpty();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            identifier.Should().StartWith("win-");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            identifier.Should().StartWith("osx-");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            identifier.Should().StartWith("linux-");
        }

        // Should include architecture
        identifier.Should().Match(s => s.EndsWith("-x64") || s.EndsWith("-arm64"));
    }

    [Fact]
    public void GetPlatformInfo_ShouldReturnBothIdentifierAndExtension()
    {
        // Act
        var (identifier, extension) = PlatformUpdateHelper.GetPlatformInfo();

        // Assert
        identifier.Should().NotBeNullOrEmpty();
        extension.Should().NotBeNullOrEmpty();

        // Verify consistency with individual methods
        identifier.Should().Be(PlatformUpdateHelper.GetPlatformIdentifier());
        extension.Should().Be(PlatformUpdateHelper.GetPackageExtension());
    }

    [Fact]
    public void SupportsSilentInstall_ShouldReturnCorrectValue()
    {
        // Act
        var supportsSilent = PlatformUpdateHelper.SupportsSilentInstall();

        // Assert
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            supportsSilent.Should().BeTrue();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            supportsSilent.Should().BeFalse();
        }
    }

    [Fact]
    public void GetInstallationNotes_ShouldReturnPlatformSpecificNotes()
    {
        // Act
        var notes = PlatformUpdateHelper.GetInstallationNotes();

        // Assert
        notes.Should().NotBeNullOrEmpty();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            notes.Should().Contain("silently");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            notes.Should().Contain("administrator password");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            notes.Should().Contain("package manager");
        }
    }

    [Fact]
    public void GetPlatformDisplayName_ShouldReturnReadableName()
    {
        // Act
        var displayName = PlatformUpdateHelper.GetPlatformDisplayName();

        // Assert
        displayName.Should().NotBeNullOrEmpty();
        displayName.Should().BeOneOf("Windows", "macOS", "Linux", "Unknown Platform");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            displayName.Should().Be("Windows");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            displayName.Should().Be("macOS");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            displayName.Should().Be("Linux");
        }
    }

    [Theory]
    [InlineData(Architecture.X64, "x64")]
    [InlineData(Architecture.Arm64, "arm64")]
    public void GetPlatformIdentifier_WithSpecificArchitecture_ShouldIncludeCorrectArch(Architecture arch, string expectedArch)
    {
        // This test verifies the logic but can't change the actual runtime architecture
        // So we test the expected behavior based on current architecture

        // Act
        var identifier = PlatformUpdateHelper.GetPlatformIdentifier();

        // Assert
        if (RuntimeInformation.ProcessArchitecture == arch)
        {
            identifier.Should().EndWith($"-{expectedArch}");
        }
    }
}