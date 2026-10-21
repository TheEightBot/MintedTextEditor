using EditorDoc = MintedTextEditor.Core.Document.Document;
using MintedTextEditor.Core.Document;

namespace MintedTextEditor.Core.Editing;

/// <summary>
/// Editor-level find and replace. Unlike <see cref="FindReplaceEngine"/>, which keeps its own
/// match cursor, this helper searches relative to the editor's <see cref="Selection"/>: the next
/// match is the first one after the selection, the found match becomes the selection, and edits
/// are pushed to the <see cref="UndoManager"/> so each <see cref="Replace"/> and
/// <see cref="ReplaceAll"/> call is a single undo step.
/// </summary>
public sealed class EditorFindReplace
{
    private readonly EditorDoc _document;
    private readonly Selection _selection;
    private readonly UndoManager? _undoManager;
    private readonly FindReplaceEngine _engine;

    /// <summary>Creates a helper that searches <paramref name="document"/> and drives <paramref name="selection"/>.</summary>
    /// <param name="document">The document to search and edit.</param>
    /// <param name="selection">The editor selection used as the search origin and set to each found match.</param>
    /// <param name="undoManager">When set, replacements are pushed as undoable actions; otherwise they are applied directly.</param>
    public EditorFindReplace(EditorDoc document, Selection selection, UndoManager? undoManager = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _undoManager = undoManager;
        _engine = new FindReplaceEngine(document);
    }

    /// <summary>Number of matches for the query after the most recent operation.</summary>
    public int MatchCount { get; private set; }

    /// <summary>
    /// Zero-based index of the selected match among <see cref="MatchCount"/> matches after the
    /// most recent operation, or -1 when no match is selected.
    /// </summary>
    public int CurrentMatchIndex { get; private set; } = -1;

    /// <summary>
    /// Selects the first match that starts after the current selection, wrapping to the start of
    /// the document when <see cref="FindOptions.WrapAround"/> is <c>true</c>. Returns the selected
    /// match, or <c>null</c> (leaving the selection unchanged) when there is none.
    /// </summary>
    public FindMatch? FindNext(string query, FindOptions? options = null)
    {
        options ??= FindOptions.Default;
        var matches = Collect(query, options);
        return Select(matches, IndexOfNext(matches, Key(_selection.Range.End), options.WrapAround));
    }

    /// <summary>
    /// Selects the last match that starts before the current selection, wrapping to the end of
    /// the document when <see cref="FindOptions.WrapAround"/> is <c>true</c>. Returns the selected
    /// match, or <c>null</c> (leaving the selection unchanged) when there is none.
    /// </summary>
    public FindMatch? FindPrevious(string query, FindOptions? options = null)
    {
        options ??= FindOptions.Default;
        var matches = Collect(query, options);
        var origin = Key(_selection.Range.Start);

        int index = -1;
        for (int i = matches.Count - 1; i >= 0; i--)
        {
            if (Compare(Key(matches[i].Range.Start), origin) < 0) { index = i; break; }
        }
        if (index < 0 && options.WrapAround && matches.Count > 0)
            index = matches.Count - 1;

        return Select(matches, index);
    }

    /// <summary>
    /// Replaces the selected match with <paramref name="replacement"/> (or, when the selection is
    /// not exactly a match, the next match after it) as one undo step, then selects the following
    /// match. Returns <c>true</c> when a replacement was made.
    /// </summary>
    public bool Replace(string query, string replacement, FindOptions? options = null)
    {
        options ??= FindOptions.Default;
        replacement ??= string.Empty;
        var matches = Collect(query, options);

        var selStart = Key(_selection.Range.Start);
        var selEnd = Key(_selection.Range.End);
        int index = matches.FindIndex(m =>
            Compare(Key(m.Range.Start), selStart) == 0 && Compare(Key(m.Range.End), selEnd) == 0);
        if (index < 0)
            index = IndexOfNext(matches, selEnd, options.WrapAround);
        if (index < 0)
        {
            UpdateState(matches.Count, -1);
            return false;
        }

        var pos = Apply(new CompositeAction(CreateReplaceActions(matches[index], replacement)));
        _selection.CollapseTo(pos);
        FindNext(query, options);
        return true;
    }

    /// <summary>
    /// Replaces every match with <paramref name="replacement"/> as a single undo step and collapses
    /// the selection after the first replacement. Returns the number of replacements made.
    /// </summary>
    public int ReplaceAll(string query, string replacement, FindOptions? options = null)
    {
        options ??= FindOptions.Default;
        replacement ??= string.Empty;
        var matches = Collect(query, options);
        if (matches.Count == 0)
        {
            UpdateState(0, -1);
            return 0;
        }

        // Build the actions in reverse document order so each step's original positions are
        // still valid when it executes (later edits never shift earlier text).
        var actions = new List<IUndoableAction>();
        for (int i = matches.Count - 1; i >= 0; i--)
            actions.AddRange(CreateReplaceActions(matches[i], replacement));

        var pos = Apply(new CompositeAction(actions));
        _selection.CollapseTo(pos);

        UpdateState(Collect(query, options).Count, -1);
        return matches.Count;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private List<FindMatch> Collect(string query, FindOptions options) =>
        [.. _engine.FindAll(query, options)];

    private FindMatch? Select(List<FindMatch> matches, int index)
    {
        if (index < 0)
        {
            UpdateState(matches.Count, -1);
            return null;
        }

        var match = matches[index];
        _selection.Set(match.Range.Start, match.Range.End);
        UpdateState(matches.Count, index);
        return match;
    }

    private void UpdateState(int count, int index)
    {
        MatchCount = count;
        CurrentMatchIndex = index;
    }

    private int IndexOfNext(List<FindMatch> matches, (int Block, int Offset) origin, bool wrapAround)
    {
        for (int i = 0; i < matches.Count; i++)
        {
            if (Compare(Key(matches[i].Range.Start), origin) >= 0) return i;
        }
        return wrapAround && matches.Count > 0 ? 0 : -1;
    }

    private DocumentPosition Apply(IUndoableAction action) =>
        _undoManager is not null ? _undoManager.Push(action) : action.Execute();

    private IEnumerable<IUndoableAction> CreateReplaceActions(FindMatch match, string replacement)
    {
        // Capture the style before deleting so the replacement keeps the match's formatting.
        var style = GetStyleAt(match.Range.Start);
        yield return new DeleteRangeAction(_document, match.Range);
        if (replacement.Length > 0)
            yield return new InsertTextAction(_document, match.Range.Start, replacement, style);
    }

    private TextStyle? GetStyleAt(DocumentPosition position)
    {
        if (position.BlockIndex < 0 || position.BlockIndex >= _document.Blocks.Count) return null;
        if (_document.Blocks[position.BlockIndex] is not Paragraph paragraph) return null;
        if (position.InlineIndex < 0 || position.InlineIndex >= paragraph.Inlines.Count) return null;

        switch (paragraph.Inlines[position.InlineIndex])
        {
            case TextRun run:
                return run.Style;
            case HyperlinkInline link:
                int remaining = position.Offset;
                foreach (var child in link.Children)
                {
                    if (remaining < child.Length)
                        return (child as TextRun)?.Style;
                    remaining -= child.Length;
                }
                return null;
            default:
                return null;
        }
    }

    /// <summary>
    /// A position as (block, character offset within the paragraph), which stays comparable even
    /// when two <see cref="DocumentPosition"/> values name the same spot via different inlines.
    /// </summary>
    private (int Block, int Offset) Key(DocumentPosition position) =>
        (position.BlockIndex, DocumentEditor.GetAbsoluteCharOffset(_document, position));

    private static int Compare((int Block, int Offset) a, (int Block, int Offset) b)
    {
        int cmp = a.Block.CompareTo(b.Block);
        return cmp != 0 ? cmp : a.Offset.CompareTo(b.Offset);
    }
}
