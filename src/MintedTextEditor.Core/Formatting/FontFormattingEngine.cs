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
    /// are changed with no active selection.
    /// </summary>
    public TextStyle? PendingStyle { get; private set; }

    // ── Apply API ─────────────────────────────────────────────────────

    public void ApplyFontFamily(Document.Document doc, TextRange range, string family)
        => Apply(doc, range, s => s.WithFontFamily(family));

    public void ApplyFontSize(Document.Document doc, TextRange range, float size)
        => Apply(doc, range, s => s.WithFontSize(size));

    public void ApplyTextColor(Document.Document doc, TextRange range, EditorColor color)
        => Apply(doc, range, s => s.WithTextColor(color));

    public void ApplyHighlightColor(Document.Document doc, TextRange range, EditorColor color)
        => Apply(doc, range, s => s.WithHighlightColor(color));

    public void RemoveHighlightColor(Document.Document doc, TextRange range)
        => Apply(doc, range, s => s.WithHighlightColor(EditorColor.Transparent));

    /// <summary>
    /// Consumes and returns the pending style (sets it back to null).
    /// </summary>
    public TextStyle? ConsumePendingStyle()
    {
        var style = PendingStyle;
        PendingStyle = null;
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

        var start = range.Start;
        var end   = range.End;

        for (int bi = start.BlockIndex; bi <= end.BlockIndex && bi < doc.Blocks.Count; bi++)
        {
            if (doc.Blocks[bi] is not Paragraph para) continue;

            for (int ii = 0; ii < para.Inlines.Count; ii++)
            {
                if (para.Inlines[ii] is not TextRun run) continue;
                if (!IsRunInRange(bi, ii, run, start, end)) continue;

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
            var baseStyle = PendingStyle ?? TextStyle.Default;
            PendingStyle = transform(baseStyle);
            return;
        }

        DocumentEditor.ApplyTextStyle(doc, range, transform);
    }

    private static bool IsRunInRange(
        int bi, int ii, TextRun run,
        DocumentPosition start, DocumentPosition end)
    {
        if (bi < start.BlockIndex || bi > end.BlockIndex) return false;
        if (bi == start.BlockIndex && ii < start.InlineIndex) return false;
        if (bi == end.BlockIndex   && ii > end.InlineIndex)   return false;
        if (bi == start.BlockIndex && ii == start.InlineIndex && start.Offset >= run.Text.Length) return false;
        if (bi == end.BlockIndex   && ii == end.InlineIndex   && end.Offset <= 0) return false;
        return true;
    }
}
