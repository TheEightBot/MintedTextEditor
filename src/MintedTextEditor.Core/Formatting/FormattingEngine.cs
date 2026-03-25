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
    /// user has toggled a format with no selection.  Consumed after one use.
    /// </summary>
    public TextStyle? PendingStyle { get; private set; }

    // ── Public toggle API ─────────────────────────────────────────────

    public void ToggleBold(Document.Document doc, TextRange range)
        => Toggle(doc, range, s => s.IsBold, (s, v) => s.WithBold(v));

    public void ToggleItalic(Document.Document doc, TextRange range)
        => Toggle(doc, range, s => s.IsItalic, (s, v) => s.WithItalic(v));

    public void ToggleUnderline(Document.Document doc, TextRange range)
        => Toggle(doc, range, s => s.IsUnderline, (s, v) => s.WithUnderline(v));

    public void ToggleStrikethrough(Document.Document doc, TextRange range)
        => Toggle(doc, range, s => s.IsStrikethrough, (s, v) => s.WithStrikethrough(v));

    public void ToggleSubscript(Document.Document doc, TextRange range)
        => Toggle(doc, range, s => s.IsSubscript, (s, v) => s.WithSubscript(v));

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
            PendingStyle = TextStyle.Default;
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
        PendingStyle = null;
        return style;
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
            // No selection: update pending style so the next typed character picks it up
            var baseStyle = PendingStyle ?? TextStyle.Default;
            PendingStyle = setter(baseStyle, !getter(baseStyle));
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
