using System;

namespace AGI.Captor.Desktop.Commands;

/// <summary>
/// Command interface for undo/redo operations
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Execute the command
    /// </summary>
    void Execute();
    
    /// <summary>
    /// Undo the command
    /// </summary>
    void Undo();
    
    /// <summary>
    /// Check if the command can be undone
    /// </summary>
    bool CanUndo { get; }
    
    /// <summary>
    /// Description of the command for UI display
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Timestamp when the command was created
    /// </summary>
    DateTime Timestamp { get; }
}
