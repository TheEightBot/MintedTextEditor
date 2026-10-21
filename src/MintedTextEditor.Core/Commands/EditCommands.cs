using MintedTextEditor.Core.Document;

namespace MintedTextEditor.Core.Commands;

// ── Undo ─────────────────────────────────────────────────────────────────────

/// <summary>Undoes the most recent action recorded by the undo manager.</summary>
public sealed class UndoCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "Undo";
    /// <inheritdoc/>
    public string Description => "Undoes the last editor action.";

    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        ctx.UndoManager.Undo();

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) =>
        ctx.UndoManager.CanUndo;
}

// ── Redo ─────────────────────────────────────────────────────────────────────

/// <summary>Redoes the most recently undone action.</summary>
public sealed class RedoCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "Redo";
    /// <inheritdoc/>
    public string Description => "Redoes the previously undone action.";

    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        ctx.UndoManager.Redo();

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) =>
        ctx.UndoManager.CanRedo;
}

// ── Copy ─────────────────────────────────────────────────────────────────────

/// <summary>Copies the selected text to the clipboard.</summary>
public sealed class CopyCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "Copy";
    /// <inheritdoc/>
    public string Description => "Copies the selected text to the clipboard.";

    /// <inheritdoc/>
    public void Execute(EditorContext ctx)
    {
        if (ctx.Clipboard is null || ctx.Selection.IsEmpty) return;
        var text = DocumentEditor.GetSelectedText(ctx.Document, ctx.Selection.Range);
        ctx.Clipboard.SetTextAsync(text);
    }

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) =>
        ctx.Clipboard is not null && !ctx.Selection.IsEmpty;
}

// ── Cut ──────────────────────────────────────────────────────────────────────

/// <summary>Copies the selected text to the clipboard, then deletes it and collapses the selection to its start.</summary>
public sealed class CutCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "Cut";
    /// <inheritdoc/>
    public string Description => "Cuts the selected text to the clipboard.";

    /// <inheritdoc/>
    public void Execute(EditorContext ctx)
    {
        if (ctx.Clipboard is null || ctx.Selection.IsEmpty) return;
        var text = DocumentEditor.GetSelectedText(ctx.Document, ctx.Selection.Range);
        ctx.Clipboard.SetTextAsync(text);
        DocumentEditor.DeleteRange(ctx.Document, ctx.Selection.Range);
        ctx.Selection.CollapseTo(ctx.Selection.Range.Start);
    }

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) =>
        ctx.Clipboard is not null && !ctx.Selection.IsEmpty;
}

// ── SelectAll ─────────────────────────────────────────────────────────────────

/// <summary>Selects the entire document, from the start of the first block to the end of the last.</summary>
public sealed class SelectAllCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "SelectAll";
    /// <inheritdoc/>
    public string Description => "Selects all content in the document.";

    /// <inheritdoc/>
    public void Execute(EditorContext ctx)
    {
        var doc = ctx.Document;
        if (doc.Blocks.Count == 0) return;

        var start = new DocumentPosition(0, 0, 0);
        var lastBlockIdx = doc.Blocks.Count - 1;
        var end = new DocumentPosition(lastBlockIdx, 0, doc.Blocks[lastBlockIdx].Length);
        ctx.Selection.Set(start, end);
    }

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => ctx.Document.Blocks.Count > 0;
}
