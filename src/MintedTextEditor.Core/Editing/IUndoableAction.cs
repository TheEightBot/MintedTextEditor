using MintedTextEditor.Core.Document;

namespace MintedTextEditor.Core.Editing;

/// <summary>
/// Represents a reversible editor action that can be pushed onto an undo stack.
/// </summary>
public interface IUndoableAction
{
    /// <summary>Human-readable description of this action (e.g. for a history panel).</summary>
    string Description { get; }

    /// <summary>Performs the action and returns the resulting caret position.</summary>
    DocumentPosition Execute();

    /// <summary>Reverses the action and returns the caret position to restore.</summary>
    DocumentPosition Undo();

    /// <summary>Re-applies the action after an undo and returns the resulting caret position.</summary>
    DocumentPosition Redo();

    /// <summary>
    /// Attempts to merge <paramref name="next"/> into this action (e.g. consecutive character
    /// inserts). Returns <see langword="true"/> if the merge succeeded, in which case
    /// <paramref name="next"/> should NOT be pushed separately.
    /// </summary>
    bool MergeWith(IUndoableAction next);
}
