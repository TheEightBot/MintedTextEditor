using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;

namespace MintedTextEditor.Core.Commands;

// ── Insert Hyperlink ──────────────────────────────────────────────────────────

/// <summary>
/// Inserts or wraps selected text as a hyperlink with the given URL.
/// Provide the <see cref="Url"/> (and optionally <see cref="DisplayText"/>) before calling
/// <see cref="Execute"/>. The insertion is a single undo step and leaves the caret at the end of the link.
/// </summary>
public sealed class InsertHyperlinkCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "InsertHyperlink";
    /// <inheritdoc/>
    public string Description => "Wraps the selected text (or inserts display text) as a hyperlink.";

    /// <summary>Set before calling Execute.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Optional text shown for the link. When non-empty (not null or whitespace), a link with this text
    /// is inserted at the caret, or replaces the selected text when there is a selection. When empty,
    /// the selected text becomes the link text, or the URL is inserted as the text when nothing is selected.
    /// </summary>
    public string DisplayText { get; set; } = string.Empty;

    /// <inheritdoc/>
    public void Execute(EditorContext ctx)
    {
        if (string.IsNullOrWhiteSpace(Url)) return;
        var action = new InsertHyperlinkAction(ctx.Document, ctx.Selection.Range, Url, DisplayText);
        var pos = ctx.UndoManager.Push(action);
        ctx.Selection.CollapseTo(pos);
    }

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => !string.IsNullOrWhiteSpace(Url);
}

// ── Remove Hyperlink ──────────────────────────────────────────────────────────

/// <summary>Removes the hyperlink at the caret position.</summary>
public sealed class RemoveHyperlinkCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "RemoveHyperlink";
    /// <inheritdoc/>
    public string Description => "Removes the hyperlink at the current caret position.";

    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        HyperlinkEngine.RemoveHyperlink(ctx.Document, ctx.Selection.Active);

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) =>
        HyperlinkEngine.GetHyperlinkAtPosition(ctx.Document, ctx.Selection.Active) is not null;
}

// ── Open Hyperlink ─────────────────────────────────────────────────────────────

/// <summary>
/// Raises <see cref="OnOpen"/> with the URL of the hyperlink at the caret.
/// </summary>
public sealed class OpenHyperlinkCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "OpenHyperlink";
    /// <inheritdoc/>
    public string Description => "Opens the URL of the hyperlink at the current caret position.";

    /// <summary>Called with the URL when the command executes.</summary>
    public Action<string>? OnOpen { get; set; }

    /// <inheritdoc/>
    public void Execute(EditorContext ctx)
    {
        var link = HyperlinkEngine.GetHyperlinkAtPosition(ctx.Document, ctx.Selection.Active);
        if (link is not null) OnOpen?.Invoke(link.Url);
    }

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) =>
        HyperlinkEngine.GetHyperlinkAtPosition(ctx.Document, ctx.Selection.Active) is not null;
}
