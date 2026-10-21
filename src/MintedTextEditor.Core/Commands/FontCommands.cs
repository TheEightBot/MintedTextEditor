using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Commands;

// ── Apply Font Family ─────────────────────────────────────────────────────────

/// <summary>Applies <see cref="FontFamily"/> to the selected text.</summary>
public sealed class ApplyFontFamilyCommand : ICharacterFormattingCommand
{
    /// <inheritdoc/>
    public string Name => "ApplyFontFamily";
    /// <inheritdoc/>
    public string Description => "Applies a font family to the selected text.";

    /// <summary>Font family name to apply. The command does nothing while this is empty.</summary>
    public string FontFamily { get; set; } = string.Empty;

    /// <inheritdoc/>
    public void Execute(EditorContext ctx)
    {
        if (string.IsNullOrEmpty(FontFamily)) return;
        var family = FontFamily;
        ctx.ApplyCharacterFormatting("Font", r => ctx.FontFormattingEngine.ApplyFontFamily(ctx.Document, r, family));
    }

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => !string.IsNullOrEmpty(FontFamily);
}

// ── Apply Font Size ───────────────────────────────────────────────────────────

/// <summary>Applies <see cref="FontSize"/> to the selected text.</summary>
public sealed class ApplyFontSizeCommand : ICharacterFormattingCommand
{
    /// <inheritdoc/>
    public string Name => "ApplyFontSize";
    /// <inheritdoc/>
    public string Description => "Applies a font size to the selected text.";

    /// <summary>Font size to apply. The command does nothing unless this is greater than zero.</summary>
    public float FontSize { get; set; }

    /// <inheritdoc/>
    public void Execute(EditorContext ctx)
    {
        if (FontSize <= 0) return;
        var size = FontSize;
        ctx.ApplyCharacterFormatting("Font Size", r => ctx.FontFormattingEngine.ApplyFontSize(ctx.Document, r, size));
    }

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => FontSize > 0;
}

// ── Apply Text Color ──────────────────────────────────────────────────────────

/// <summary>Applies <see cref="Color"/> as the text colour of the selected text.</summary>
public sealed class ApplyTextColorCommand : ICharacterFormattingCommand
{
    /// <inheritdoc/>
    public string Name => "ApplyTextColor";
    /// <inheritdoc/>
    public string Description => "Applies a text colour to the selected text.";

    /// <summary>Text colour to apply. Defaults to black.</summary>
    public EditorColor Color { get; set; } = EditorColor.Black;

    /// <inheritdoc/>
    public void Execute(EditorContext ctx)
    {
        var color = Color;
        ctx.ApplyCharacterFormatting("Text Color", r => ctx.FontFormattingEngine.ApplyTextColor(ctx.Document, r, color));
    }

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => true;
}

// ── Apply Highlight Color ─────────────────────────────────────────────────────

/// <summary>Applies <see cref="Color"/> as the highlight colour of the selected text.</summary>
public sealed class ApplyHighlightColorCommand : ICharacterFormattingCommand
{
    /// <inheritdoc/>
    public string Name => "ApplyHighlightColor";
    /// <inheritdoc/>
    public string Description => "Applies a highlight colour to the selected text.";

    /// <summary>Highlight colour to apply. Defaults to yellow.</summary>
    public EditorColor Color { get; set; } = EditorColor.Yellow;

    /// <inheritdoc/>
    public void Execute(EditorContext ctx)
    {
        var color = Color;
        ctx.ApplyCharacterFormatting("Highlight Color", r => ctx.FontFormattingEngine.ApplyHighlightColor(ctx.Document, r, color));
    }

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => true;
}
