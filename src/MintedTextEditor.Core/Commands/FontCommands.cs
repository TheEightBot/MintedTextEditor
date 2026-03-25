using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Commands;

// ── Apply Font Family ─────────────────────────────────────────────────────────

public sealed class ApplyFontFamilyCommand : IEditorCommand
{
    public string Name => "ApplyFontFamily";
    public string Description => "Applies a font family to the selected text.";

    public string FontFamily { get; set; } = string.Empty;

    public void Execute(EditorContext ctx)
    {
        if (string.IsNullOrEmpty(FontFamily)) return;
        ctx.FontFormattingEngine.ApplyFontFamily(ctx.Document, ctx.Selection.Range, FontFamily);
    }

    public bool CanExecute(EditorContext ctx) => !string.IsNullOrEmpty(FontFamily);
}

// ── Apply Font Size ───────────────────────────────────────────────────────────

public sealed class ApplyFontSizeCommand : IEditorCommand
{
    public string Name => "ApplyFontSize";
    public string Description => "Applies a font size to the selected text.";

    public float FontSize { get; set; }

    public void Execute(EditorContext ctx)
    {
        if (FontSize <= 0) return;
        ctx.FontFormattingEngine.ApplyFontSize(ctx.Document, ctx.Selection.Range, FontSize);
    }

    public bool CanExecute(EditorContext ctx) => FontSize > 0;
}

// ── Apply Text Color ──────────────────────────────────────────────────────────

public sealed class ApplyTextColorCommand : IEditorCommand
{
    public string Name => "ApplyTextColor";
    public string Description => "Applies a text colour to the selected text.";

    public EditorColor Color { get; set; } = EditorColor.Black;

    public void Execute(EditorContext ctx) =>
        ctx.FontFormattingEngine.ApplyTextColor(ctx.Document, ctx.Selection.Range, Color);

    public bool CanExecute(EditorContext ctx) => true;
}

// ── Apply Highlight Color ─────────────────────────────────────────────────────

public sealed class ApplyHighlightColorCommand : IEditorCommand
{
    public string Name => "ApplyHighlightColor";
    public string Description => "Applies a highlight colour to the selected text.";

    public EditorColor Color { get; set; } = EditorColor.Yellow;

    public void Execute(EditorContext ctx) =>
        ctx.FontFormattingEngine.ApplyHighlightColor(ctx.Document, ctx.Selection.Range, Color);

    public bool CanExecute(EditorContext ctx) => true;
}
