namespace MintedTextEditor.Core.Commands;

/// <summary>
/// Represents a single editor action that can be executed and inspected for availability.
/// </summary>
public interface IEditorCommand
{
    /// <summary>Unique name used to look up the command in the registry.</summary>
    string Name { get; }

    /// <summary>Human-readable description of what the command does.</summary>
    string Description { get; }

    /// <summary>Executes the command against the given context.</summary>
    void Execute(EditorContext context);

    /// <summary>Returns true when the command can currently be executed.</summary>
    bool CanExecute(EditorContext context);
}

/// <summary>
/// Marker interface for commands that toggle or apply inline character formatting
/// (bold, italic, underline, superscript, etc.) without intentionally relocating
/// the selection.  The editor uses this to distinguish formatting toggles from
/// commands that deliberately change the selection (e.g. <c>SelectAll</c>), so it
/// can capture the selection as absolute offsets before the operation, call
/// <see cref="Document.DocumentEditor.NormalizeInlines"/> after it, and then
/// rebuild the selection without drift.
/// </summary>
public interface ICharacterFormattingCommand : IEditorCommand { }
