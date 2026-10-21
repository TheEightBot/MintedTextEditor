using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Formatting;

namespace MintedTextEditor.Core.Commands;

// ── Toggle Bold ─────────────────────────────────────────────────────────────

/// <summary>Toggles bold formatting on the selected text.</summary>
public sealed class ToggleBoldCommand : ICharacterFormattingCommand
{
    /// <inheritdoc/>
    public string Name => "ToggleBold";
    /// <inheritdoc/>
    public string Description => "Toggles bold formatting on the selected text.";

    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        ctx.ApplyCharacterFormatting("Bold", r => ctx.FormattingEngine.ToggleBold(ctx.Document, r));

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => true;
}

// ── Toggle Italic ────────────────────────────────────────────────────────────

/// <summary>Toggles italic formatting on the selected text.</summary>
public sealed class ToggleItalicCommand : ICharacterFormattingCommand
{
    /// <inheritdoc/>
    public string Name => "ToggleItalic";
    /// <inheritdoc/>
    public string Description => "Toggles italic formatting on the selected text.";

    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        ctx.ApplyCharacterFormatting("Italic", r => ctx.FormattingEngine.ToggleItalic(ctx.Document, r));

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => true;
}

// ── Toggle Underline ─────────────────────────────────────────────────────────

/// <summary>Toggles underline formatting on the selected text.</summary>
public sealed class ToggleUnderlineCommand : ICharacterFormattingCommand
{
    /// <inheritdoc/>
    public string Name => "ToggleUnderline";
    /// <inheritdoc/>
    public string Description => "Toggles underline formatting on the selected text.";

    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        ctx.ApplyCharacterFormatting("Underline", r => ctx.FormattingEngine.ToggleUnderline(ctx.Document, r));

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => true;
}

// ── Toggle Strikethrough ─────────────────────────────────────────────────────

/// <summary>Toggles strikethrough formatting on the selected text.</summary>
public sealed class ToggleStrikethroughCommand : ICharacterFormattingCommand
{
    /// <inheritdoc/>
    public string Name => "ToggleStrikethrough";
    /// <inheritdoc/>
    public string Description => "Toggles strikethrough formatting on the selected text.";

    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        ctx.ApplyCharacterFormatting("Strikethrough", r => ctx.FormattingEngine.ToggleStrikethrough(ctx.Document, r));

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => true;
}

// ── Toggle Subscript ─────────────────────────────────────────────────────────

/// <summary>Toggles subscript formatting on the selected text.</summary>
public sealed class ToggleSubscriptCommand : ICharacterFormattingCommand
{
    /// <inheritdoc/>
    public string Name => "ToggleSubscript";
    /// <inheritdoc/>
    public string Description => "Toggles subscript formatting on the selected text.";

    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        ctx.ApplyCharacterFormatting("Subscript", r => ctx.FormattingEngine.ToggleSubscript(ctx.Document, r));

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => true;
}

// ── Toggle Superscript ───────────────────────────────────────────────────────

/// <summary>Toggles superscript formatting on the selected text.</summary>
public sealed class ToggleSuperscriptCommand : ICharacterFormattingCommand
{
    /// <inheritdoc/>
    public string Name => "ToggleSuperscript";
    /// <inheritdoc/>
    public string Description => "Toggles superscript formatting on the selected text.";

    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        ctx.ApplyCharacterFormatting("Superscript", r => ctx.FormattingEngine.ToggleSuperscript(ctx.Document, r));

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => true;
}

// ── Clear Formatting ─────────────────────────────────────────────────────────

/// <summary>Removes all character formatting from the selected text.</summary>
public sealed class ClearFormattingCommand : ICharacterFormattingCommand
{
    /// <inheritdoc/>
    public string Name => "ClearFormatting";
    /// <inheritdoc/>
    public string Description => "Removes all character formatting from the selected text.";

    /// <inheritdoc/>
    public void Execute(EditorContext ctx) =>
        ctx.ApplyCharacterFormatting("Clear Formatting", r => ctx.FormattingEngine.ClearFormatting(ctx.Document, r));

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => true;
}
