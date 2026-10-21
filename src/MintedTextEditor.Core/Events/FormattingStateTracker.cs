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
/// are true only when applied to all selected text, including text in table cells and hyperlinks;
/// font family, size and colours are the selected text's common value, or the value of its first
/// character when mixed.
/// For a caret, every value comes from the pending style (formatting chosen with nothing
/// selected, applied to the next typed text) when there is one, otherwise from the text run (or
/// hyperlink text) at the caret. Paragraph values come from the paragraph at the start of the
/// selection.
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

        // Styles of the text runs that the selection actually covers, including runs inside
        // table cells and hyperlinks. A selection with no text (e.g. only an image) has no
        // on/off styles and takes its font from the run at its start.
        var styles = CollectSelectedRunStyles(document, range);
        if (styles.Count == 0)
        {
            return FromStyles(GetRunStyle(paragraph, range.Start), paraStyle) with
            {
                IsBold = false, IsItalic = false, IsUnderline = false,
                IsStrikethrough = false, IsSubscript = false, IsSuperscript = false,
            };
        }

        // Font and colours: the common value, or the value at the start of the selection when
        // mixed; both are the first covered run's value.
        return FromStyles(styles[0], paraStyle) with
        {
            IsBold          = styles.TrueForAll(s => s.IsBold),
            IsItalic        = styles.TrueForAll(s => s.IsItalic),
            IsUnderline     = styles.TrueForAll(s => s.IsUnderline),
            IsStrikethrough = styles.TrueForAll(s => s.IsStrikethrough),
            IsSubscript     = styles.TrueForAll(s => s.IsSubscript),
            IsSuperscript   = styles.TrueForAll(s => s.IsSuperscript),
        };
    }

    /// <summary>
    /// Returns the styles of the text runs that share at least one character with
    /// <paramref name="range"/>, in document order. Coverage is decided by character offsets
    /// within each paragraph, so the equivalent positions either side of a run boundary
    /// (end of one run, start of the next) select the same runs.
    /// </summary>
    private static List<TextStyle> CollectSelectedRunStyles(Document.Document document, TextRange range)
    {
        var styles = new List<TextStyle>();
        var start = range.Start;
        var end = range.End;

        for (int bi = Math.Max(0, start.BlockIndex); bi <= end.BlockIndex && bi < document.Blocks.Count; bi++)
        {
            switch (document.Blocks[bi])
            {
                case Paragraph paragraph:
                {
                    int from = bi == start.BlockIndex && !start.IsInTableCell
                        ? DocumentEditor.GetAbsoluteCharOffset(document, start) : 0;
                    int to = bi == end.BlockIndex && !end.IsInTableCell
                        ? DocumentEditor.GetAbsoluteCharOffset(document, end) : int.MaxValue;
                    CollectFromInlines(paragraph.Inlines, 0, from, to, styles);
                    break;
                }
                case TableBlock table:
                {
                    bool startsInTable = bi == start.BlockIndex && start.IsInTableCell;
                    bool endsInTable = bi == end.BlockIndex && end.IsInTableCell;
                    for (int r = 0; r < table.Rows.Count; r++)
                    {
                        for (int c = 0; c < table.Rows[r].Cells.Count; c++)
                        {
                            bool isStartCell = startsInTable && r == start.CellRow && c == start.CellCol;
                            bool isEndCell = endsInTable && r == end.CellRow && c == end.CellCol;
                            if (startsInTable && (r, c).CompareTo((start.CellRow, start.CellCol)) < 0) continue;
                            if (endsInTable && (r, c).CompareTo((end.CellRow, end.CellCol)) > 0) continue;

                            // Positions in a cell refer to its first paragraph.
                            var blocks = table.Rows[r].Cells[c].Blocks;
                            for (int k = 0; k < blocks.Count; k++)
                            {
                                if (blocks[k] is not Paragraph cellParagraph) continue;
                                if (isEndCell && k > 0) break;
                                int from = isStartCell && k == 0 ? DocumentEditor.GetAbsoluteCharOffset(document, start) : 0;
                                int to = isEndCell ? DocumentEditor.GetAbsoluteCharOffset(document, end) : int.MaxValue;
                                CollectFromInlines(cellParagraph.Inlines, 0, from, to, styles);
                            }
                        }
                    }
                    break;
                }
            }
        }

        return styles;
    }

    /// <summary>
    /// Adds the styles of text runs in <paramref name="inlines"/> (and in hyperlinks among them)
    /// that overlap the character span [<paramref name="from"/>, <paramref name="to"/>).
    /// Returns the offset after the last inline.
    /// </summary>
    private static int CollectFromInlines(IEnumerable<Inline> inlines, int offset, int from, int to, List<TextStyle> styles)
    {
        foreach (var inline in inlines)
        {
            if (inline is HyperlinkInline link)
            {
                offset = CollectFromInlines(link.Children, offset, from, to, styles);
                continue;
            }

            int length = inline.Length;
            if (inline is TextRun run && Math.Max(offset, from) < Math.Min(offset + length, to))
                styles.Add(run.Style);
            offset += length;
        }
        return offset;
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
            HyperlinkInline link => GetHyperlinkChildStyle(link, position.Offset),
            _ => TextStyle.Default,
        };
    }

    /// <summary>
    /// Returns the style of the hyperlink child at <paramref name="offset"/> (counted over the
    /// children's cumulative lengths). At a boundary between two children the earlier one is used,
    /// as <see cref="DocumentEditor.InsertText"/> does when typing there.
    /// </summary>
    private static TextStyle GetHyperlinkChildStyle(HyperlinkInline link, int offset)
    {
        int remaining = Math.Clamp(offset, 0, link.Length);
        TextStyle? lastText = null;
        foreach (var child in link.Children)
        {
            if (child is TextRun run)
                lastText = run.Style;
            if (remaining <= child.Length && lastText is not null)
                return lastText;
            remaining -= child.Length;
        }
        return lastText ?? TextStyle.Default;
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
