using EditorDoc = MintedTextEditor.Core.Document.Document;
using MintedTextEditor.Core.Document;

namespace MintedTextEditor.Core.Editing;

/// <summary>Options controlling a find/replace operation.</summary>
public sealed class FindOptions
{
    /// <summary>When <c>true</c>, the search is case-sensitive.</summary>
    public bool MatchCase { get; init; }

    /// <summary>When <c>true</c>, only whole-word occurrences are returned.</summary>
    public bool WholeWord { get; init; }

    /// <summary>When <c>true</c>, wraps around to the start of the document after the last match.</summary>
    public bool WrapAround { get; init; } = true;

    /// <summary>Returns a <see cref="FindOptions"/> instance with all defaults.</summary>
    public static FindOptions Default => new();
}

/// <summary>Describes a single match found by <see cref="FindReplaceEngine"/>.</summary>
public sealed class FindMatch
{
    /// <summary>The document range covering the matched text.</summary>
    public TextRange Range { get; }

    /// <summary>The matched text.</summary>
    public string Text { get; }

    /// <summary>Creates a match at the given <paramref name="range"/>.</summary>
    public FindMatch(TextRange range, string text)
    {
        Range = range;
        Text = text;
    }
}

/// <summary>
/// Provides incremental find and replace within a <see cref="Document"/>.
///
/// The engine extracts the plain text of each block on demand so no extra memory is allocated
/// when the document is unchanged. All positions are expressed as
/// <see cref="DocumentPosition"/> / <see cref="TextRange"/> values that map 1:1 to inline
/// offsets inside the block's text-run chain.
/// </summary>
public sealed class FindReplaceEngine
{
    private readonly EditorDoc _document;

    // ─── State ───────────────────────────────────────────────────────────────

    /// <summary>All matches for the last <see cref="Find"/> / <see cref="FindAll"/> call.</summary>
    public IReadOnlyList<FindMatch> Matches => _matches;

    /// <summary>Index into <see cref="Matches"/> of the currently highlighted match, or -1.</summary>
    public int CurrentMatchIndex { get; private set; } = -1;

    /// <summary>The currently highlighted match, or <c>null</c>.</summary>
    public FindMatch? CurrentMatch =>
        CurrentMatchIndex >= 0 && CurrentMatchIndex < _matches.Count ? _matches[CurrentMatchIndex] : null;

    private readonly List<FindMatch> _matches = [];

    /// <summary>Creates an engine bound to <paramref name="document"/>.</summary>
    public FindReplaceEngine(EditorDoc document) =>
        _document = document ?? throw new ArgumentNullException(nameof(document));

    // ─── Find ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Searches for all occurrences of <paramref name="query"/> and caches them.
    /// Returns the first match, or <c>null</c> when there are no results.
    /// </summary>
    public FindMatch? Find(string query, FindOptions? options = null)
    {
        if (string.IsNullOrEmpty(query))
        {
            _matches.Clear();
            CurrentMatchIndex = -1;
            return null;
        }

        options ??= FindOptions.Default;
        _matches.Clear();
        CurrentMatchIndex = -1;

        CollectMatches(query, options);
        if (_matches.Count == 0) return null;

        CurrentMatchIndex = 0;
        return _matches[0];
    }

    /// <summary>
    /// Advances to the next match after the current one.
    /// Wraps around to the beginning when <see cref="FindOptions.WrapAround"/> is <c>true</c>.
    /// </summary>
    public FindMatch? FindNext(FindOptions? options = null)
    {
        if (_matches.Count == 0) return null;

        bool wrapAround = options?.WrapAround ?? true;
        if (CurrentMatchIndex < _matches.Count - 1)
            CurrentMatchIndex++;
        else if (wrapAround)
            CurrentMatchIndex = 0;
        else
            return null;

        return _matches[CurrentMatchIndex];
    }

    /// <summary>
    /// Moves back to the previous match before the current one.
    /// Wraps around to the end when <see cref="FindOptions.WrapAround"/> is <c>true</c>.
    /// </summary>
    public FindMatch? FindPrevious(FindOptions? options = null)
    {
        if (_matches.Count == 0) return null;

        bool wrapAround = options?.WrapAround ?? true;
        if (CurrentMatchIndex > 0)
            CurrentMatchIndex--;
        else if (wrapAround)
            CurrentMatchIndex = _matches.Count - 1;
        else
            return null;

        return _matches[CurrentMatchIndex];
    }

    /// <summary>Returns every match in document order without changing <see cref="CurrentMatchIndex"/>.</summary>
    public IReadOnlyList<FindMatch> FindAll(string query, FindOptions? options = null)
    {
        if (string.IsNullOrEmpty(query))
        {
            _matches.Clear();
            CurrentMatchIndex = -1;
            return _matches;
        }

        options ??= FindOptions.Default;
        _matches.Clear();
        CollectMatches(query, options);
        return _matches;
    }

    // ─── Replace ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces the <see cref="CurrentMatch"/> with <paramref name="replacement"/>, then re-runs
    /// find to refresh positions.  Returns the next match, or <c>null</c> when there are none.
    /// </summary>
    public FindMatch? Replace(string query, string replacement, FindOptions? options = null)
    {
        if (CurrentMatch is not { } match) return null;

        // Delete the matched range, then insert the replacement at that position.
        var pos = DocumentEditor.DeleteRange(_document, match.Range);
        DocumentEditor.InsertText(_document, pos, replacement);

        return Find(query, options);
    }

    /// <summary>
    /// Replaces all occurrences of <paramref name="query"/> with <paramref name="replacement"/>.
    /// Applies replacements in reverse document order so earlier positions remain valid.
    /// Returns the number of replacements made.
    /// </summary>
    public int ReplaceAll(string query, string replacement, FindOptions? options = null)
    {
        if (string.IsNullOrEmpty(query)) return 0;

        options ??= FindOptions.Default;
        _matches.Clear();
        CollectMatches(query, options);

        // Apply in reverse so later-in-document replacements do not shift earlier positions.
        for (int i = _matches.Count - 1; i >= 0; i--)
        {
            var pos = DocumentEditor.DeleteRange(_document, _matches[i].Range);
            DocumentEditor.InsertText(_document, pos, replacement);
        }

        int count = _matches.Count;
        _matches.Clear();
        CurrentMatchIndex = -1;
        return count;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void CollectMatches(string query, FindOptions options)
    {
        var comparison = options.MatchCase
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        for (int bi = 0; bi < _document.Blocks.Count; bi++)
        {
            var block = _document.Blocks[bi];
            string blockText = block.GetText();
            int searchFrom = 0;

            while (searchFrom < blockText.Length)
            {
                int idx = blockText.IndexOf(query, searchFrom, comparison);
                if (idx < 0) break;

                if (options.WholeWord && !IsWholeWord(blockText, idx, query.Length))
                {
                    searchFrom = idx + 1;
                    continue;
                }

                var (startPos, endPos) = OffsetToPosition(block, bi, idx, idx + query.Length);
                _matches.Add(new FindMatch(new TextRange(startPos, endPos),
                             blockText.Substring(idx, query.Length)));

                searchFrom = idx + query.Length;
            }
        }
    }

    private static bool IsWholeWord(string text, int start, int length)
    {
        bool leftOk  = start == 0 || !char.IsLetterOrDigit(text[start - 1]);
        bool rightOk = start + length >= text.Length || !char.IsLetterOrDigit(text[start + length]);
        return leftOk && rightOk;
    }

    /// <summary>
    /// Converts character offsets within a block's plain-text back to
    /// <see cref="DocumentPosition"/> values by walking the inline chain.
    /// </summary>
    private static (DocumentPosition start, DocumentPosition end) OffsetToPosition(
        Block block, int blockIndex, int charStart, int charEnd)
    {
        if (block is not Paragraph para)
        {
            var def = new DocumentPosition(blockIndex, 0, 0);
            return (def, def);
        }

        int accumulated = 0;
        DocumentPosition startPos = new(blockIndex, 0, 0);
        DocumentPosition endPos   = new(blockIndex, 0, 0);
        bool foundStart = false;
        bool foundEnd   = false;

        for (int ii = 0; ii < para.Inlines.Count; ii++)
        {
            string inlineText = para.Inlines[ii].GetText();
            int len = inlineText.Length;

            if (!foundStart && charStart < accumulated + len)
            {
                startPos   = new DocumentPosition(blockIndex, ii, charStart - accumulated);
                foundStart = true;
            }

            if (!foundEnd && charEnd <= accumulated + len)
            {
                endPos   = new DocumentPosition(blockIndex, ii, charEnd - accumulated);
                foundEnd = true;
            }

            if (foundStart && foundEnd) break;

            accumulated += len;
        }

        // Fallback: clamp to end of block.
        if (!foundEnd && para.Inlines.Count > 0)
        {
            int last = para.Inlines.Count - 1;
            endPos = new DocumentPosition(blockIndex, last, para.Inlines[last].GetText().Length);
        }

        return (startPos, endPos);
    }
}
