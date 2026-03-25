using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Commands;

// ── Alignment ────────────────────────────────────────────────────────────────

public sealed class AlignLeftCommand : IEditorCommand
{
    public string Name => "AlignLeft";
    public string Description => "Aligns selected paragraphs to the left.";
    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.SetAlignment(ctx.Document, ctx.Selection.Range, TextAlignment.Left);
    public bool CanExecute(EditorContext ctx) => true;
}

public sealed class AlignCenterCommand : IEditorCommand
{
    public string Name => "AlignCenter";
    public string Description => "Centers selected paragraphs.";
    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.SetAlignment(ctx.Document, ctx.Selection.Range, TextAlignment.Center);
    public bool CanExecute(EditorContext ctx) => true;
}

public sealed class AlignRightCommand : IEditorCommand
{
    public string Name => "AlignRight";
    public string Description => "Aligns selected paragraphs to the right.";
    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.SetAlignment(ctx.Document, ctx.Selection.Range, TextAlignment.Right);
    public bool CanExecute(EditorContext ctx) => true;
}

public sealed class AlignJustifyCommand : IEditorCommand
{
    public string Name => "AlignJustify";
    public string Description => "Justifies selected paragraphs.";
    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.SetAlignment(ctx.Document, ctx.Selection.Range, TextAlignment.Justify);
    public bool CanExecute(EditorContext ctx) => true;
}

// ── Lists ────────────────────────────────────────────────────────────────────

public sealed class ToggleBulletListCommand : IEditorCommand
{
    public string Name => "ToggleBulletList";
    public string Description => "Toggles a bullet (unordered) list on the selected paragraphs.";
    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.ToggleBulletList(ctx.Document, ctx.Selection.Range);
    public bool CanExecute(EditorContext ctx) => true;
}

public sealed class ToggleNumberListCommand : IEditorCommand
{
    public string Name => "ToggleNumberList";
    public string Description => "Toggles a numbered (ordered) list on the selected paragraphs.";
    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.ToggleNumberList(ctx.Document, ctx.Selection.Range);
    public bool CanExecute(EditorContext ctx) => true;
}

// ── Indent ───────────────────────────────────────────────────────────────────

public sealed class IncreaseIndentCommand : IEditorCommand
{
    public string Name => "IncreaseIndent";
    public string Description => "Increases the indent level of the selected paragraphs.";
    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.IncreaseIndent(ctx.Document, ctx.Selection.Range);
    public bool CanExecute(EditorContext ctx) => true;
}

public sealed class DecreaseIndentCommand : IEditorCommand
{
    public string Name => "DecreaseIndent";
    public string Description => "Decreases the indent level of the selected paragraphs.";
    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.DecreaseIndent(ctx.Document, ctx.Selection.Range);
    public bool CanExecute(EditorContext ctx) => true;
}

// ── Heading / Paragraph Format ───────────────────────────────────────────────

public sealed class SetHeadingLevelCommand : IEditorCommand
{
    private readonly int _level;

    public SetHeadingLevelCommand(int level) => _level = level;

    public string Name => $"SetHeadingLevel{_level}";
    public string Description => _level == 0
        ? "Clears the heading level, making the paragraph body text."
        : $"Applies Heading {_level} style to the selected paragraphs.";

    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.SetHeadingLevel(ctx.Document, ctx.Selection.Range, _level);

    public bool CanExecute(EditorContext ctx) => true;
}

public sealed class SetParagraphFormatCommand : IEditorCommand
{
    private readonly string _format;

    public SetParagraphFormatCommand(string format) => _format = format;

    public string Name => $"SetParagraphFormat_{_format}";
    public string Description => $"Applies the '{_format}' paragraph style to the selected paragraphs.";

    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.SetParagraphFormat(ctx.Document, ctx.Selection.Range, _format);

    public bool CanExecute(EditorContext ctx) => true;
}
