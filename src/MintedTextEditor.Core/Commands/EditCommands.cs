using MintedTextEditor.Core.Document;

namespace MintedTextEditor.Core.Commands;

// ── Undo ─────────────────────────────────────────────────────────────────────

public sealed class UndoCommand : IEditorCommand
{
    public string Name => "Undo";
    public string Description => "Undoes the last editor action.";

    public void Execute(EditorContext ctx) =>
        ctx.UndoManager.Undo();

    public bool CanExecute(EditorContext ctx) =>
        ctx.UndoManager.CanUndo;
}

// ── Redo ─────────────────────────────────────────────────────────────────────

public sealed class RedoCommand : IEditorCommand
{
    public string Name => "Redo";
    public string Description => "Redoes the previously undone action.";

    public void Execute(EditorContext ctx) =>
        ctx.UndoManager.Redo();

    public bool CanExecute(EditorContext ctx) =>
        ctx.UndoManager.CanRedo;
}

// ── Copy ─────────────────────────────────────────────────────────────────────

public sealed class CopyCommand : IEditorCommand
{
    public string Name => "Copy";
    public string Description => "Copies the selected text to the clipboard.";

    public void Execute(EditorContext ctx)
    {
        if (ctx.Clipboard is null || ctx.Selection.IsEmpty) return;
        var text = DocumentEditor.GetSelectedText(ctx.Document, ctx.Selection.Range);
        ctx.Clipboard.SetTextAsync(text);
    }

    public bool CanExecute(EditorContext ctx) =>
        ctx.Clipboard is not null && !ctx.Selection.IsEmpty;
}

// ── Cut ──────────────────────────────────────────────────────────────────────

public sealed class CutCommand : IEditorCommand
{
    public string Name => "Cut";
    public string Description => "Cuts the selected text to the clipboard.";

    public void Execute(EditorContext ctx)
    {
        if (ctx.Clipboard is null || ctx.Selection.IsEmpty) return;
        var text = DocumentEditor.GetSelectedText(ctx.Document, ctx.Selection.Range);
        ctx.Clipboard.SetTextAsync(text);
        DocumentEditor.DeleteRange(ctx.Document, ctx.Selection.Range);
        ctx.Selection.CollapseTo(ctx.Selection.Range.Start);
    }

    public bool CanExecute(EditorContext ctx) =>
        ctx.Clipboard is not null && !ctx.Selection.IsEmpty;
}

// ── SelectAll ─────────────────────────────────────────────────────────────────

public sealed class SelectAllCommand : IEditorCommand
{
    public string Name => "SelectAll";
    public string Description => "Selects all content in the document.";

    public void Execute(EditorContext ctx)
    {
        var doc = ctx.Document;
        if (doc.Blocks.Count == 0) return;

        var start = new DocumentPosition(0, 0, 0);
        var lastBlockIdx = doc.Blocks.Count - 1;
        var end = new DocumentPosition(lastBlockIdx, 0, doc.Blocks[lastBlockIdx].Length);
        ctx.Selection.Set(start, end);
    }

    public bool CanExecute(EditorContext ctx) => ctx.Document.Blocks.Count > 0;
}
