using MintedTextEditor.Core.Document;

namespace MintedTextEditor.Core.Editing;

/// <summary>
/// Manages an undo / redo stack for editor operations.
/// </summary>
public sealed class UndoManager
{
    private readonly Stack<IUndoableAction> _undoStack = new();
    private readonly Stack<IUndoableAction> _redoStack = new();

    /// <summary>Maximum number of undo steps retained. Oldest steps are evicted when exceeded.</summary>
    public int MaxDepth { get; set; } = 100;

    /// <summary>Whether there are actions available to undo.</summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>Whether there are actions available to redo.</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Raised after any push, undo, or redo operation changes the stack state.</summary>
    public event EventHandler? UndoStackChanged;

    /// <summary>
    /// Executes <paramref name="action"/>, pushes it onto the undo stack, and clears the
    /// redo stack. If the action merges with the top of the undo stack, it is not pushed
    /// separately. Returns the resulting caret position.
    /// </summary>
    public DocumentPosition Push(IUndoableAction action)
    {
        var pos = action.Execute();

        // Try to merge with the most recent undo step
        if (_undoStack.Count > 0 && _undoStack.Peek().MergeWith(action))
        {
            // Merged — the top action already absorbed the new one
        }
        else
        {
            // Enforce max depth: if at capacity, drop the oldest entry (bottom of stack)
            if (_undoStack.Count >= MaxDepth)
            {
                var items = _undoStack.ToArray(); // index 0 = top/newest, last = bottom/oldest
                _undoStack.Clear();
                for (int i = items.Length - 2; i >= 0; i--)
                    _undoStack.Push(items[i]);
            }

            _undoStack.Push(action);
        }

        _redoStack.Clear();
        UndoStackChanged?.Invoke(this, EventArgs.Empty);
        return pos;
    }

    /// <summary>
    /// Undoes the most recent action. Returns the restored caret position, or
    /// <see langword="null"/> if the undo stack is empty.
    /// </summary>
    public DocumentPosition? Undo()
    {
        if (!CanUndo) return null;

        var action = _undoStack.Pop();
        var pos = action.Undo();
        _redoStack.Push(action);
        UndoStackChanged?.Invoke(this, EventArgs.Empty);
        return pos;
    }

    /// <summary>
    /// Redoes the most recently undone action. Returns the resulting caret position, or
    /// <see langword="null"/> if the redo stack is empty.
    /// </summary>
    public DocumentPosition? Redo()
    {
        if (!CanRedo) return null;

        var action = _redoStack.Pop();
        var pos = action.Redo();
        _undoStack.Push(action);
        UndoStackChanged?.Invoke(this, EventArgs.Empty);
        return pos;
    }

    /// <summary>Clears both the undo and redo stacks.</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        UndoStackChanged?.Invoke(this, EventArgs.Empty);
    }
}
