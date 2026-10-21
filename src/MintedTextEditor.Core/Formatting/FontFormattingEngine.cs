using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Formatting;

/// <summary>
/// Applies font-level formatting (family, size, colors) to a range and
/// provides query helpers to read the effective style at a position or range.
/// </summary>
public class FontFormattingEngine
{
    /// <summary>
    /// Style to apply on the next text insert when the caret is collapsed and font settings
    /// are changed with no active selection. Discarded when the caret moves away
    /// (see <see cref="DiscardPendingStyleIfMoved"/>).
    /// </summary>
    public TextStyle? PendingStyle { get; private set; }

    /// <summary>The caret position where <see cref="PendingStyle"/> was chosen, or null when there is none.</summary>
    public DocumentPosition? PendingStylePosition { get; private set; }

    // Where the pending style was chosen, as a paragraph and character offset taken when it was chosen.
    private PendingStyleAnchor? _pendingAnchor;

    /// <summary>
    /// Discards <see cref="PendingStyle"/> unless <paramref name="selection"/> is still the collapsed
    /// caret where it was chosen, so a pending style applies only at that caret.
    /// </summary>
    /// <param name="doc">The document.</param>
    /// <param name="selection">The current selection.</param>
    public void DiscardPendingStyleIfMoved(Document.Document doc, TextRange selection)
    {
        if (PendingStyle is not null && !PendingStyleAnchor.IsCaretAt(doc, selection, _pendingAnchor))
            SetPendingStyle(doc, null, null);
    }

    /// <summary>Re-anchors the pending style at <paramref name="position"/> (an edit such as Enter carried it there).</summary>
    internal void MovePendingStyleTo(Document.Document doc, DocumentPosition position)
    {
        if (PendingStyle is not null) SetPendingStyle(doc, PendingStyle, position);
    }

    private void SetPendingStyle(Document.Document doc, TextStyle? style, DocumentPosition? position)
    {
        PendingStyle = style;
        PendingStylePosition = style is null ? null : position;
        _pendingAnchor = style is not null && position is { } p ? PendingStyleAnchor.At(doc, p) : null;
    }

    // ── Apply API ─────────────────────────────────────────────────────

    /// <summary>Sets the font family of the text in <paramref name="range"/>. When <paramref name="range"/> is empty, updates <see cref="PendingStyle"/> instead.</summary>
    public void ApplyFontFamily(Document.Document doc, TextRange range, string family)
        => Apply(doc, range, s => s.WithFontFamily(family));

    /// <summary>Sets the font size of the text in <paramref name="range"/>. When <paramref name="range"/> is empty, updates <see cref="PendingStyle"/> instead.</summary>
    public void ApplyFontSize(Document.Document doc, TextRange range, float size)
        => Apply(doc, range, s => s.WithFontSize(size));

    /// <summary>Sets the text color of the text in <paramref name="range"/>. When <paramref name="range"/> is empty, updates <see cref="PendingStyle"/> instead.</summary>
    public void ApplyTextColor(Document.Document doc, TextRange range, EditorColor color)
        => Apply(doc, range, s => s.WithTextColor(color));

    /// <summary>Sets the highlight color of the text in <paramref name="range"/>. When <paramref name="range"/> is empty, updates <see cref="PendingStyle"/> instead.</summary>
    public void ApplyHighlightColor(Document.Document doc, TextRange range, EditorColor color)
        => Apply(doc, range, s => s.WithHighlightColor(color));

    /// <summary>Sets the highlight color of the text in <paramref name="range"/> to transparent. When <paramref name="range"/> is empty, updates <see cref="PendingStyle"/> instead.</summary>
    public void RemoveHighlightColor(Document.Document doc, TextRange range)
        => Apply(doc, range, s => s.WithHighlightColor(EditorColor.Transparent));

    /// <summary>
    /// Consumes and returns the pending style (sets it back to null).
    /// </summary>
    public TextStyle? ConsumePendingStyle()
    {
        var style = PendingStyle;
        PendingStyle = null;
        PendingStylePosition = null;
        _pendingAnchor = null;
        return style;
    }

    // ── Query API ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the <see cref="TextStyle"/> of the run at <paramref name="position"/>.
    /// Falls back to <see cref="TextStyle.Default"/> when the paragraph is empty.
    /// </summary>
    public static TextStyle GetCurrentTextStyle(Document.Document doc, DocumentPosition position)
    {
        if (position.BlockIndex >= doc.Blocks.Count)
            return TextStyle.Default;

        if (doc.Blocks[position.BlockIndex] is not Paragraph para)
            return TextStyle.Default;

        if (para.Inlines.Count == 0)
            return TextStyle.Default;

        // Clamp inline index in case it is past the end
        int ii = Math.Min(position.InlineIndex, para.Inlines.Count - 1);
        if (para.Inlines[ii] is TextRun run)
            return run.Style;

        return TextStyle.Default;
    }

    /// <summary>
    /// Returns the common <see cref="TextStyle"/> if every run in <paramref name="range"/>
    /// shares the same style; returns <see langword="null"/> when styles are mixed.
    /// Returns <see langword="null"/> also when the range is empty.
    /// </summary>
    public static TextStyle? GetTextStyleForRange(Document.Document doc, TextRange range)
    {
        if (range.IsEmpty) return null;

        TextStyle? common = null;

        // Top-level paragraphs and the paragraphs of the table cells in the range.
        foreach (var (para, from, to) in DocumentEditor.GetParagraphsInRange(doc, range))
        {
            for (int ii = 0; ii < para.Inlines.Count; ii++)
            {
                if (para.Inlines[ii] is not TextRun run) continue;
                if (!FormattingEngine.IsRunInRange(ii, run, from, to)) continue;

                if (common is null)
                    common = run.Style;
                else if (!common.Equals(run.Style))
                    return null;   // mixed
            }
        }

        return common;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private void Apply(Document.Document doc, TextRange range, Func<TextStyle, TextStyle> transform)
    {
        if (range.IsEmpty)
        {
            DiscardPendingStyleIfMoved(doc, range);
            var baseStyle = PendingStyle ?? DocumentEditor.GetInsertionStyle(doc, range.Start);
            SetPendingStyle(doc, transform(baseStyle), range.Start);
            return;
        }

        DocumentEditor.ApplyTextStyle(doc, range, transform);
    }
}
