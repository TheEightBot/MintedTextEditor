using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Events;

/// <summary>
/// Snapshot of the formatting at the caret or selection, as reported by the editor's
/// formatting "changed" events.
/// </summary>
/// <remarks>
/// For a non-empty selection, bold, italic, underline, strikethrough, subscript and superscript
/// are true only when applied to the whole selection (as the toolbar shows them); font family,
/// size and colours are the selection's common value, or the value at its start when mixed.
/// For a caret, every value comes from the pending style (formatting chosen with nothing
/// selected, applied to the next typed text) when there is one, otherwise from the text run at
/// the caret. Paragraph values come from the paragraph at the start of the selection.
/// </remarks>
public sealed record FormattingState
{
    /// <summary>The state of an empty document: default text and paragraph style.</summary>
    public static FormattingState Default { get; } = FromStyles(TextStyle.Default, ParagraphStyle.Default);

    /// <summary>Font family name.</summary>
    public string FontFamily { get; init; } = TextStyle.Default.FontFamily;
    /// <summary>Font size.</summary>
    public float FontSize { get; init; } = TextStyle.Default.FontSize;
    /// <summary>Whether bold is applied.</summary>
    public bool IsBold { get; init; }
    /// <summary>Whether italic is applied.</summary>
    public bool IsItalic { get; init; }
    /// <summary>Whether subscript is applied.</summary>
    public bool IsSubscript { get; init; }
    /// <summary>Whether superscript is applied.</summary>
    public bool IsSuperscript { get; init; }
    /// <summary>Whether underline is applied.</summary>
    public bool IsUnderline { get; init; }
    /// <summary>Whether strikethrough is applied.</summary>
    public bool IsStrikethrough { get; init; }
    /// <summary>Text colour.</summary>
    public EditorColor TextColor { get; init; } = TextStyle.Default.TextColor;
    /// <summary>Highlight (background) colour; transparent when none.</summary>
    public EditorColor HighlightColor { get; init; } = TextStyle.Default.HighlightColor;
    /// <summary>Heading level of the paragraph: 0 = body text, 1–6 = heading levels.</summary>
    public int HeadingLevel { get; init; }
    /// <summary>Whether the paragraph is a block quote.</summary>
    public bool IsBlockQuote { get; init; }
    /// <summary>Horizontal alignment of the paragraph.</summary>
    public TextAlignment Alignment { get; init; } = TextAlignment.Left;
    /// <summary>List type of the paragraph.</summary>
    public ListType ListType { get; init; } = ListType.None;

    /// <summary>
    /// The paragraph format name, as accepted by <see cref="ParagraphFormattingEngine.SetParagraphFormat"/>:
    /// "Normal", "Heading1"–"Heading6" or "Quote".
    /// </summary>
    public string ParagraphFormat =>
        HeadingLevel is >= 1 and <= 6 ? $"Heading{HeadingLevel}"
        : IsBlockQuote ? "Quote"
        : "Normal";

    /// <summary>
    /// Computes the formatting state for <paramref name="range"/> in <paramref name="document"/>.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="range">The current selection; an empty range is a caret.</param>
    /// <param name="pendingStyle">
    /// Style that will be applied to the next typed text, if any. Used only when
    /// <paramref name="range"/> is empty.
    /// </param>
    public static FormattingState Compute(Document.Document document, TextRange range, TextStyle? pendingStyle = null)
    {
        var paragraph = FindParagraph(document, range.Start);
        var paraStyle = paragraph?.Style ?? ParagraphStyle.Default;

        if (range.IsEmpty)
        {
            var caretStyle = pendingStyle ?? GetRunStyle(paragraph, range.Start);
            return FromStyles(caretStyle, paraStyle);
        }

        var style = FontFormattingEngine.GetTextStyleForRange(document, range)
                    ?? GetRunStyle(paragraph, range.Start);
        return FromStyles(style, paraStyle) with
        {
            IsBold          = FormattingEngine.IsAppliedToEntireRange(document, range, s => s.IsBold),
            IsItalic        = FormattingEngine.IsAppliedToEntireRange(document, range, s => s.IsItalic),
            IsUnderline     = FormattingEngine.IsAppliedToEntireRange(document, range, s => s.IsUnderline),
            IsStrikethrough = FormattingEngine.IsAppliedToEntireRange(document, range, s => s.IsStrikethrough),
            IsSubscript     = FormattingEngine.IsAppliedToEntireRange(document, range, s => s.IsSubscript),
            IsSuperscript   = FormattingEngine.IsAppliedToEntireRange(document, range, s => s.IsSuperscript),
        };
    }

    private static FormattingState FromStyles(TextStyle text, ParagraphStyle paragraph) => new()
    {
        FontFamily      = text.FontFamily,
        FontSize        = text.FontSize,
        IsBold          = text.IsBold,
        IsItalic        = text.IsItalic,
        IsSubscript     = text.IsSubscript,
        IsSuperscript   = text.IsSuperscript,
        IsUnderline     = text.IsUnderline,
        IsStrikethrough = text.IsStrikethrough,
        TextColor       = text.TextColor,
        HighlightColor  = text.HighlightColor,
        HeadingLevel    = paragraph.HeadingLevel,
        IsBlockQuote    = paragraph.IsBlockQuote,
        Alignment       = paragraph.Alignment,
        ListType        = paragraph.ListType,
    };

    private static Paragraph? FindParagraph(Document.Document document, DocumentPosition position)
    {
        if (position.BlockIndex < 0 || position.BlockIndex >= document.Blocks.Count)
            return null;

        var block = document.Blocks[position.BlockIndex];
        if (position.IsInTableCell)
        {
            var cell = (block as TableBlock)?.GetCell(position.CellRow, position.CellCol);
            return cell is { Blocks.Count: > 0 } ? cell.Blocks[0] as Paragraph : null;
        }

        return block as Paragraph;
    }

    private static TextStyle GetRunStyle(Paragraph? paragraph, DocumentPosition position)
    {
        if (paragraph is null || paragraph.Inlines.Count == 0)
            return TextStyle.Default;

        int ii = Math.Clamp(position.InlineIndex, 0, paragraph.Inlines.Count - 1);
        return paragraph.Inlines[ii] switch
        {
            TextRun run => run.Style,
            HyperlinkInline link => link.Children.OfType<TextRun>().FirstOrDefault()?.Style ?? TextStyle.Default,
            _ => TextStyle.Default,
        };
    }
}

/// <summary>Which groups of <see cref="FormattingState"/> values differ between two states.</summary>
[Flags]
public enum FormattingStateChanges
{
    /// <summary>Nothing changed.</summary>
    None = 0,
    /// <summary><see cref="FormattingState.FontFamily"/> changed.</summary>
    FontFamily = 1 << 0,
    /// <summary><see cref="FormattingState.FontSize"/> changed.</summary>
    FontSize = 1 << 1,
    /// <summary>Bold, italic, subscript or superscript changed.</summary>
    FontAttributes = 1 << 2,
    /// <summary>Underline or strikethrough changed.</summary>
    TextDecorations = 1 << 3,
    /// <summary><see cref="FormattingState.ParagraphFormat"/> (heading level or block quote) changed.</summary>
    ParagraphFormat = 1 << 4,
    /// <summary><see cref="FormattingState.Alignment"/> changed.</summary>
    Alignment = 1 << 5,
    /// <summary><see cref="FormattingState.ListType"/> changed.</summary>
    ListType = 1 << 6,
    /// <summary><see cref="FormattingState.TextColor"/> changed.</summary>
    TextColor = 1 << 7,
    /// <summary><see cref="FormattingState.HighlightColor"/> changed.</summary>
    HighlightColor = 1 << 8,
}

/// <summary>
/// Remembers the last <see cref="FormattingState"/> and reports which parts of it change, so
/// the editor raises its formatting "changed" events only on an actual change.
/// </summary>
public sealed class FormattingStateTracker
{
    /// <summary>Creates a tracker whose baseline is <see cref="FormattingState.Default"/>.</summary>
    public FormattingStateTracker() : this(FormattingState.Default) { }

    /// <summary>Creates a tracker with the given baseline state.</summary>
    /// <param name="initial">The state to compare the first update against.</param>
    public FormattingStateTracker(FormattingState initial) => Current = initial;

    /// <summary>The most recent state passed to <see cref="Update"/> (or the baseline).</summary>
    public FormattingState Current { get; private set; }

    /// <summary>
    /// Stores <paramref name="state"/> as the current state and returns what differs from the
    /// previous one.
    /// </summary>
    /// <param name="state">The newly computed state.</param>
    public FormattingStateChanges Update(FormattingState state)
    {
        var changes = Compare(Current, state);
        Current = state;
        return changes;
    }

    /// <summary>Returns which groups of values differ between <paramref name="previous"/> and <paramref name="current"/>.</summary>
    /// <param name="previous">The earlier state.</param>
    /// <param name="current">The later state.</param>
    public static FormattingStateChanges Compare(FormattingState previous, FormattingState current)
    {
        var changes = FormattingStateChanges.None;
        if (!string.Equals(previous.FontFamily, current.FontFamily, StringComparison.Ordinal))
            changes |= FormattingStateChanges.FontFamily;
        if (previous.FontSize != current.FontSize)
            changes |= FormattingStateChanges.FontSize;
        if (previous.IsBold != current.IsBold || previous.IsItalic != current.IsItalic
            || previous.IsSubscript != current.IsSubscript || previous.IsSuperscript != current.IsSuperscript)
            changes |= FormattingStateChanges.FontAttributes;
        if (previous.IsUnderline != current.IsUnderline || previous.IsStrikethrough != current.IsStrikethrough)
            changes |= FormattingStateChanges.TextDecorations;
        if (previous.ParagraphFormat != current.ParagraphFormat)
            changes |= FormattingStateChanges.ParagraphFormat;
        if (previous.Alignment != current.Alignment)
            changes |= FormattingStateChanges.Alignment;
        if (previous.ListType != current.ListType)
            changes |= FormattingStateChanges.ListType;
        if (previous.TextColor != current.TextColor)
            changes |= FormattingStateChanges.TextColor;
        if (previous.HighlightColor != current.HighlightColor)
            changes |= FormattingStateChanges.HighlightColor;
        return changes;
    }
}
