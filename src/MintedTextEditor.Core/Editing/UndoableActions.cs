using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Formatting;

namespace MintedTextEditor.Core.Editing;

// ── Shared snapshot helpers ─────────────────────────────────────────────────

// Snapshots keep a reference to each original object together with its mutable state at capture
// time. Restoring puts those same objects back (with that state) instead of creating copies, so
// other undo actions that hold an inline or paragraph reference (e.g. InsertImageAction) still
// find it after an undo.
internal abstract record InlineSnapshot(Inline Inline);
internal record TextRunSnapshot(TextRun Run, string Text, TextStyle Style) : InlineSnapshot(Run);
internal record ImageInlineSnapshot(ImageInline Image, string Source, string AltText, float Width, float Height) : InlineSnapshot(Image);
internal record HyperlinkInlineSnapshot(HyperlinkInline Link, string Url, string? Title, IReadOnlyList<InlineSnapshot> Children) : InlineSnapshot(Link);
// Inlines without mutable state of their own (LineBreak, and any other inline type).
internal record OpaqueInlineSnapshot(Inline Inline) : InlineSnapshot(Inline);
internal record ParagraphSnapshot(Paragraph Paragraph, ParagraphStyle Style, IReadOnlyList<InlineSnapshot> Inlines);
// A top-level block together with snapshots of the paragraphs in it that an edit may change
// (the block itself for a paragraph; the cell paragraphs in the edited range for a table).
internal record BlockSnapshot(Block Block, IReadOnlyList<ParagraphSnapshot> Paragraphs);

/// <summary>
/// A position stored as a character offset within its paragraph (or table-cell paragraph)
/// instead of an (inline index, offset) pair. Run splits and merges from formatting are not
/// on the undo stack, so inline indices recorded by an action can go stale; paragraph
/// offsets stay valid and are resolved back to a position when the action replays.
/// A position that is not in a paragraph (e.g. a table block without cell coordinates)
/// is kept as is, so it still names its own block rather than the document's last block.
/// </summary>
internal readonly struct ParagraphAnchor
{
    private readonly DocumentPosition _context;
    private readonly int _offset;
    private readonly bool _inParagraph;
    private readonly bool _atHyperlinkStart;

    /// <param name="document">The document containing <paramref name="position"/>.</param>
    /// <param name="position">The position to anchor.</param>
    /// <param name="keepHyperlinkStart">
    /// When true and <paramref name="position"/> is at the start of a hyperlink, <see cref="Resolve"/>
    /// returns the start of the hyperlink rather than the end of the inline before it.
    /// </param>
    public ParagraphAnchor(Document.Document document, DocumentPosition position, bool keepHyperlinkStart = false)
    {
        _context = position;
        _inParagraph = IsParagraphPosition(document, position);
        _offset = _inParagraph ? DocumentEditor.GetAbsoluteCharOffset(document, position) : position.Offset;
        _atHyperlinkStart = _inParagraph && keepHyperlinkStart && position.Offset == 0
            && DocumentEditor.GetHyperlinkAt(document, position) is not null;
    }

    public DocumentPosition Resolve(Document.Document document)
    {
        if (!_inParagraph || !IsParagraphPosition(document, _context))
            return _context;
        var position = DocumentEditor.AbsoluteCharOffsetToPosition(document, _context, _offset);
        return _atHyperlinkStart ? DocumentEditor.PreferFollowingHyperlink(document, position) : position;
    }

    public bool SameAs(ParagraphAnchor other)
        => _context.BlockIndex == other._context.BlockIndex
           && _context.CellRow == other._context.CellRow
           && _context.CellCol == other._context.CellCol
           && _inParagraph == other._inParagraph
           && (_inParagraph ? _offset == other._offset : _context == other._context);

    // Mirrors DocumentEditor.GetParagraph: a top-level paragraph, or the first paragraph of a table cell.
    private static bool IsParagraphPosition(Document.Document document, DocumentPosition position)
    {
        if (position.BlockIndex < 0 || position.BlockIndex >= document.Blocks.Count)
            return false;
        var block = document.Blocks[position.BlockIndex];
        if (!position.IsInTableCell)
            return block is Paragraph;
        return block is TableBlock table
            && table.GetCell(position.CellRow, position.CellCol) is { Blocks.Count: > 0 } cell
            && cell.Blocks[0] is Paragraph;
    }
}

internal static class SnapshotHelper
{
    internal static ParagraphSnapshot Capture(Paragraph p) =>
        new(p, p.Style.Clone(), p.Inlines.Select(CaptureInline).ToList());

    private static InlineSnapshot CaptureInline(Inline inline) => inline switch
    {
        TextRun t => new TextRunSnapshot(t, t.Text, t.Style),
        ImageInline i => new ImageInlineSnapshot(i, i.Source, i.AltText, i.Width, i.Height),
        HyperlinkInline h => new HyperlinkInlineSnapshot(h, h.Url, h.Title, h.Children.Select(CaptureInline).ToList()),
        _ => new OpaqueInlineSnapshot(inline)
    };

    /// <summary>
    /// Puts the inlines (and style) recorded in <paramref name="snapshot"/> back into
    /// <paramref name="p"/>, without changing <paramref name="p"/>'s identity or parent.
    /// The recorded inline objects themselves are reinserted with their recorded state.
    /// </summary>
    internal static void Restore(Paragraph p, ParagraphSnapshot snapshot)
    {
        p.Style = snapshot.Style.Clone();
        // Remove existing inlines from back to front to keep indices stable
        for (int i = p.Inlines.Count - 1; i >= 0; i--)
            p.RemoveInline(i);
        foreach (var snap in snapshot.Inlines)
            p.AddInline(RestoreInline(p, snap));
    }

    /// <summary>
    /// Returns the paragraph recorded in <paramref name="snapshot"/>, restored to its recorded
    /// content, for reinsertion into the document.
    /// </summary>
    internal static Paragraph CreateParagraph(ParagraphSnapshot snapshot)
    {
        Restore(snapshot.Paragraph, snapshot);
        return snapshot.Paragraph;
    }

    /// <summary>
    /// Records every top-level block from <paramref name="range"/>'s start block to its end block,
    /// tables included, with snapshots of the paragraphs an edit of the range may change: each
    /// top-level paragraph, and the cell paragraphs of a table that are in the range.
    /// </summary>
    internal static List<BlockSnapshot> CaptureBlocks(Document.Document document, TextRange range)
    {
        var cellParagraphs = DocumentEditor.GetParagraphsInRange(document, range)
            .Select(x => x.Paragraph)
            .ToHashSet();

        var snapshots = new List<BlockSnapshot>();
        for (int i = Math.Max(0, range.Start.BlockIndex); i <= range.End.BlockIndex && i < document.Blocks.Count; i++)
        {
            var block = document.Blocks[i];
            IReadOnlyList<ParagraphSnapshot> paragraphs = block switch
            {
                Paragraph p => [Capture(p)],
                TableBlock t => t.Rows
                    .SelectMany(r => r.Cells)
                    .SelectMany(c => c.Blocks)
                    .OfType<Paragraph>()
                    .Where(cellParagraphs.Contains)
                    .Select(Capture)
                    .ToList(),
                _ => [],
            };
            snapshots.Add(new BlockSnapshot(block, paragraphs));
        }
        return snapshots;
    }

    /// <summary>
    /// Restores the consecutive blocks recorded by <see cref="CaptureBlocks"/> starting at
    /// <paramref name="startBlock"/>. A recorded block that was removed (merged or deleted away,
    /// a table included) is reinserted at its original index, and every recorded paragraph is
    /// restored in place. Returns whether any block was reinserted, in which case later blocks
    /// have moved and the caller must report a structural change.
    /// </summary>
    internal static bool RestoreBlocks(Document.Document document, int startBlock, IReadOnlyList<BlockSnapshot> snapshots)
    {
        bool reinserted = false;
        for (int i = 0; i < snapshots.Count; i++)
        {
            var block = snapshots[i].Block;
            if (!document.Blocks.Contains(block))
            {
                document.InsertBlock(Math.Min(startBlock + i, document.Blocks.Count), block);
                reinserted = true;
            }
            foreach (var paragraph in snapshots[i].Paragraphs)
                Restore(paragraph.Paragraph, paragraph);
        }
        return reinserted;
    }

    private static Inline RestoreInline(Paragraph p, InlineSnapshot snap)
    {
        switch (snap)
        {
            case TextRunSnapshot t:
                t.Run.Text = t.Text;
                t.Run.Style = t.Style;
                break;
            case ImageInlineSnapshot i:
                i.Image.Source = i.Source;
                i.Image.AltText = i.AltText;
                i.Image.Width = i.Width;
                i.Image.Height = i.Height;
                break;
            case HyperlinkInlineSnapshot h:
                h.Link.Url = h.Url;
                h.Link.Title = h.Title;
                h.Link.Children.Clear();
                foreach (var c in h.Children)
                    h.Link.Children.Add(RestoreInline(p, c));
                break;
        }
        snap.Inline.Parent = p;
        return snap.Inline;
    }
}

// ── InsertTextAction ────────────────────────────────────────────────────────

/// <summary>Records a text-insertion so it can be undone by deleting the inserted range.</summary>
public sealed class InsertTextAction : IUndoableAction
{
    private readonly Document.Document _document;
    private readonly DocumentPosition _position;
    private string _text;
    private readonly TextStyle? _style;
    private DocumentPosition _resultPosition;
    private ParagraphAnchor? _startAnchor;
    private ParagraphAnchor _endAnchor;

    /// <summary>Creates an action that inserts <paramref name="text"/> at <paramref name="position"/> with an optional style.</summary>
    public InsertTextAction(Document.Document document, DocumentPosition position, string text, TextStyle? style = null)
    {
        _document = document;
        _position = position;
        _text = text;
        _style = style;
    }

    /// <inheritdoc/>
    public string Description => $"Insert \"{_text}\"";

    /// <summary>
    /// When true, text inserted at the start or end of a hyperlink goes outside the link instead
    /// of extending it (see <see cref="DocumentEditor.InsertText(Document.Document, DocumentPosition, string, TextStyle?, bool)"/>).
    /// Set it for text typed or pasted at a collapsed caret; leave it false when the text replaces a selection.
    /// </summary>
    public bool EscapeHyperlinkBoundary { get; init; }

    /// <inheritdoc/>
    public DocumentPosition Execute()
    {
        var start = _startAnchor is { } anchor ? anchor.Resolve(_document) : _position;
        _startAnchor ??= new ParagraphAnchor(_document, start, keepHyperlinkStart: true);
        _resultPosition = DocumentEditor.InsertText(_document, start, _text, _style, EscapeHyperlinkBoundary);
        _endAnchor = new ParagraphAnchor(_document, _resultPosition);
        return _resultPosition;
    }

    /// <inheritdoc/>
    public DocumentPosition Undo()
    {
        var start = _startAnchor?.Resolve(_document) ?? _position;
        var end = _endAnchor.Resolve(_document);
        DocumentEditor.DeleteRange(_document, new TextRange(start, end));
        return start;
    }

    /// <inheritdoc/>
    public DocumentPosition Redo() => Execute();

    /// <summary>
    /// Merges consecutive single-character (or small word) insertions at the same position
    /// so that a run of typed characters forms one undo step.
    /// </summary>
    public bool MergeWith(IUndoableAction next)
    {
        if (next is InsertTextAction insert &&
            insert._startAnchor is { } nextStart &&
            nextStart.SameAs(_endAnchor) &&
            insert._style == _style &&
            insert.EscapeHyperlinkBoundary == EscapeHyperlinkBoundary)
        {
            _text += insert._text;
            _resultPosition = insert._resultPosition;
            _endAnchor = insert._endAnchor;
            return true;
        }
        return false;
    }
}

// ── DeleteRangeAction ───────────────────────────────────────────────────────

/// <summary>
/// Records a range deletion. Snapshots all affected blocks before the delete (paragraphs, the
/// table-cell paragraphs in the range, and any table between the range's ends, which the delete
/// removes) so they can be fully restored on undo.
/// </summary>
public sealed class DeleteRangeAction : IUndoableAction
{
    private readonly Document.Document _document;
    private TextRange _range;
    private readonly ParagraphAnchor _startAnchor;
    private readonly ParagraphAnchor _endAnchor;
    private List<BlockSnapshot> _snapshots = [];

    /// <summary>Creates an action that deletes <paramref name="range"/> from the document.</summary>
    public DeleteRangeAction(Document.Document document, TextRange range)
    {
        _document = document;
        _range = range;
        _startAnchor = new ParagraphAnchor(document, range.Start);
        _endAnchor = new ParagraphAnchor(document, range.End);
    }

    /// <inheritdoc/>
    public string Description => "Delete";

    /// <inheritdoc/>
    public DocumentPosition Execute()
    {
        _range = new TextRange(_startAnchor.Resolve(_document), _endAnchor.Resolve(_document));
        _snapshots = SnapshotHelper.CaptureBlocks(_document, _range);
        return DocumentEditor.DeleteRange(_document, _range);
    }

    /// <inheritdoc/>
    public DocumentPosition Undo()
    {
        if (_snapshots.Count == 0) return _range.Start;

        // Restore the first paragraph in place and re-insert the blocks (paragraphs and tables)
        // removed by a multi-block range. Reinserted blocks shift every later block, so report a
        // structural change for them.
        bool reinserted = SnapshotHelper.RestoreBlocks(_document, _range.Start.BlockIndex, _snapshots);
        _document.NotifyChanged(new DocumentChangedEventArgs(
            reinserted ? DocumentChangeType.BlockSplit : DocumentChangeType.TextInserted, _range));
        return _startAnchor.Resolve(_document);
    }

    /// <inheritdoc/>
    public DocumentPosition Redo() => Execute();

    /// <inheritdoc/>
    public bool MergeWith(IUndoableAction next) => false;
}

// ── SplitBlockAction ────────────────────────────────────────────────────────

/// <summary>
/// Records a block split. Undo merges the two resulting blocks back,
/// perfectly restoring the original paragraph.
/// </summary>
public sealed class SplitBlockAction : IUndoableAction
{
    private readonly Document.Document _document;
    private readonly DocumentPosition _position;
    private readonly ParagraphAnchor _anchor;
    private DocumentPosition _resultPosition;

    /// <summary>Creates an action that splits the block at <paramref name="position"/>.</summary>
    public SplitBlockAction(Document.Document document, DocumentPosition position)
    {
        _document = document;
        _position = position;
        _anchor = new ParagraphAnchor(document, position);
    }

    /// <inheritdoc/>
    public string Description => "Split Paragraph";

    /// <inheritdoc/>
    public DocumentPosition Execute()
    {
        _resultPosition = DocumentEditor.SplitBlock(_document, _anchor.Resolve(_document));
        return _resultPosition;
    }

    /// <inheritdoc/>
    public DocumentPosition Undo()
    {
        DocumentEditor.MergeBlocks(_document, _position.BlockIndex);
        return _anchor.Resolve(_document);
    }

    /// <inheritdoc/>
    public DocumentPosition Redo() => Execute();

    /// <inheritdoc/>
    public bool MergeWith(IUndoableAction next) => false;
}

// ── MergeBlocksAction ───────────────────────────────────────────────────────

/// <summary>
/// Records a block merge. Snapshots both source paragraphs before merging so
/// they can be fully restored on undo.
/// </summary>
public sealed class MergeBlocksAction : IUndoableAction
{
    private readonly Document.Document _document;
    private readonly int _blockIndex;
    private ParagraphSnapshot? _firstSnapshot;
    private ParagraphSnapshot? _secondSnapshot;
    private DocumentPosition _resultPosition;

    /// <summary>Creates an action that merges the block at <paramref name="blockIndex"/> with the block after it.</summary>
    public MergeBlocksAction(Document.Document document, int blockIndex)
    {
        _document = document;
        _blockIndex = blockIndex;
    }

    /// <inheritdoc/>
    public string Description => "Merge Paragraphs";

    /// <inheritdoc/>
    public DocumentPosition Execute()
    {
        if (_blockIndex < _document.Blocks.Count && _document.Blocks[_blockIndex] is Paragraph p1)
            _firstSnapshot = SnapshotHelper.Capture(p1);
        if (_blockIndex + 1 < _document.Blocks.Count && _document.Blocks[_blockIndex + 1] is Paragraph p2)
            _secondSnapshot = SnapshotHelper.Capture(p2);

        _resultPosition = DocumentEditor.MergeBlocks(_document, _blockIndex);
        return _resultPosition;
    }

    /// <inheritdoc/>
    public DocumentPosition Undo()
    {
        if (_firstSnapshot == null) return _resultPosition;

        // Restore first paragraph in-place (it survived the merge)
        if (_blockIndex < _document.Blocks.Count && _document.Blocks[_blockIndex] is Paragraph first)
            SnapshotHelper.Restore(first, _firstSnapshot);

        // Re-insert the second paragraph that was removed by the merge
        if (_secondSnapshot != null)
        {
            var p = SnapshotHelper.CreateParagraph(_secondSnapshot);
            _document.InsertBlock(_blockIndex + 1, p);
        }

        _document.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.BlockSplit,
            new TextRange(_resultPosition, _resultPosition)));
        return _resultPosition;
    }

    /// <inheritdoc/>
    public DocumentPosition Redo() => Execute();

    /// <inheritdoc/>
    public bool MergeWith(IUndoableAction next) => false;
}

// ── ApplyStyleAction ────────────────────────────────────────────────────────

/// <summary>
/// Records a character-formatting change over a range. Snapshots the inlines of every
/// affected paragraph before applying, so undo restores the original runs with their exact
/// styles. Redo applies the change again to the restored runs.
/// </summary>
/// <remarks>
/// Undo restores only the paragraphs' inlines and leaves their <see cref="ParagraphStyle"/>
/// alone, since character formatting never changes it. Runs split by the change are merged
/// back with same-styled neighbours after it is applied.
/// </remarks>
public sealed class ApplyStyleAction : IUndoableAction
{
    private readonly Document.Document _document;
    private TextRange _range;
    private readonly ParagraphAnchor _startAnchor;
    private readonly ParagraphAnchor _endAnchor;
    private readonly Action<TextRange> _apply;
    private readonly List<ParagraphSnapshot> _snapshots = [];

    /// <summary>Creates an action that applies <paramref name="transform"/> to the style of the text in <paramref name="range"/>.</summary>
    public ApplyStyleAction(Document.Document document, TextRange range, Func<TextStyle, TextStyle> transform)
        : this(document, range, r => DocumentEditor.ApplyTextStyle(document, r, transform), "Apply Style")
    {
    }

    /// <summary>
    /// Creates an action that runs <paramref name="apply"/> over <paramref name="range"/>. Use it for
    /// formatting operations that choose their own transform, such as the <see cref="FormattingEngine"/>
    /// toggles, which add a format unless the whole range already has it.
    /// </summary>
    /// <param name="document">The document to format.</param>
    /// <param name="range">The range to format.</param>
    /// <param name="apply">
    /// Formats the range it is given. It may split and restyle the text runs of the paragraphs the
    /// range covers, but must not add, remove or move text or blocks. It runs again on redo, against
    /// the same runs it saw the first time.
    /// </param>
    /// <param name="description">Description of the undo step, e.g. "Bold".</param>
    public ApplyStyleAction(Document.Document document, TextRange range, Action<TextRange> apply, string description)
    {
        _document = document;
        _range = range;
        _startAnchor = new ParagraphAnchor(document, range.Start);
        _endAnchor = new ParagraphAnchor(document, range.End);
        _apply = apply;
        Description = description;
    }

    /// <inheritdoc/>
    public string Description { get; }

    /// <inheritdoc/>
    public DocumentPosition Execute()
    {
        _range = new TextRange(_startAnchor.Resolve(_document), _endAnchor.Resolve(_document));
        _snapshots.Clear();
        // Top-level paragraphs and the table-cell paragraphs in the range.
        foreach (var (p, _, _) in DocumentEditor.GetParagraphsInRange(_document, _range))
            _snapshots.Add(SnapshotHelper.Capture(p));

        _apply(_range);

        foreach (var snapshot in _snapshots)
            DocumentEditor.NormalizeInlines(snapshot.Paragraph);
        return _endAnchor.Resolve(_document);
    }

    /// <inheritdoc/>
    public DocumentPosition Undo()
    {
        foreach (var snapshot in _snapshots)
            RestoreInlines(snapshot.Paragraph, snapshot);
        _document.NotifyChanged(new DocumentChangedEventArgs(DocumentChangeType.StyleChanged, _range));
        return _endAnchor.Resolve(_document);
    }

    /// <summary>
    /// Restores the inlines recorded in <paramref name="snapshot"/> but keeps <paramref name="p"/>'s
    /// current paragraph style, which inline-only changes never alter.
    /// </summary>
    internal static void RestoreInlines(Paragraph p, ParagraphSnapshot snapshot)
    {
        var style = p.Style;
        SnapshotHelper.Restore(p, snapshot);
        p.Style = style;
    }

    /// <inheritdoc/>
    public DocumentPosition Redo() => Execute();

    /// <inheritdoc/>
    public bool MergeWith(IUndoableAction next) => false;
}

// ── ApplyParagraphStyleAction ───────────────────────────────────────────────

/// <summary>
/// Records a paragraph-level formatting change (alignment, heading, list, indent, line spacing,
/// block quote, …). Snapshots the <see cref="ParagraphStyle"/> of every paragraph in the range
/// before and after the change, so undo restores the previous styles and redo reapplies the
/// recorded result in one step.
/// </summary>
public sealed class ApplyParagraphStyleAction : IUndoableAction
{
    private readonly Document.Document _document;
    private TextRange _range;
    private readonly ParagraphAnchor _startAnchor;
    private readonly ParagraphAnchor _endAnchor;
    private readonly Action<Document.Document, TextRange> _apply;
    private readonly List<(int BlockIndex, ParagraphStyle Before, ParagraphStyle After)> _changes = [];

    /// <summary>
    /// Creates an action that runs <paramref name="apply"/> over <paramref name="range"/>, for example
    /// <c>(d, r) =&gt; ParagraphFormattingEngine.SetAlignment(d, r, TextAlignment.Center)</c>.
    /// </summary>
    /// <param name="document">The document to modify.</param>
    /// <param name="range">The range whose paragraphs are formatted.</param>
    /// <param name="apply">Changes the paragraph styles of the paragraphs in the range. Only the styles of the
    /// paragraphs from <paramref name="range"/>'s start block to its end block are recorded.</param>
    /// <param name="description">Human-readable description of the change.</param>
    public ApplyParagraphStyleAction(
        Document.Document document,
        TextRange range,
        Action<Document.Document, TextRange> apply,
        string description = "Paragraph Format")
    {
        _document = document;
        _range = range;
        _startAnchor = new ParagraphAnchor(document, range.Start);
        _endAnchor = new ParagraphAnchor(document, range.End);
        _apply = apply;
        Description = description;
    }

    /// <inheritdoc/>
    public string Description { get; }

    /// <inheritdoc/>
    public DocumentPosition Execute()
    {
        _range = new TextRange(_startAnchor.Resolve(_document), _endAnchor.Resolve(_document));
        var before = new List<(int, ParagraphStyle)>();
        for (int i = _range.Start.BlockIndex; i <= _range.End.BlockIndex && i < _document.Blocks.Count; i++)
        {
            if (_document.Blocks[i] is Paragraph p)
                before.Add((i, p.Style.Clone()));
        }

        _apply(_document, _range);

        _changes.Clear();
        foreach (var (index, style) in before)
        {
            if (index < _document.Blocks.Count && _document.Blocks[index] is Paragraph p)
                _changes.Add((index, style, p.Style.Clone()));
        }
        return _range.Start;
    }

    /// <inheritdoc/>
    public DocumentPosition Undo() => Restore(after: false);

    /// <inheritdoc/>
    public DocumentPosition Redo() => Restore(after: true);

    /// <inheritdoc/>
    public bool MergeWith(IUndoableAction next) => false;

    private DocumentPosition Restore(bool after)
    {
        foreach (var (index, before, afterStyle) in _changes)
        {
            if (index < _document.Blocks.Count && _document.Blocks[index] is Paragraph p)
                p.Style = (after ? afterStyle : before).Clone();
        }
        _range = new TextRange(_startAnchor.Resolve(_document), _endAnchor.Resolve(_document));
        _document.NotifyChanged(new DocumentChangedEventArgs(DocumentChangeType.StyleChanged, _range));
        return _range.Start;
    }
}

// ── CompositeAction ─────────────────────────────────────────────────────────

/// <summary>
/// Wraps multiple undoable actions into a single undo step (e.g. replace = delete + insert).
/// </summary>
public sealed class CompositeAction : IUndoableAction
{
    private readonly IReadOnlyList<IUndoableAction> _actions;
    private DocumentPosition _resultPosition;

    /// <summary>Creates a composite that executes <paramref name="actions"/> in order and undoes them in reverse order.</summary>
    public CompositeAction(IEnumerable<IUndoableAction> actions)
    {
        _actions = [.. actions];
    }

    /// <inheritdoc/>
    public string Description => string.Join(" + ", _actions.Select(a => a.Description));

    /// <inheritdoc/>
    public DocumentPosition Execute()
    {
        foreach (var a in _actions)
            _resultPosition = a.Execute();
        return _resultPosition;
    }

    /// <inheritdoc/>
    public DocumentPosition Undo()
    {
        DocumentPosition pos = _resultPosition;
        for (int i = _actions.Count - 1; i >= 0; i--)
            pos = _actions[i].Undo();
        return pos;
    }

    /// <inheritdoc/>
    public DocumentPosition Redo()
    {
        foreach (var a in _actions)
            _resultPosition = a.Redo();
        return _resultPosition;
    }

    /// <inheritdoc/>
    public bool MergeWith(IUndoableAction next) => false;
}

// ── ReplaceMatchesAction ────────────────────────────────────────────────────

/// <summary>
/// Replaces a set of find matches as one undo step. Each affected paragraph is snapshotted
/// once before and once after the edit, so the memory held does not grow with the number of
/// matches. Matches are stored as paragraph character offsets and applied last to first, so
/// the offsets of earlier matches stay valid while later ones change length.
/// </summary>
internal sealed class ReplaceMatchesAction : IUndoableAction
{
    private readonly record struct Span(int Block, int Start, int End);

    private readonly Document.Document _document;
    private readonly IReadOnlyList<Span> _spans;
    private readonly string _replacement;
    private readonly List<(int Block, ParagraphSnapshot Snapshot)> _before = [];
    private readonly List<(int Block, ParagraphSnapshot Snapshot)> _after = [];

    /// <summary>
    /// Creates an action that replaces <paramref name="matches"/> (in document order, each within
    /// one top-level paragraph, as <see cref="FindReplaceEngine"/> returns them) with
    /// <paramref name="replacement"/>.
    /// </summary>
    public ReplaceMatchesAction(Document.Document document, IEnumerable<FindMatch> matches, string replacement)
    {
        _document = document;
        _replacement = replacement ?? string.Empty;
        _spans = [.. matches.Select(m => new Span(
            m.Range.Start.BlockIndex,
            DocumentEditor.GetAbsoluteCharOffset(document, m.Range.Start),
            DocumentEditor.GetAbsoluteCharOffset(document, m.Range.End)))];
    }

    /// <summary>Number of paragraph snapshots currently held (for tests).</summary>
    internal int SnapshotCount => _before.Count + _after.Count;

    /// <inheritdoc/>
    public string Description => _spans.Count == 1 ? "Replace" : "Replace All";

    /// <inheritdoc/>
    public DocumentPosition Execute()
    {
        _before.Clear();
        _after.Clear();
        if (_spans.Count == 0) return default;

        Capture(_before);
        for (int i = _spans.Count - 1; i >= 0; i--)
            ReplaceSpan(_spans[i]);
        Capture(_after);

        return PositionAt(_spans[0].Block, _spans[0].Start + _replacement.Length);
    }

    /// <inheritdoc/>
    public DocumentPosition Undo()
    {
        if (_spans.Count == 0) return default;
        Restore(_before);
        return PositionAt(_spans[0].Block, _spans[0].Start);
    }

    /// <inheritdoc/>
    public DocumentPosition Redo()
    {
        if (_spans.Count == 0) return default;
        Restore(_after);
        return PositionAt(_spans[0].Block, _spans[0].Start + _replacement.Length);
    }

    /// <inheritdoc/>
    public bool MergeWith(IUndoableAction next) => false;

    private void ReplaceSpan(Span span)
    {
        if (span.Block >= _document.Blocks.Count || _document.Blocks[span.Block] is not Paragraph paragraph)
            return;

        var context = new DocumentPosition(span.Block, 0, 0);
        if (_replacement.Length > 0 && TextPositionOfChar(paragraph, context, span.Start) is { } at)
        {
            // Insert in front of the match, inside the inline holding its first character, then
            // delete the match. The replacement keeps that run's style (and hyperlink) even when
            // the match is the whole run, which deleting first would remove.
            DocumentEditor.InsertText(_document, at, _replacement, StyleAt(paragraph, at));
            Delete(context, span.Start + _replacement.Length, span.End + _replacement.Length);
        }
        else
        {
            Delete(context, span.Start, span.End);
            if (_replacement.Length > 0)
                DocumentEditor.InsertText(_document, PositionAt(span.Block, span.Start), _replacement);
        }
    }

    private void Delete(DocumentPosition context, int start, int end) =>
        DocumentEditor.DeleteRange(_document, new TextRange(
            DocumentEditor.AbsoluteCharOffsetToPosition(_document, context, start),
            DocumentEditor.AbsoluteCharOffsetToPosition(_document, context, end)));

    private DocumentPosition PositionAt(int block, int offset) =>
        DocumentEditor.AbsoluteCharOffsetToPosition(_document, new DocumentPosition(block, 0, 0), offset);

    /// <summary>
    /// The position of the character at <paramref name="offset"/> within the inline that holds
    /// it (not the end of the inline before), or <c>null</c> when that inline is not text.
    /// </summary>
    private static DocumentPosition? TextPositionOfChar(Paragraph paragraph, DocumentPosition context, int offset)
    {
        int accumulated = 0;
        for (int i = 0; i < paragraph.Inlines.Count; i++)
        {
            var inline = paragraph.Inlines[i];
            if (offset < accumulated + inline.Length)
                return inline is TextRun or HyperlinkInline ? context.With(i, offset - accumulated) : null;
            accumulated += inline.Length;
        }
        return null;
    }

    private static TextStyle? StyleAt(Paragraph paragraph, DocumentPosition position)
    {
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

    private void Capture(List<(int Block, ParagraphSnapshot Snapshot)> snapshots)
    {
        int previous = -1;
        foreach (var span in _spans)
        {
            if (span.Block == previous) continue;
            previous = span.Block;
            if (span.Block < _document.Blocks.Count && _document.Blocks[span.Block] is Paragraph p)
                snapshots.Add((span.Block, SnapshotHelper.Capture(p)));
        }
    }

    private void Restore(List<(int Block, ParagraphSnapshot Snapshot)> snapshots)
    {
        foreach (var (block, snapshot) in snapshots)
        {
            if (block < _document.Blocks.Count && _document.Blocks[block] is Paragraph p)
                SnapshotHelper.Restore(p, snapshot);
        }

        var first = new DocumentPosition(_spans[0].Block, 0, 0);
        var last = new DocumentPosition(_spans[^1].Block, 0, 0);
        _document.NotifyChanged(new DocumentChangedEventArgs(DocumentChangeType.TextInserted, new TextRange(first, last)));
    }
}

// ── InsertImageAction ───────────────────────────────────────────────────────

/// <summary>
/// Records an image insertion so it can be undone by removing the inserted image.
/// </summary>
public sealed class InsertImageAction : IUndoableAction
{
    private readonly Document.Document _document;
    private readonly DocumentPosition _insertPosition;
    private readonly string _source;
    private readonly string _altText;
    private readonly float _width;
    private readonly float _height;
    private ImageInline? _inserted;
    private DocumentPosition _resultPosition;

    /// <summary>Creates an action that inserts an image at <paramref name="insertPosition"/>.</summary>
    public InsertImageAction(Document.Document document, DocumentPosition insertPosition,
        string source, string altText = "", float width = 0, float height = 0)
    {
        _document = document;
        _insertPosition = insertPosition;
        _source = source;
        _altText = altText;
        _width = width;
        _height = height;
    }

    /// <inheritdoc/>
    public string Description => "Insert Image";

    /// <inheritdoc/>
    public DocumentPosition Execute()
    {
        _inserted = ImageEngine.InsertImage(_document, _insertPosition, _source, _altText, _width, _height);
        if (_inserted.Parent is Paragraph para)
        {
            int blockIdx = _document.Blocks.IndexOf(para);
            int imgIdx = para.Inlines.IndexOf(_inserted);
            _resultPosition = new DocumentPosition(blockIdx, imgIdx, 1);
        }
        else
        {
            _resultPosition = _insertPosition;
        }
        return _resultPosition;
    }

    /// <inheritdoc/>
    public DocumentPosition Undo()
    {
        if (_inserted is not null)
            ImageEngine.RemoveImage(_document, _inserted);
        return _insertPosition;
    }

    /// <inheritdoc/>
    public DocumentPosition Redo() => Execute();

    /// <inheritdoc/>
    public bool MergeWith(IUndoableAction next) => false;
}

// ── InsertHyperlinkAction ───────────────────────────────────────────────────

/// <summary>
/// Records a hyperlink insertion. Snapshots the affected paragraphs before inserting so undo
/// restores the original text and formatting exactly, and redo can replay the insertion.
/// </summary>
public sealed class InsertHyperlinkAction : IUndoableAction
{
    private readonly Document.Document _document;
    private TextRange _range;
    private readonly ParagraphAnchor _startAnchor;
    private readonly ParagraphAnchor _endAnchor;
    private readonly string _url;
    private readonly string? _displayText;
    private List<BlockSnapshot> _snapshots = [];

    /// <summary>Creates an action that inserts a hyperlink to <paramref name="url"/> over <paramref name="originalRange"/>.</summary>
    public InsertHyperlinkAction(Document.Document document, TextRange originalRange, string url)
        : this(document, originalRange, url, displayText: null)
    {
    }

    /// <summary>
    /// Creates an action that inserts a hyperlink to <paramref name="url"/> over <paramref name="originalRange"/>
    /// with <paramref name="displayText"/> as its text. When <paramref name="displayText"/> is non-empty (not null
    /// or whitespace) it replaces the text in <paramref name="originalRange"/>; otherwise the text in the range is
    /// linked, or the URL is inserted as the text when the range is empty. Undo restores the original text.
    /// </summary>
    public InsertHyperlinkAction(Document.Document document, TextRange originalRange, string url, string? displayText)
    {
        _document = document;
        _range = originalRange;
        _startAnchor = new ParagraphAnchor(document, originalRange.Start);
        _endAnchor = new ParagraphAnchor(document, originalRange.End);
        _url = url;
        _displayText = displayText;
    }

    /// <inheritdoc/>
    public string Description => "Insert Hyperlink";

    /// <inheritdoc/>
    public DocumentPosition Execute()
    {
        _range = new TextRange(_startAnchor.Resolve(_document), _endAnchor.Resolve(_document));
        _snapshots = SnapshotHelper.CaptureBlocks(_document, _range);

        var inserted = HyperlinkEngine.InsertHyperlink(_document, _range, _url, title: null, _displayText);
        // The link is in the range's start paragraph (a top-level paragraph or a table cell's).
        if (inserted.Parent is Paragraph para && para.Inlines.IndexOf(inserted) is var linkIdx and >= 0)
            return _range.Start.With(linkIdx, inserted.Length);
        return _range.Start;
    }

    /// <inheritdoc/>
    public DocumentPosition Undo()
    {
        if (_snapshots.Count == 0) return _range.Start;

        // Restore the first paragraph in place and re-insert any merged away by a multi-block
        // selection. Reinserted blocks shift every later block, so report a structural change for them.
        bool reinserted = SnapshotHelper.RestoreBlocks(_document, _range.Start.BlockIndex, _snapshots);
        _document.NotifyChanged(new DocumentChangedEventArgs(
            reinserted ? DocumentChangeType.BlockSplit : DocumentChangeType.TextDeleted, _range));
        return _startAnchor.Resolve(_document);
    }

    /// <inheritdoc/>
    public DocumentPosition Redo() => Execute();

    /// <inheritdoc/>
    public bool MergeWith(IUndoableAction next) => false;
}

// ── RemoveHyperlinkAction ───────────────────────────────────────────────────

/// <summary>
/// Records the removal of a hyperlink, whose text stays in place with its formatting.
/// Snapshots the paragraph's inlines so undo restores the link exactly.
/// </summary>
public sealed class RemoveHyperlinkAction : IUndoableAction
{
    private readonly Document.Document _document;
    private readonly DocumentPosition _position;
    private readonly ParagraphAnchor _anchor;
    private ParagraphSnapshot? _snapshot;

    /// <summary>Creates an action that removes the hyperlink at <paramref name="position"/>.</summary>
    public RemoveHyperlinkAction(Document.Document document, DocumentPosition position)
    {
        _document = document;
        _position = position;
        _anchor = new ParagraphAnchor(document, position);
    }

    /// <inheritdoc/>
    public string Description => "Remove Hyperlink";

    /// <inheritdoc/>
    public DocumentPosition Execute()
    {
        _snapshot = null;
        if (HyperlinkEngine.GetHyperlinkAtPosition(_document, _position) is not null &&
            _document.Blocks[_position.BlockIndex] is Paragraph para)
        {
            _snapshot = SnapshotHelper.Capture(para);
            HyperlinkEngine.RemoveHyperlink(_document, _position);
        }
        return _anchor.Resolve(_document);
    }

    /// <inheritdoc/>
    public DocumentPosition Undo()
    {
        if (_snapshot is not null &&
            _position.BlockIndex < _document.Blocks.Count &&
            _document.Blocks[_position.BlockIndex] is Paragraph para)
        {
            ApplyStyleAction.RestoreInlines(para, _snapshot);
            _document.NotifyChanged(new DocumentChangedEventArgs(
                DocumentChangeType.TextInserted,
                new TextRange(_position, _position)));
        }
        return _anchor.Resolve(_document);
    }

    /// <inheritdoc/>
    public DocumentPosition Redo() => Execute();

    /// <inheritdoc/>
    public bool MergeWith(IUndoableAction next) => false;
}

// ── InsertTableAction ─────────────────────────────────────────────────────

/// <summary>
/// Records a table insertion so it can be undone by removing the inserted <see cref="TableBlock"/>.
/// </summary>
public sealed class InsertTableAction : IUndoableAction
{
    private readonly Document.Document _document;
    private readonly DocumentPosition _insertPosition;
    private readonly int _rows;
    private readonly int _cols;
    private TableBlock? _inserted;
    private DocumentPosition _resultPosition;

    /// <summary>Creates an action that inserts a table with <paramref name="rows"/> rows and <paramref name="cols"/> columns at <paramref name="insertPosition"/>.</summary>
    public InsertTableAction(Document.Document document, DocumentPosition insertPosition, int rows, int cols)
    {
        _document = document;
        _insertPosition = insertPosition;
        _rows = rows;
        _cols = cols;
    }

    /// <inheritdoc/>
    public string Description => "Insert Table";

    /// <inheritdoc/>
    public DocumentPosition Execute()
    {
        _inserted = TableEngine.InsertTable(_document, _insertPosition, _rows, _cols);
        int blockIdx = _document.Blocks.IndexOf(_inserted);
        _resultPosition = blockIdx >= 0
            ? new DocumentPosition(blockIdx, 0, 0)
            : _insertPosition;
        return _resultPosition;
    }

    /// <inheritdoc/>
    public DocumentPosition Undo()
    {
        if (_inserted is not null)
        {
            int idx = _document.Blocks.IndexOf(_inserted);
            if (idx >= 0)
            {
                _document.Blocks.RemoveAt(idx);
                _document.NotifyChanged(new DocumentChangedEventArgs(
                    DocumentChangeType.TextDeleted,
                    new TextRange(_insertPosition, _insertPosition)));
            }
        }
        return _insertPosition;
    }

    /// <inheritdoc/>
    public DocumentPosition Redo() => Execute();

    /// <inheritdoc/>
    public bool MergeWith(IUndoableAction next) => false;
}

// ── InsertRowAction ──────────────────────────────────────────────────────────

/// <summary>Records a table row insertion so it can be undone by removing the inserted row.</summary>
public sealed class InsertRowAction : IUndoableAction
{
    private readonly Document.Document _document;
    private readonly TableBlock _table;
    private readonly int _afterRowIndex;
    private readonly int _originalCellRow;
    private TableRow? _inserted;

    /// <summary>Creates an action that inserts a new row into <paramref name="table"/>.</summary>
    /// <param name="document">The document that contains the table.</param>
    /// <param name="table">The table to modify.</param>
    /// <param name="afterRowIndex">Index of the row after which the new row is inserted.</param>
    /// <param name="originalCellRow">Row of the caret before the insert; the caret returns to this row on undo.</param>
    public InsertRowAction(Document.Document document, TableBlock table, int afterRowIndex, int originalCellRow)
    {
        _document = document;
        _table = table;
        _afterRowIndex = afterRowIndex;
        _originalCellRow = originalCellRow;
    }

    /// <inheritdoc/>
    public string Description => "Insert Row";

    /// <inheritdoc/>
    public DocumentPosition Execute()
    {
        _inserted = TableEngine.InsertRow(_table, _afterRowIndex);
        int blockIdx = _document.Blocks.IndexOf(_table);
        int newRowIdx = _table.Rows.IndexOf(_inserted);
        return blockIdx >= 0
            ? new DocumentPosition(blockIdx, 0, 0, newRowIdx, 0)
            : new DocumentPosition(0, 0, 0);
    }

    /// <inheritdoc/>
    public DocumentPosition Undo()
    {
        if (_inserted is not null)
        {
            int rowIdx = _table.Rows.IndexOf(_inserted);
            if (rowIdx >= 0 && _table.RowCount > 1)
                TableEngine.DeleteRow(_table, rowIdx);
        }
        int blockIdx = _document.Blocks.IndexOf(_table);
        int targetRow = Math.Clamp(_originalCellRow, 0, _table.RowCount - 1);
        return blockIdx >= 0
            ? new DocumentPosition(blockIdx, 0, 0, targetRow, 0)
            : new DocumentPosition(0, 0, 0);
    }

    /// <inheritdoc/>
    public DocumentPosition Redo() => Execute();
    /// <inheritdoc/>
    public bool MergeWith(IUndoableAction next) => false;
}

// ── DeleteRowAction ──────────────────────────────────────────────────────────

/// <summary>Records a table row deletion so it can be undone by reinserting the removed row.</summary>
public sealed class DeleteRowAction : IUndoableAction
{
    private readonly Document.Document _document;
    private readonly TableBlock _table;
    private readonly int _rowIndex;
    private TableRow? _removed;

    /// <summary>Creates an action that deletes the row at <paramref name="rowIndex"/> from <paramref name="table"/>.</summary>
    public DeleteRowAction(Document.Document document, TableBlock table, int rowIndex)
    {
        _document = document;
        _table = table;
        _rowIndex = rowIndex;
    }

    /// <inheritdoc/>
    public string Description => "Delete Row";

    /// <inheritdoc/>
    public DocumentPosition Execute()
    {
        _removed = _table.Rows[_rowIndex];
        TableEngine.DeleteRow(_table, _rowIndex);
        int blockIdx = _document.Blocks.IndexOf(_table);
        int targetRow = Math.Clamp(_rowIndex, 0, _table.RowCount - 1);
        return blockIdx >= 0
            ? new DocumentPosition(blockIdx, 0, 0, targetRow, 0)
            : new DocumentPosition(0, 0, 0);
    }

    /// <inheritdoc/>
    public DocumentPosition Undo()
    {
        if (_removed is not null)
        {
            _removed.Parent = _table;
            _table.Rows.Insert(_rowIndex, _removed);
            int blockIdx = _document.Blocks.IndexOf(_table);
            if (blockIdx >= 0)
            {
                var p = new DocumentPosition(blockIdx, 0, 0);
                _document.NotifyChanged(new DocumentChangedEventArgs(
                    DocumentChangeType.StyleChanged,
                    new TextRange(p, p)));
            }
        }
        int bi = _document.Blocks.IndexOf(_table);
        return bi >= 0
            ? new DocumentPosition(bi, 0, 0, _rowIndex, 0)
            : new DocumentPosition(0, 0, 0);
    }

    /// <inheritdoc/>
    public DocumentPosition Redo() => Execute();
    /// <inheritdoc/>
    public bool MergeWith(IUndoableAction next) => false;
}

// ── InsertColumnAction ───────────────────────────────────────────────────────

/// <summary>Records a table column insertion so it can be undone by deleting the inserted column.</summary>
public sealed class InsertColumnAction : IUndoableAction
{
    private readonly Document.Document _document;
    private readonly TableBlock _table;
    private readonly int _afterColIndex;
    private readonly int _originalCellCol;

    /// <summary>Creates an action that inserts a new column into <paramref name="table"/>.</summary>
    /// <param name="document">The document that contains the table.</param>
    /// <param name="table">The table to modify.</param>
    /// <param name="afterColIndex">Index of the column after which the new column is inserted.</param>
    /// <param name="originalCellCol">Column of the caret before the insert; the caret returns to this column on undo.</param>
    public InsertColumnAction(Document.Document document, TableBlock table, int afterColIndex, int originalCellCol)
    {
        _document = document;
        _table = table;
        _afterColIndex = afterColIndex;
        _originalCellCol = originalCellCol;
    }

    /// <inheritdoc/>
    public string Description => "Insert Column";

    /// <inheritdoc/>
    public DocumentPosition Execute()
    {
        TableEngine.InsertColumn(_table, _afterColIndex);
        int blockIdx = _document.Blocks.IndexOf(_table);
        int newColIdx = Math.Clamp(_afterColIndex + 1, 0, _table.ColumnCount - 1);
        return blockIdx >= 0
            ? new DocumentPosition(blockIdx, 0, 0, 0, newColIdx)
            : new DocumentPosition(0, 0, 0);
    }

    /// <inheritdoc/>
    public DocumentPosition Undo()
    {
        int colToRemove = Math.Clamp(_afterColIndex + 1, 0, _table.ColumnCount - 1);
        if (_table.ColumnCount > 1)
            TableEngine.DeleteColumn(_table, colToRemove);
        int blockIdx = _document.Blocks.IndexOf(_table);
        int targetCol = Math.Clamp(_originalCellCol, 0, _table.ColumnCount - 1);
        return blockIdx >= 0
            ? new DocumentPosition(blockIdx, 0, 0, 0, targetCol)
            : new DocumentPosition(0, 0, 0);
    }

    /// <inheritdoc/>
    public DocumentPosition Redo() => Execute();
    /// <inheritdoc/>
    public bool MergeWith(IUndoableAction next) => false;
}

// ── DeleteColumnAction ───────────────────────────────────────────────────────

/// <summary>Records a table column deletion so it can be undone by reinserting the removed cells.</summary>
public sealed class DeleteColumnAction : IUndoableAction
{
    private readonly Document.Document _document;
    private readonly TableBlock _table;
    private readonly int _colIndex;
    private List<TableCell>? _removedCells;

    /// <summary>Creates an action that deletes the column at <paramref name="colIndex"/> from <paramref name="table"/>.</summary>
    public DeleteColumnAction(Document.Document document, TableBlock table, int colIndex)
    {
        _document = document;
        _table = table;
        _colIndex = colIndex;
    }

    /// <inheritdoc/>
    public string Description => "Delete Column";

    /// <inheritdoc/>
    public DocumentPosition Execute()
    {
        _removedCells = _table.Rows.Select(r => r.Cells[_colIndex]).ToList();
        TableEngine.DeleteColumn(_table, _colIndex);
        int blockIdx = _document.Blocks.IndexOf(_table);
        int targetCol = Math.Clamp(_colIndex, 0, _table.ColumnCount - 1);
        return blockIdx >= 0
            ? new DocumentPosition(blockIdx, 0, 0, 0, targetCol)
            : new DocumentPosition(0, 0, 0);
    }

    /// <inheritdoc/>
    public DocumentPosition Undo()
    {
        if (_removedCells is not null)
        {
            for (int r = 0; r < _table.Rows.Count && r < _removedCells.Count; r++)
            {
                var cell = _removedCells[r];
                cell.Parent = _table.Rows[r];
                _table.Rows[r].Cells.Insert(_colIndex, cell);
            }
            int blockIdx = _document.Blocks.IndexOf(_table);
            if (blockIdx >= 0)
            {
                var p = new DocumentPosition(blockIdx, 0, 0);
                _document.NotifyChanged(new DocumentChangedEventArgs(
                    DocumentChangeType.StyleChanged,
                    new TextRange(p, p)));
            }
        }
        int bi = _document.Blocks.IndexOf(_table);
        return bi >= 0
            ? new DocumentPosition(bi, 0, 0, 0, _colIndex)
            : new DocumentPosition(0, 0, 0);
    }

    /// <inheritdoc/>
    public DocumentPosition Redo() => Execute();
    /// <inheritdoc/>
    public bool MergeWith(IUndoableAction next) => false;
}

// ── DeleteTableAction ────────────────────────────────────────────────────────

/// <summary>Records the removal of a table so it can be undone by reinserting it at its original block index.</summary>
public sealed class DeleteTableAction : IUndoableAction
{
    private readonly Document.Document _document;
    private readonly TableBlock _table;
    private int _blockIndex = -1;

    /// <summary>Creates an action that removes <paramref name="table"/> from the document.</summary>
    public DeleteTableAction(Document.Document document, TableBlock table)
    {
        _document = document;
        _table = table;
    }

    /// <inheritdoc/>
    public string Description => "Delete Table";

    /// <inheritdoc/>
    public DocumentPosition Execute()
    {
        _blockIndex = _document.Blocks.IndexOf(_table);
        if (_blockIndex >= 0)
        {
            _table.Parent = null;
            _document.Blocks.RemoveAt(_blockIndex);
            var removedPos = new DocumentPosition(_blockIndex, 0, 0);
            _document.NotifyChanged(new DocumentChangedEventArgs(
                DocumentChangeType.TextDeleted,
                new TextRange(removedPos, removedPos)));
        }
        int targetBlock = Math.Clamp(_blockIndex, 0, _document.Blocks.Count - 1);
        return new DocumentPosition(targetBlock, 0, 0);
    }

    /// <inheritdoc/>
    public DocumentPosition Undo()
    {
        if (_blockIndex >= 0)
        {
            _table.Parent = _document;
            _document.Blocks.Insert(_blockIndex, _table);
            var reinsertedPos = new DocumentPosition(_blockIndex, 0, 0);
            _document.NotifyChanged(new DocumentChangedEventArgs(
                DocumentChangeType.TextInserted,
                new TextRange(reinsertedPos, reinsertedPos)));
        }
        return _blockIndex >= 0
            ? new DocumentPosition(_blockIndex, 0, 0, 0, 0)
            : new DocumentPosition(0, 0, 0);
    }

    /// <inheritdoc/>
    public DocumentPosition Redo() => Execute();
    /// <inheritdoc/>
    public bool MergeWith(IUndoableAction next) => false;
}
