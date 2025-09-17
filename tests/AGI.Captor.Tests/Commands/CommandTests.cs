using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using AGI.Captor.Desktop.Commands;
using AGI.Captor.Desktop.Models;
using AGI.Captor.Desktop.Rendering;
using AGI.Captor.Tests.TestHelpers;
using NSubstitute;
using Avalonia.Controls;

namespace AGI.Captor.Tests.Commands;

public class CommandTests : TestBase
{
    public CommandTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void CommandManager_ShouldInitializeCorrectly()
    {
        // Act
        var commandManager = new CommandManager();

        // Assert
        commandManager.Should().NotBeNull();
        commandManager.CanUndo.Should().BeFalse();
        commandManager.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void CommandManager_ExecuteCommand_ShouldExecuteCommand()
    {
        // Arrange
        var commandManager = new CommandManager();
        var command = new TestCommand("Test Command");

        // Act
        commandManager.ExecuteCommand(command);

        // Assert
        command.IsExecuted.Should().BeTrue();
        commandManager.CanUndo.Should().BeTrue();
        commandManager.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void CommandManager_Undo_ShouldUndoLastCommand()
    {
        // Arrange
        var commandManager = new CommandManager();
        var command = new TestCommand("Test Command");
        commandManager.ExecuteCommand(command);

        // Act
        commandManager.Undo();

        // Assert
        command.IsExecuted.Should().BeFalse();
        commandManager.CanUndo.Should().BeFalse();
        commandManager.CanRedo.Should().BeTrue();
    }

    [Fact]
    public void CommandManager_Redo_ShouldRedoLastUndoneCommand()
    {
        // Arrange
        var commandManager = new CommandManager();
        var command = new TestCommand("Test Command");
        commandManager.ExecuteCommand(command);
        commandManager.Undo();

        // Act
        commandManager.Redo();

        // Assert
        command.IsExecuted.Should().BeTrue();
        commandManager.CanUndo.Should().BeTrue();
        commandManager.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void CommandManager_ExecuteMultipleCommands_ShouldMaintainHistory()
    {
        // Arrange
        var commandManager = new CommandManager();
        var command1 = new TestCommand("Command 1");
        var command2 = new TestCommand("Command 2");
        var command3 = new TestCommand("Command 3");

        // Act
        commandManager.ExecuteCommand(command1);
        commandManager.ExecuteCommand(command2);
        commandManager.ExecuteCommand(command3);

        // Assert
        command1.IsExecuted.Should().BeTrue();
        command2.IsExecuted.Should().BeTrue();
        command3.IsExecuted.Should().BeTrue();
        commandManager.CanUndo.Should().BeTrue();
        commandManager.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void CommandManager_UndoMultipleCommands_ShouldUndoInReverseOrder()
    {
        // Arrange
        var commandManager = new CommandManager();
        var command1 = new TestCommand("Command 1");
        var command2 = new TestCommand("Command 2");
        var command3 = new TestCommand("Command 3");

        commandManager.ExecuteCommand(command1);
        commandManager.ExecuteCommand(command2);
        commandManager.ExecuteCommand(command3);

        // Act
        commandManager.Undo();
        commandManager.Undo();
        commandManager.Undo();

        // Assert
        command1.IsExecuted.Should().BeFalse();
        command2.IsExecuted.Should().BeFalse();
        command3.IsExecuted.Should().BeFalse();
        commandManager.CanUndo.Should().BeFalse();
        commandManager.CanRedo.Should().BeTrue();
    }

    [Fact]
    public void CommandManager_RedoMultipleCommands_ShouldRedoInOrder()
    {
        // Arrange
        var commandManager = new CommandManager();
        var command1 = new TestCommand("Command 1");
        var command2 = new TestCommand("Command 2");
        var command3 = new TestCommand("Command 3");

        commandManager.ExecuteCommand(command1);
        commandManager.ExecuteCommand(command2);
        commandManager.ExecuteCommand(command3);
        commandManager.Undo();
        commandManager.Undo();
        commandManager.Undo();

        // Act
        commandManager.Redo();
        commandManager.Redo();
        commandManager.Redo();

        // Assert
        command1.IsExecuted.Should().BeTrue();
        command2.IsExecuted.Should().BeTrue();
        command3.IsExecuted.Should().BeTrue();
        commandManager.CanUndo.Should().BeTrue();
        commandManager.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void CommandManager_ExecuteNewCommandAfterUndo_ShouldClearRedoHistory()
    {
        // Arrange
        var commandManager = new CommandManager();
        var command1 = new TestCommand("Command 1");
        var command2 = new TestCommand("Command 2");
        var command3 = new TestCommand("Command 3");

        commandManager.ExecuteCommand(command1);
        commandManager.ExecuteCommand(command2);
        commandManager.Undo();
        commandManager.CanRedo.Should().BeTrue();

        // Act
        commandManager.ExecuteCommand(command3);

        // Assert
        command1.IsExecuted.Should().BeTrue();
        command2.IsExecuted.Should().BeFalse();
        command3.IsExecuted.Should().BeTrue();
        commandManager.CanUndo.Should().BeTrue();
        commandManager.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void CommandManager_ClearHistory_ShouldClearAllHistory()
    {
        // Arrange
        var commandManager = new CommandManager();
        var command1 = new TestCommand("Command 1");
        var command2 = new TestCommand("Command 2");

        commandManager.ExecuteCommand(command1);
        commandManager.ExecuteCommand(command2);
        commandManager.Undo();

        // Act
        commandManager.ClearHistory();

        // Assert
        commandManager.CanUndo.Should().BeFalse();
        commandManager.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void CommandManager_UndoWhenNoCommands_ShouldNotThrow()
    {
        // Arrange
        var commandManager = new CommandManager();

        // Act & Assert
        var action = () => commandManager.Undo();
        action.Should().NotThrow();
    }

    [Fact]
    public void CommandManager_RedoWhenNoCommands_ShouldNotThrow()
    {
        // Arrange
        var commandManager = new CommandManager();

        // Act & Assert
        var action = () => commandManager.Redo();
        action.Should().NotThrow();
    }

    [Fact]
    public void CommandManager_ExecuteNullCommand_ShouldThrowArgumentNullException()
    {
        // Arrange
        var commandManager = new CommandManager();

        // Act & Assert
        var action = () => commandManager.ExecuteCommand(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AnnotationCommands_AddAnnotationCommand_ShouldExecuteCorrectly()
    {
        // Arrange
        var manager = new AnnotationManager();
        var commandManager = new CommandManager();
        var annotation = new RectangleAnnotation(
            new Avalonia.Point(10, 20),
            new Avalonia.Point(110, 70),
            new AnnotationStyle());

        // Act
        var renderer = Substitute.For<IAnnotationRenderer>();
        var canvas = new Canvas();
        var command = new AddAnnotationCommand(manager, renderer, annotation, canvas);
        commandManager.ExecuteCommand(command);

        // Assert
        manager.Items.Should().HaveCount(1);
        manager.Items.First().Should().Be(annotation);
    }

    [Fact]
    public void AnnotationCommands_AddAnnotationCommand_ShouldUndoCorrectly()
    {
        // Arrange
        var manager = new AnnotationManager();
        var commandManager = new CommandManager();
        var annotation = new RectangleAnnotation(
            new Avalonia.Point(10, 20),
            new Avalonia.Point(110, 70),
            new AnnotationStyle());

        var renderer = Substitute.For<IAnnotationRenderer>();
        var canvas = new Canvas();
        var command = new AddAnnotationCommand(manager, renderer, annotation, canvas);
        commandManager.ExecuteCommand(command);
        manager.Items.Should().HaveCount(1);

        // Act
        commandManager.Undo();

        // Assert
        manager.Items.Should().BeEmpty();
    }

    [Fact]
    public void AnnotationCommands_RemoveAnnotationCommand_ShouldExecuteCorrectly()
    {
        // Arrange
        var manager = new AnnotationManager();
        var commandManager = new CommandManager();
        var annotation = new RectangleAnnotation(
            new Avalonia.Point(10, 20),
            new Avalonia.Point(110, 70),
            new AnnotationStyle());

        manager.AddItem(annotation);
        manager.Items.Should().HaveCount(1);

        // Act
        var renderer = Substitute.For<IAnnotationRenderer>();
        var canvas = new Canvas();
        var command = new RemoveAnnotationCommand(manager, renderer, annotation, canvas);
        commandManager.ExecuteCommand(command);

        // Assert
        manager.Items.Should().BeEmpty();
    }

    [Fact]
    public void AnnotationCommands_RemoveAnnotationCommand_ShouldUndoCorrectly()
    {
        // Arrange
        var manager = new AnnotationManager();
        var commandManager = new CommandManager();
        var annotation = new RectangleAnnotation(
            new Avalonia.Point(10, 20),
            new Avalonia.Point(110, 70),
            new AnnotationStyle());

        manager.AddItem(annotation);
        var renderer = Substitute.For<IAnnotationRenderer>();
        var canvas = new Canvas();
        var command = new RemoveAnnotationCommand(manager, renderer, annotation, canvas);
        commandManager.ExecuteCommand(command);
        manager.Items.Should().BeEmpty();

        // Act
        commandManager.Undo();

        // Assert
        manager.Items.Should().HaveCount(1);
        manager.Items.First().Should().Be(annotation);
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}

/// <summary>
/// Test command implementation for testing purposes
/// </summary>
public class TestCommand : ICommand
{
    public string Description { get; }
    public bool IsExecuted { get; private set; }
    public bool CanUndo => true;
    public DateTime Timestamp { get; }

    public TestCommand(string description)
    {
        Description = description;
        Timestamp = DateTime.Now;
    }

    public void Execute()
    {
        IsExecuted = true;
    }

    public void Undo()
    {
        IsExecuted = false;
    }
}
