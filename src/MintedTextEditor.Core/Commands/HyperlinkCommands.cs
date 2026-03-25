using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;

namespace MintedTextEditor.Core.Commands;

// ── Insert Hyperlink ──────────────────────────────────────────────────────────

/// <summary>
/// Inserts or wraps selected text as a hyperlink with the given URL.
/// Provide the <see cref="Url"/> before calling <see cref="Execute"/>.
/// </summary>
public sealed class InsertHyperlinkCommand : IEditorCommand
{
    public string Name => "InsertHyperlink";
    public string Description => "Wraps the selected text (or inserts display text) as a hyperlink.";

    /// <summary>Set before calling Execute.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Optional display text. When empty, uses selected text or the URL.</summary>
    public string DisplayText { get; set; } = string.Empty;

    public void Execute(EditorContext ctx)
    {
        if (string.IsNullOrWhiteSpace(Url)) return;
        // Passes the current selection range — if there is selected text it becomes
        // the link text; otherwise the URL is used as the display text.
        var action = new InsertHyperlinkAction(ctx.Document, ctx.Selection.Range, Url);
        var pos = ctx.UndoManager.Push(action);
        ctx.Selection.CollapseTo(pos);
    }

    public bool CanExecute(EditorContext ctx) => !string.IsNullOrWhiteSpace(Url);
}

// ── Remove Hyperlink ──────────────────────────────────────────────────────────

public sealed class RemoveHyperlinkCommand : IEditorCommand
{
    public string Name => "RemoveHyperlink";
    public string Description => "Removes the hyperlink at the current caret position.";

    public void Execute(EditorContext ctx) =>
        HyperlinkEngine.RemoveHyperlink(ctx.Document, ctx.Selection.Active);

    public bool CanExecute(EditorContext ctx) =>
        HyperlinkEngine.GetHyperlinkAtPosition(ctx.Document, ctx.Selection.Active) is not null;
}

// ── Open Hyperlink ─────────────────────────────────────────────────────────────

/// <summary>
/// Raises <see cref="OnOpen"/> with the URL of the hyperlink at the caret.
/// </summary>
public sealed class OpenHyperlinkCommand : IEditorCommand
{
    public string Name => "OpenHyperlink";
    public string Description => "Opens the URL of the hyperlink at the current caret position.";

    /// <summary>Called with the URL when the command executes.</summary>
    public Action<string>? OnOpen { get; set; }

    public void Execute(EditorContext ctx)
    {
        var link = HyperlinkEngine.GetHyperlinkAtPosition(ctx.Document, ctx.Selection.Active);
        if (link is not null) OnOpen?.Invoke(link.Url);
    }

    public bool CanExecute(EditorContext ctx) =>
        HyperlinkEngine.GetHyperlinkAtPosition(ctx.Document, ctx.Selection.Active) is not null;
}
