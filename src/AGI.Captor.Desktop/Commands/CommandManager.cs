using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace AGI.Captor.Desktop.Commands;

/// <summary>
/// Command manager for undo/redo operations
/// </summary>
public class CommandManager
{
    private readonly Stack<ICommand> _undoStack = new();
    private readonly Stack<ICommand> _redoStack = new();
    private readonly int _maxUndoLevels;

    public CommandManager(int maxUndoLevels = 50)
    {
        _maxUndoLevels = maxUndoLevels;
    }

    /// <summary>
    /// Execute a command and add it to the undo stack
    /// </summary>
    public void ExecuteCommand(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            command.Execute();

            // Add to undo stack
            _undoStack.Push(command);

            // Clear redo stack when new command is executed
            _redoStack.Clear();

            // Limit undo stack size
            while (_undoStack.Count > _maxUndoLevels)
            {
                var oldest = _undoStack.ToArray().Last();
                var temp = new Stack<ICommand>();

                // Remove the oldest command
                while (_undoStack.Count > 1)
                {
                    temp.Push(_undoStack.Pop());
                }
                _undoStack.Pop(); // Remove oldest

                // Restore stack
                while (temp.Count > 0)
                {
                    _undoStack.Push(temp.Pop());
                }
            }

            Log.Debug("Executed command: {Command}", command.Description);
            OnStackChanged();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to execute command: {Command}", command.Description);
            throw;
        }
    }

    /// <summary>
    /// Undo the last command
    /// </summary>
    public bool Undo()
    {
        if (!CanUndo)
            return false;

        try
        {
            var command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);

            Log.Debug("Undone command: {Command}", command.Description);
            OnStackChanged();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to undo command");
            return false;
        }
    }

    /// <summary>
    /// Redo the last undone command
    /// </summary>
    public bool Redo()
    {
        if (!CanRedo)
            return false;

        try
        {
            var command = _redoStack.Pop();
            command.Execute();
            _undoStack.Push(command);

            Log.Debug("Redone command: {Command}", command.Description);
            OnStackChanged();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to redo command");
            return false;
        }
    }

    /// <summary>
    /// Check if undo is available
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0 && _undoStack.Peek().CanUndo;

    /// <summary>
    /// Check if redo is available
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Get the description of the next undo command
    /// </summary>
    public string? UndoDescription => CanUndo ? _undoStack.Peek().Description : null;

    /// <summary>
    /// Get the description of the next redo command
    /// </summary>
    public string? RedoDescription => CanRedo ? _redoStack.Peek().Description : null;

    /// <summary>
    /// Clear all commands
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        Log.Debug("Command history cleared");
        OnStackChanged();
    }

    /// <summary>
    /// Clear all commands (alias for Clear method)
    /// </summary>
    public void ClearHistory() => Clear();

    /// <summary>
    /// Get undo history for debugging
    /// </summary>
    public IEnumerable<string> GetUndoHistory() => _undoStack.Select(c => c.Description);

    /// <summary>
    /// Get redo history for debugging
    /// </summary>
    public IEnumerable<string> GetRedoHistory() => _redoStack.Select(c => c.Description);

    /// <summary>
    /// Event fired when the command stacks change
    /// </summary>
    public event Action? StackChanged;

    private void OnStackChanged() => StackChanged?.Invoke();
}
