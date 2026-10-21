using MintedTextEditor.Core.Document;

namespace MintedTextEditor.Core.Formatting;

/// <summary>
/// Stateless (except for pending style) engine that applies, removes, and toggles
/// inline character formatting over a <see cref="TextRange"/>.
/// </summary>
public class FormattingEngine
{
    /// <summary>
    /// Style to apply on the next text insert when the caret is collapsed and the
    /// user has toggled a format with no selection.  Consumed after one use, and discarded
    /// when the caret moves away (see <see cref="DiscardPendingStyleIfMoved"/>).
    /// </summary>
    public TextStyle? PendingStyle { get; private set; }

    /// <summary>The caret position where <see cref="PendingStyle"/> was chosen, or null when there is none.</summary>
    public DocumentPosition? PendingStylePosition { get; private set; }

    /// <summary>
    /// Discards <see cref="PendingStyle"/> unless <paramref name="selection"/> is still the collapsed
    /// caret where it was chosen, so a pending style applies only at that caret.
    /// </summary>
    /// <param name="doc">The document.</param>
    /// <param name="selection">The current selection.</param>
    public void DiscardPendingStyleIfMoved(Document.Document doc, TextRange selection)
    {
        if (PendingStyle is not null && !IsCaretAt(doc, selection, PendingStylePosition))
            SetPendingStyle(null, null);
    }

    /// <summary>Re-anchors the pending style at <paramref name="position"/> (an edit such as Enter carried it there).</summary>
    internal void MovePendingStyleTo(DocumentPosition position)
    {
        if (PendingStyle is not null) PendingStylePosition = position;
    }

    /// <summary>
    /// Whether <paramref name="selection"/> is a collapsed caret at the same spot as <paramref name="position"/>
    /// (same paragraph and character offset, however the position is split across inlines).
    /// </summary>
    internal static bool IsCaretAt(Document.Document doc, TextRange selection, DocumentPosition? position)
    {
        if (position is not { } p || !selection.IsEmpty) return false;
        var caret = selection.Start;
        return caret.BlockIndex == p.BlockIndex && caret.CellRow == p.CellRow && caret.CellCol == p.CellCol
            && DocumentEditor.GetAbsoluteCharOffset(doc, caret) == DocumentEditor.GetAbsoluteCharOffset(doc, p);
    }

    private void SetPendingStyle(TextStyle? style, DocumentPosition? position)
    {
        PendingStyle = style;
        PendingStylePosition = style is null ? null : position;
    }

    // ── Public toggle API ─────────────────────────────────────────────

    /// <summary>
    /// Toggles bold for <paramref name="range"/>: removes it if the entire range already has it,
    /// otherwise applies it. When the range is empty, toggles it on <see cref="PendingStyle"/> instead.
    /// </summary>
    public void ToggleBold(Document.Document doc, TextRange range)
        => Toggle(doc, range, s => s.IsBold, (s, v) => s.WithBold(v));

    /// <summary>
    /// Toggles italic for <paramref name="range"/>: removes it if the entire range already has it,
    /// otherwise applies it. When the range is empty, toggles it on <see cref="PendingStyle"/> instead.
    /// </summary>
    public void ToggleItalic(Document.Document doc, TextRange range)
        => Toggle(doc, range, s => s.IsItalic, (s, v) => s.WithItalic(v));

    /// <summary>
    /// Toggles underline for <paramref name="range"/>: removes it if the entire range already has it,
    /// otherwise applies it. When the range is empty, toggles it on <see cref="PendingStyle"/> instead.
    /// </summary>
    public void ToggleUnderline(Document.Document doc, TextRange range)
        => Toggle(doc, range, s => s.IsUnderline, (s, v) => s.WithUnderline(v));

    /// <summary>
    /// Toggles strikethrough for <paramref name="range"/>: removes it if the entire range already has it,
    /// otherwise applies it. When the range is empty, toggles it on <see cref="PendingStyle"/> instead.
    /// </summary>
    public void ToggleStrikethrough(Document.Document doc, TextRange range)
        => Toggle(doc, range, s => s.IsStrikethrough, (s, v) => s.WithStrikethrough(v));

    /// <summary>
    /// Toggles subscript (superscript is always cleared) for <paramref name="range"/>: removes it if the entire range already has it,
    /// otherwise applies it. When the range is empty, toggles it on <see cref="PendingStyle"/> instead.
    /// </summary>
    public void ToggleSubscript(Document.Document doc, TextRange range)
        => Toggle(doc, range, s => s.IsSubscript, (s, v) => s.WithSubscript(v));

    /// <summary>
    /// Toggles superscript (subscript is always cleared) for <paramref name="range"/>: removes it if the entire range already has it,
    /// otherwise applies it. When the range is empty, toggles it on <see cref="PendingStyle"/> instead.
    /// </summary>
    public void ToggleSuperscript(Document.Document doc, TextRange range)
        => Toggle(doc, range, s => s.IsSuperscript, (s, v) => s.WithSuperscript(v));

    /// <summary>
    /// Resets all character formatting to <see cref="TextStyle.Default"/> within the range.
    /// When range is empty, sets the pending style to default for the next typed character.
    /// </summary>
    public void ClearFormatting(Document.Document doc, TextRange range)
    {
        if (range.IsEmpty)
        {
            SetPendingStyle(TextStyle.Default, range.Start);
            return;
        }
        DocumentEditor.ApplyTextStyle(doc, range, _ => TextStyle.Default);
    }

    /// <summary>
    /// Consumes and returns the pending style (sets it back to null).
    /// Called by the input controller before inserting a typed character.
    /// </summary>
    public TextStyle? ConsumePendingStyle()
    {
        var style = PendingStyle;
        SetPendingStyle(null, null);
        return style;
    }

    /// <summary>
    /// Returns the style that text typed at a collapsed caret at <paramref name="caret"/> receives:
    /// the pending styles when set (font-level properties taken from <paramref name="pendingFontStyle"/>),
    /// otherwise the style inherited from the surrounding text (see <see cref="DocumentEditor.GetInsertionStyle"/>).
    /// </summary>
    /// <param name="doc">The document.</param>
    /// <param name="caret">The caret position.</param>
    /// <param name="pendingStyle">The pending character style (from <see cref="PendingStyle"/>), or null.</param>
    /// <param name="pendingFontStyle">The pending font style (from <see cref="FontFormattingEngine.PendingStyle"/>), or null.</param>
    public static TextStyle GetEffectiveCaretStyle(
        Document.Document doc,
        DocumentPosition caret,
        TextStyle? pendingStyle,
        TextStyle? pendingFontStyle)
    {
        if (pendingStyle is null && pendingFontStyle is null)
            return DocumentEditor.GetInsertionStyle(doc, caret);
        if (pendingStyle is null) return pendingFontStyle!;
        if (pendingFontStyle is null) return pendingStyle;
        return pendingStyle
            .WithFontFamily(pendingFontStyle.FontFamily)
            .WithFontSize(pendingFontStyle.FontSize)
            .WithTextColor(pendingFontStyle.TextColor)
            .WithHighlightColor(pendingFontStyle.HighlightColor);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private void Toggle(
        Document.Document doc,
        TextRange range,
        Func<TextStyle, bool> getter,
        Func<TextStyle, bool, TextStyle> setter)
    {
        if (range.IsEmpty)
        {
            // No selection: update pending style so the next typed character picks it up.
            // Start from the style the caret would otherwise type with, so toggling bold off
            // inside bold text really turns it off and other formatting is kept.
            DiscardPendingStyleIfMoved(doc, range);
            var baseStyle = PendingStyle ?? DocumentEditor.GetInsertionStyle(doc, range.Start);
            SetPendingStyle(setter(baseStyle, !getter(baseStyle)), range.Start);
            return;
        }

        bool allApplied = IsAppliedToEntireRange(doc, range, getter);
        DocumentEditor.ApplyTextStyle(doc, range, s => setter(s, !allApplied));
    }

    /// <summary>
    /// Returns true when every text run covered by <paramref name="range"/> has <paramref name="getter"/> returning true.
    /// Returns false when the range is empty or contains no text runs.
    /// </summary>
    public static bool IsAppliedToEntireRange(
        Document.Document doc,
        TextRange range,
        Func<TextStyle, bool> getter)
    {
        if (range.IsEmpty) return false;

        var start = range.Start;
        var end   = range.End;
        bool foundAnyRun = false;

        for (int bi = start.BlockIndex; bi <= end.BlockIndex && bi < doc.Blocks.Count; bi++)
        {
            if (doc.Blocks[bi] is not Paragraph para) continue;

            for (int ii = 0; ii < para.Inlines.Count; ii++)
            {
                if (para.Inlines[ii] is not TextRun run) continue;
                if (!IsRunInRange(bi, ii, run, start, end)) continue;

                foundAnyRun = true;
                if (!getter(run.Style))
                    return false;
            }
        }

        return foundAnyRun;
    }

    private static bool IsRunInRange(
        int bi, int ii, TextRun run,
        DocumentPosition start, DocumentPosition end)
    {
        if (bi < start.BlockIndex || bi > end.BlockIndex) return false;

        if (bi == start.BlockIndex && ii < start.InlineIndex) return false;
        if (bi == end.BlockIndex   && ii > end.InlineIndex)   return false;

        // At the exact first inline: make sure the range doesn't start after the run's text ends
        if (bi == start.BlockIndex && ii == start.InlineIndex && start.Offset >= run.Text.Length)
            return false;

        // At the exact last inline: make sure the range doesn't end before the run's text starts
        if (bi == end.BlockIndex && ii == end.InlineIndex && end.Offset <= 0)
            return false;

        return true;
    }
}
