using System.Text.RegularExpressions;
using MintedTextEditor.Core.Document;

namespace MintedTextEditor.Core.Formatting;

/// <summary>
/// Operations for inserting, editing, removing, and navigating hyperlinks
/// within a document.  All mutating methods fire <see cref="Document.Document.NotifyChanged"/>.
/// </summary>
public static class HyperlinkEngine
{
    // Minimal URL detection pattern: http(s):// or www. prefix
    private static readonly Regex UrlPattern =
        new(@"(https?://\S+|www\.\S+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ── Insert / Edit / Remove ────────────────────────────────────────

    /// <summary>
    /// Wraps the text covered by <paramref name="range"/> in a new
    /// <see cref="HyperlinkInline"/>. If the range is empty, inserts a new
    /// text run equal to <paramref name="url"/> wrapped in the hyperlink.
    /// Returns the inserted <see cref="HyperlinkInline"/>.
    /// </summary>
    public static HyperlinkInline InsertHyperlink(
        Document.Document doc, TextRange range, string url, string? title = null)
    {
        var para = GetParagraph(doc, range.Start.BlockIndex);

        string linkText;
        DocumentPosition insertPos;

        if (range.IsEmpty)
        {
            linkText  = url;
            insertPos = range.Start;
        }
        else
        {
            linkText  = DocumentEditor.GetSelectedText(doc, range);
            insertPos = DocumentEditor.DeleteRange(doc, range);
        }

        var hyperlink = new HyperlinkInline(url, title);
        hyperlink.AddChild(new TextRun(linkText));

        // Refresh paragraph reference after deletion
        para = GetParagraph(doc, insertPos.BlockIndex);
        InsertHyperlinkInline(para, insertPos, hyperlink);

        doc.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.TextInserted,
            new TextRange(insertPos, insertPos)));

        return hyperlink;
    }

    /// <summary>
    /// Updates the URL and optional title of an existing hyperlink.
    /// </summary>
    public static void EditHyperlink(
        Document.Document doc, HyperlinkInline hyperlink, string newUrl, string? newTitle = null)
    {
        hyperlink.Url   = newUrl;
        hyperlink.Title = newTitle;
        doc.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.StyleChanged,
            TextRange.Empty));
    }

    /// <summary>
    /// Removes the <see cref="HyperlinkInline"/> at <paramref name="position"/>, leaving
    /// its text in place as <see cref="TextRun"/>s that keep their character formatting.
    /// Not recorded for undo; use <see cref="Editing.RemoveHyperlinkAction"/> for that.
    /// </summary>
    public static void RemoveHyperlink(Document.Document doc, DocumentPosition position)
    {
        var hyperlink = GetHyperlinkAtPosition(doc, position);
        if (hyperlink is null) return;

        var para = GetParagraph(doc, position.BlockIndex);
        int idx  = para.Inlines.IndexOf(hyperlink);
        if (idx < 0) return;

        // Replace the hyperlink with copies of its text runs, keeping each run's style
        para.RemoveInline(idx);
        int insertAt = idx;
        foreach (var child in hyperlink.Children)
        {
            if (child is TextRun run)
            {
                if (run.Length > 0)
                    para.InsertInline(insertAt++, new TextRun(run.Text, run.Style));
            }
            else
            {
                para.InsertInline(insertAt++, child);
            }
        }
        if (para.Inlines.Count == 0)
            para.AppendRun(string.Empty);

        doc.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.TextDeleted,
            new TextRange(position, position)));
    }

    // ── Query ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the <see cref="HyperlinkInline"/> whose inline index matches
    /// <paramref name="position"/>, or <see langword="null"/> if none.
    /// </summary>
    public static HyperlinkInline? GetHyperlinkAtPosition(
        Document.Document doc, DocumentPosition position)
    {
        if (position.BlockIndex >= doc.Blocks.Count) return null;
        if (doc.Blocks[position.BlockIndex] is not Paragraph para) return null;
        if (position.InlineIndex >= para.Inlines.Count) return null;

        return para.Inlines[position.InlineIndex] as HyperlinkInline;
    }

    // ── Auto-detect ───────────────────────────────────────────────────

    /// <summary>
    /// Scans the most-recently-typed word in the paragraph at <paramref name="blockIndex"/>,
    /// ending at <paramref name="caretOffset"/> within the inline at <paramref name="caretInlineIndex"/>,
    /// for a URL pattern. When a URL is found, wraps it in a <see cref="HyperlinkInline"/> and returns
    /// the hyperlink; otherwise returns <see langword="null"/>.
    ///
    /// Call this after inserting a space/newline character (word-commit).
    /// </summary>
    public static HyperlinkInline? AutoDetectUrl(
        Document.Document doc, int blockIndex, int caretInlineIndex, int caretOffset)
    {
        if (doc.Blocks[blockIndex] is not Paragraph para) return null;
        if (caretInlineIndex >= para.Inlines.Count) return null;
        if (para.Inlines[caretInlineIndex] is not TextRun run) return null;

        // Look at the word immediately before the caret offset
        int wordEnd = caretOffset > 0 && caretOffset <= run.Text.Length
            ? caretOffset - 1   // exclude the just-typed delimiter
            : run.Text.Length - 1;

        // Walk back to find word start
        int wordStart = wordEnd;
        while (wordStart > 0 && !char.IsWhiteSpace(run.Text[wordStart - 1]))
            wordStart--;

        if (wordStart >= wordEnd) return null;

        string word = run.Text[wordStart..wordEnd];
        var match = UrlPattern.Match(word);
        if (!match.Success || match.Index != 0 || match.Length != word.Length)
            return null;

        // Build the range covering that word within the run
        var start = new DocumentPosition(blockIndex, caretInlineIndex, wordStart);
        var end   = new DocumentPosition(blockIndex, caretInlineIndex, wordEnd);
        return InsertHyperlink(doc, new TextRange(start, end), match.Value);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static Paragraph GetParagraph(Document.Document doc, int blockIndex)
    {
        if (blockIndex < 0 || blockIndex >= doc.Blocks.Count)
            throw new ArgumentOutOfRangeException(nameof(blockIndex));
        if (doc.Blocks[blockIndex] is not Paragraph para)
            throw new InvalidOperationException("Block is not a Paragraph.");
        return para;
    }

    /// <summary>
    /// Inserts a <see cref="HyperlinkInline"/> into the paragraph at the
    /// location described by <paramref name="pos"/>. If the caret is in the
    /// middle of a <see cref="TextRun"/>, the run is split first.
    /// </summary>
    private static void InsertHyperlinkInline(
        Paragraph para, DocumentPosition pos, HyperlinkInline hyperlink)
    {
        hyperlink.Parent = para;

        int ii = Math.Min(pos.InlineIndex, para.Inlines.Count);

        if (ii < para.Inlines.Count && para.Inlines[ii] is TextRun run && pos.Offset > 0)
        {
            // Split the run at the caret offset
            var right = run.Split(pos.Offset);   // mutates run (left part), returns right part
            int insertAt = ii + 1;
            para.Inlines.Insert(insertAt, hyperlink);
            if (right.Length > 0)
                para.Inlines.Insert(insertAt + 1, right);
        }
        else
        {
            para.Inlines.Insert(ii, hyperlink);
        }
    }
}
