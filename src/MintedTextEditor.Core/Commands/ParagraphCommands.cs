using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Commands;

// ── Alignment ────────────────────────────────────────────────────────────────

/// <summary>Aligns the selected paragraphs to the left.</summary>
public sealed class AlignLeftCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "AlignLeft";
    /// <inheritdoc/>
    public string Description => "Aligns selected paragraphs to the left.";
    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.SetAlignment(ctx.Document, ctx.Selection.Range, TextAlignment.Left);
    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => true;
}

/// <summary>Centers the selected paragraphs.</summary>
public sealed class AlignCenterCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "AlignCenter";
    /// <inheritdoc/>
    public string Description => "Centers selected paragraphs.";
    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.SetAlignment(ctx.Document, ctx.Selection.Range, TextAlignment.Center);
    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => true;
}

/// <summary>Aligns the selected paragraphs to the right.</summary>
public sealed class AlignRightCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "AlignRight";
    /// <inheritdoc/>
    public string Description => "Aligns selected paragraphs to the right.";
    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.SetAlignment(ctx.Document, ctx.Selection.Range, TextAlignment.Right);
    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => true;
}

/// <summary>Justifies the selected paragraphs.</summary>
public sealed class AlignJustifyCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "AlignJustify";
    /// <inheritdoc/>
    public string Description => "Justifies selected paragraphs.";
    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.SetAlignment(ctx.Document, ctx.Selection.Range, TextAlignment.Justify);
    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => true;
}

// ── Lists ────────────────────────────────────────────────────────────────────

/// <summary>Toggles a bullet (unordered) list on the selected paragraphs.</summary>
public sealed class ToggleBulletListCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "ToggleBulletList";
    /// <inheritdoc/>
    public string Description => "Toggles a bullet (unordered) list on the selected paragraphs.";
    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.ToggleBulletList(ctx.Document, ctx.Selection.Range);
    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => true;
}

/// <summary>Toggles a numbered (ordered) list on the selected paragraphs.</summary>
public sealed class ToggleNumberListCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "ToggleNumberList";
    /// <inheritdoc/>
    public string Description => "Toggles a numbered (ordered) list on the selected paragraphs.";
    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.ToggleNumberList(ctx.Document, ctx.Selection.Range);
    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => true;
}

// ── Indent ───────────────────────────────────────────────────────────────────

/// <summary>Increases the indent level of the selected paragraphs.</summary>
public sealed class IncreaseIndentCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "IncreaseIndent";
    /// <inheritdoc/>
    public string Description => "Increases the indent level of the selected paragraphs.";
    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.IncreaseIndent(ctx.Document, ctx.Selection.Range);
    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => true;
}

/// <summary>Decreases the indent level of the selected paragraphs.</summary>
public sealed class DecreaseIndentCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "DecreaseIndent";
    /// <inheritdoc/>
    public string Description => "Decreases the indent level of the selected paragraphs.";
    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.DecreaseIndent(ctx.Document, ctx.Selection.Range);
    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => true;
}

// ── Heading / Paragraph Format ───────────────────────────────────────────────

/// <summary>Applies a fixed heading level to the selected paragraphs.</summary>
public sealed class SetHeadingLevelCommand : IEditorCommand
{
    private readonly int _level;

    /// <summary>Creates a command that applies the given heading level.</summary>
    /// <param name="level">Heading level to apply: 0 for body text, 1–6 for H1–H6 (values outside this range are clamped when applied).</param>
    public SetHeadingLevelCommand(int level) => _level = level;

    /// <inheritdoc/>
    public string Name => $"SetHeadingLevel{_level}";
    /// <inheritdoc/>
    public string Description => _level == 0
        ? "Clears the heading level, making the paragraph body text."
        : $"Applies Heading {_level} style to the selected paragraphs.";

    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.SetHeadingLevel(ctx.Document, ctx.Selection.Range, _level);

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => true;
}

/// <summary>Applies a fixed named paragraph format to the selected paragraphs.</summary>
public sealed class SetParagraphFormatCommand : IEditorCommand
{
    private readonly string _format;

    /// <summary>Creates a command that applies the given named paragraph format.</summary>
    /// <param name="format">Format name: "Normal", "Heading1"–"Heading6", or "Quote".</param>
    public SetParagraphFormatCommand(string format) => _format = format;

    /// <inheritdoc/>
    public string Name => $"SetParagraphFormat_{_format}";
    /// <inheritdoc/>
    public string Description => $"Applies the '{_format}' paragraph style to the selected paragraphs.";

    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        ParagraphFormattingEngine.SetParagraphFormat(ctx.Document, ctx.Selection.Range, _format);

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => true;
}
