using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using AGI.Captor.Desktop.Models;
using AGI.Captor.Desktop.Rendering;
using Serilog;

namespace AGI.Captor.Desktop.Commands;

/// <summary>
/// Command to add an annotation
/// </summary>
public class AddAnnotationCommand : CommandBase
{
    private readonly AnnotationManager _manager;
    private readonly IAnnotationRenderer _renderer;
    private readonly IAnnotationItem _annotation;
    private readonly Canvas _canvas;

    public AddAnnotationCommand(AnnotationManager manager, IAnnotationRenderer renderer, IAnnotationItem annotation, Canvas canvas)
        : base($"Add {annotation.Type}")
    {
        _manager = manager;
        _renderer = renderer;
        _annotation = annotation;
        _canvas = canvas;
    }

    public override void Execute()
    {
        _manager.AddItem(_annotation);
        _renderer.RenderAll(_canvas, new[] { _annotation });
        Log.Debug("Added annotation: {Type} {Id}", _annotation.Type, _annotation.Id);
    }

    public override void Undo()
    {
        _renderer.RemoveRender(_canvas, _annotation);
        _manager.RemoveItem(_annotation);
        Log.Debug("Removed annotation: {Type} {Id}", _annotation.Type, _annotation.Id);
    }
}

/// <summary>
/// Command to remove an annotation
/// </summary>
public class RemoveAnnotationCommand : CommandBase
{
    private readonly AnnotationManager _manager;
    private readonly IAnnotationRenderer _renderer;
    private readonly IAnnotationItem _annotation;
    private readonly Canvas _canvas;

    public RemoveAnnotationCommand(AnnotationManager manager, IAnnotationRenderer renderer, IAnnotationItem annotation, Canvas canvas)
        : base($"Remove {annotation.Type}")
    {
        _manager = manager;
        _renderer = renderer;
        _annotation = annotation;
        _canvas = canvas;
    }

    public override void Execute()
    {
        _renderer.RemoveRender(_canvas, _annotation);
        _manager.RemoveItem(_annotation);
        Log.Debug("Removed annotation: {Type} {Id}", _annotation.Type, _annotation.Id);
    }

    public override void Undo()
    {
        _manager.AddItem(_annotation);
        _renderer.RenderAll(_canvas, new[] { _annotation });
        Log.Debug("Restored annotation: {Type} {Id}", _annotation.Type, _annotation.Id);
    }
}

/// <summary>
/// Command to modify annotation properties
/// </summary>
public class ModifyAnnotationCommand : CommandBase
{
    private readonly IAnnotationRenderer _renderer;
    private readonly AnnotationManager _manager;
    private readonly IAnnotationItem _annotation;
    private readonly object _oldValue;
    private readonly object _newValue;
    private readonly string _propertyName;
    private readonly Action<object> _setter;
    private readonly Canvas _canvas;

    public ModifyAnnotationCommand(IAnnotationRenderer renderer, AnnotationManager manager, IAnnotationItem annotation,
        string propertyName, object oldValue, object newValue, Action<object> setter, Canvas canvas)
        : base($"Modify {annotation.Type} {propertyName}")
    {
        _renderer = renderer;
        _manager = manager;
        _annotation = annotation;
        _oldValue = oldValue;
        _newValue = newValue;
        _propertyName = propertyName;
        _setter = setter;
        _canvas = canvas;
    }

    public override void Execute()
    {
        _setter(_newValue);
        _renderer.RenderAll(_canvas, _manager.Items);
        Log.Debug("Modified annotation {Id} {Property}: {Old} -> {New}",
            _annotation.Id, _propertyName, _oldValue, _newValue);
    }

    public override void Undo()
    {
        _setter(_oldValue);
        _renderer.RenderAll(_canvas, _manager.Items);
        Log.Debug("Reverted annotation {Id} {Property}: {New} -> {Old}",
            _annotation.Id, _propertyName, _newValue, _oldValue);
    }
}

/// <summary>
/// Command to move annotations
/// </summary>
public class MoveAnnotationCommand : CommandBase
{
    private readonly IAnnotationRenderer _renderer;
    private readonly AnnotationManager _manager;
    private readonly IAnnotationItem _annotation;
    private readonly Avalonia.Vector _delta;
    private readonly Canvas _canvas;

    public MoveAnnotationCommand(IAnnotationRenderer renderer, AnnotationManager manager, IAnnotationItem annotation, Avalonia.Vector delta, Canvas canvas)
        : base($"Move {annotation.Type}")
    {
        _renderer = renderer;
        _manager = manager;
        _annotation = annotation;
        _delta = delta;
        _canvas = canvas;
    }

    public override void Execute()
    {
        _annotation.Move(_delta);
        _renderer.RenderAll(_canvas, _manager.Items);
        Log.Debug("Moved annotation {Id} by {Delta}", _annotation.Id, _delta);
    }

    public override void Undo()
    {
        _annotation.Move(-_delta);
        _renderer.RenderAll(_canvas, _manager.Items);
        Log.Debug("Reverted move of annotation {Id} by {Delta}", _annotation.Id, -_delta);
    }
}

/// <summary>
/// Command to transform (scale/rotate) annotations
/// </summary>
public class TransformAnnotationCommand : CommandBase
{
    private readonly IAnnotationRenderer _renderer;
    private readonly AnnotationManager _manager;
    private readonly IAnnotationItem _annotation;
    private readonly double _scaleFactor;
    private readonly Avalonia.Point _center;
    private readonly Canvas _canvas;

    public TransformAnnotationCommand(IAnnotationRenderer renderer, AnnotationManager manager, IAnnotationItem annotation,
        double scaleFactor, Avalonia.Point center, Canvas canvas)
        : base($"Scale {annotation.Type}")
    {
        _renderer = renderer;
        _manager = manager;
        _annotation = annotation;
        _scaleFactor = scaleFactor;
        _center = center;
        _canvas = canvas;
    }

    public override void Execute()
    {
        _annotation.Scale(_scaleFactor, _center);
        _renderer.RenderAll(_canvas, _manager.Items);
        Log.Debug("Scaled annotation {Id} by {Factor} around {Center}", _annotation.Id, _scaleFactor, _center);
    }

    public override void Undo()
    {
        _annotation.Scale(1.0 / _scaleFactor, _center);
        _renderer.RenderAll(_canvas, _manager.Items);
        Log.Debug("Reverted scale of annotation {Id} by {Factor} around {Center}",
            _annotation.Id, 1.0 / _scaleFactor, _center);
    }
}

/// <summary>
/// Command to set both endpoints of an arrow (supports undo)
/// </summary>
public class SetArrowEndpointsCommand : CommandBase
{
    private readonly IAnnotationRenderer _renderer;
    private readonly AnnotationManager _manager;
    private readonly ArrowAnnotation _arrow;
    private readonly Avalonia.Point _oldStart;
    private readonly Avalonia.Point _oldEnd;
    private readonly Avalonia.Point _newStart;
    private readonly Avalonia.Point _newEnd;
    private readonly Canvas _canvas;

    public SetArrowEndpointsCommand(IAnnotationRenderer renderer, AnnotationManager manager, ArrowAnnotation arrow,
        Avalonia.Point oldStart, Avalonia.Point oldEnd, Avalonia.Point newStart, Avalonia.Point newEnd, Canvas canvas)
        : base($"Set arrow endpoints")
    {
        _renderer = renderer;
        _manager = manager;
        _arrow = arrow;
        _oldStart = oldStart;
        _oldEnd = oldEnd;
        _newStart = newStart;
        _newEnd = newEnd;
        _canvas = canvas;
    }

    public override void Execute()
    {
        _arrow.StartPoint = _newStart;
        _arrow.EndPoint = _newEnd;
        _renderer.RenderAll(_canvas, _manager.Items);
        Log.Debug("Arrow endpoints set: {Id} Start={Start} End={End}", _arrow.Id, _newStart, _newEnd);
    }

    public override void Undo()
    {
        _arrow.StartPoint = _oldStart;
        _arrow.EndPoint = _oldEnd;
        _renderer.RenderAll(_canvas, _manager.Items);
        Log.Debug("Arrow endpoints reverted: {Id} Start={Start} End={End}", _arrow.Id, _oldStart, _oldEnd);
    }
}

/// <summary>
/// Composite command to execute multiple commands as one unit
/// </summary>
public class CompositeCommand : CommandBase
{
    private readonly List<ICommand> _commands;

    public CompositeCommand(string description, IEnumerable<ICommand> commands)
        : base(description)
    {
        _commands = commands.ToList();
    }

    public override void Execute()
    {
        foreach (var command in _commands)
        {
            command.Execute();
        }
        Log.Debug("Executed composite command: {Description} ({Count} commands)", Description, _commands.Count);
    }

    public override void Undo()
    {
        // Undo in reverse order
        for (int i = _commands.Count - 1; i >= 0; i--)
        {
            _commands[i].Undo();
        }
        Log.Debug("Undone composite command: {Description} ({Count} commands)", Description, _commands.Count);
    }

    public override bool CanUndo => _commands.All(c => c.CanUndo);
}
