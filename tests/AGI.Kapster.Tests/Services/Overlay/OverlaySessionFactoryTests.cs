using AGI.Kapster.Desktop.Services.Overlay.State;
using FluentAssertions;
using Xunit;

namespace AGI.Kapster.Tests.Services.Overlay;

/// <summary>
/// Tests for OverlaySessionFactory
/// </summary>
public class OverlaySessionFactoryTests
{
    [Fact]
    public void CreateSession_ShouldReturnNewSession()
    {
        // Arrange
        var factory = new OverlaySessionFactory();

        // Act
        var session = factory.CreateSession();

        // Assert
        session.Should().NotBeNull();
        session.Should().BeAssignableTo<IOverlaySession>();
    }

    [Fact]
    public void CreateSession_ShouldReturnDifferentInstancesEachTime()
    {
        // Arrange
        var factory = new OverlaySessionFactory();

        // Act
        var session1 = factory.CreateSession();
        var session2 = factory.CreateSession();

        // Assert
        session1.Should().NotBeSameAs(session2);
    }

    [Fact]
    public void CreateSession_ShouldReturnSessionWithEmptyWindows()
    {
        // Arrange
        var factory = new OverlaySessionFactory();

        // Act
        var session = factory.CreateSession();

        // Assert
        session.Windows.Should().BeEmpty();
    }

    [Fact]
    public void CreateSession_MultipleCalls_ShouldAllReturnWorkingSessions()
    {
        // Arrange
        var factory = new OverlaySessionFactory();

        // Act & Assert
        for (int i = 0; i < 5; i++)
        {
            var session = factory.CreateSession();
            session.Should().NotBeNull();
            session.Windows.Should().BeEmpty();
        }
    }
}

