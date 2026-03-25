using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Formatting;

namespace MintedTextEditor.Core.Commands;

// ── Toggle Bold ─────────────────────────────────────────────────────────────

public sealed class ToggleBoldCommand : IEditorCommand
{
    public string Name => "ToggleBold";
    public string Description => "Toggles bold formatting on the selected text.";

    public void Execute(EditorContext ctx) =>
        ctx.FormattingEngine.ToggleBold(ctx.Document, ctx.Selection.Range);

    public bool CanExecute(EditorContext ctx) => true;
}

// ── Toggle Italic ────────────────────────────────────────────────────────────

public sealed class ToggleItalicCommand : IEditorCommand
{
    public string Name => "ToggleItalic";
    public string Description => "Toggles italic formatting on the selected text.";

    public void Execute(EditorContext ctx) =>
        ctx.FormattingEngine.ToggleItalic(ctx.Document, ctx.Selection.Range);

    public bool CanExecute(EditorContext ctx) => true;
}

// ── Toggle Underline ─────────────────────────────────────────────────────────

public sealed class ToggleUnderlineCommand : IEditorCommand
{
    public string Name => "ToggleUnderline";
    public string Description => "Toggles underline formatting on the selected text.";

    public void Execute(EditorContext ctx) =>
        ctx.FormattingEngine.ToggleUnderline(ctx.Document, ctx.Selection.Range);

    public bool CanExecute(EditorContext ctx) => true;
}

// ── Toggle Strikethrough ─────────────────────────────────────────────────────

public sealed class ToggleStrikethroughCommand : IEditorCommand
{
    public string Name => "ToggleStrikethrough";
    public string Description => "Toggles strikethrough formatting on the selected text.";

    public void Execute(EditorContext ctx) =>
        ctx.FormattingEngine.ToggleStrikethrough(ctx.Document, ctx.Selection.Range);

    public bool CanExecute(EditorContext ctx) => true;
}

// ── Toggle Subscript ─────────────────────────────────────────────────────────

public sealed class ToggleSubscriptCommand : IEditorCommand
{
    public string Name => "ToggleSubscript";
    public string Description => "Toggles subscript formatting on the selected text.";

    public void Execute(EditorContext ctx) =>
        ctx.FormattingEngine.ToggleSubscript(ctx.Document, ctx.Selection.Range);

    public bool CanExecute(EditorContext ctx) => true;
}

// ── Toggle Superscript ───────────────────────────────────────────────────────

public sealed class ToggleSuperscriptCommand : IEditorCommand
{
    public string Name => "ToggleSuperscript";
    public string Description => "Toggles superscript formatting on the selected text.";

    public void Execute(EditorContext ctx) =>
        ctx.FormattingEngine.ToggleSuperscript(ctx.Document, ctx.Selection.Range);

    public bool CanExecute(EditorContext ctx) => true;
}

// ── Clear Formatting ─────────────────────────────────────────────────────────

public sealed class ClearFormattingCommand : IEditorCommand
{
    public string Name => "ClearFormatting";
    public string Description => "Removes all character formatting from the selected text.";

    public void Execute(EditorContext ctx) =>
        ctx.FormattingEngine.ClearFormatting(ctx.Document, ctx.Selection.Range);

    public bool CanExecute(EditorContext ctx) => true;
}
