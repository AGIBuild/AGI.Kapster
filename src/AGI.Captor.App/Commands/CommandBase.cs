using System;

namespace AGI.Captor.App.Commands;

/// <summary>
/// Base class for commands providing common functionality
/// </summary>
public abstract class CommandBase : ICommand
{
    protected CommandBase(string description)
    {
        Description = description;
        Timestamp = DateTime.UtcNow;
    }
    
    public abstract void Execute();
    public abstract void Undo();
    
    public virtual bool CanUndo => true;
    
    public string Description { get; }
    
    public DateTime Timestamp { get; }
    
    public override string ToString() => $"{Description} ({Timestamp:HH:mm:ss})";
}
