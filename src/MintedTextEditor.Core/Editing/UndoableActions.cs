using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Formatting;

namespace MintedTextEditor.Core.Editing;

// ── Shared snapshot helpers ─────────────────────────────────────────────────

internal abstract record InlineSnapshot;
internal record TextRunSnapshot(string Text, TextStyle Style) : InlineSnapshot;
internal record ImageInlineSnapshot(string Source, string AltText, float Width, float Height) : InlineSnapshot;
internal record HyperChildSnapshot(string Text, TextStyle Style);
internal record HyperlinkInlineSnapshot(string Url, string? Title, IReadOnlyList<HyperChildSnapshot> Children) : InlineSnapshot;
internal record ParagraphSnapshot(ParagraphStyle Style, IReadOnlyList<InlineSnapshot> Inlines);

internal static class SnapshotHelper
{
    internal static ParagraphSnapshot Capture(Paragraph p) =>
        new(p.Style.Clone(), p.Inlines.Select(CaptureInline).ToList());

    private static InlineSnapshot CaptureInline(Inline inline) => inline switch
    {
        TextRun t => new TextRunSnapshot(t.Text, t.Style),
        ImageInline i => new ImageInlineSnapshot(i.Source, i.AltText, i.Width, i.Height),
        HyperlinkInline h => new HyperlinkInlineSnapshot(h.Url, h.Title,
            h.Children.OfType<TextRun>().Select(c => new HyperChildSnapshot(c.Text, c.Style)).ToList()),
        _ => new TextRunSnapshot(inline.GetText(), TextStyle.Default)
    };

    /// <summary>
    /// Replaces the inlines (and style) of <paramref name="p"/> with the data from
    /// <paramref name="snapshot"/>, without changing <paramref name="p"/>'s identity or parent.
    /// </summary>
    internal static void Restore(Paragraph p, ParagraphSnapshot snapshot)
    {
        p.Style = snapshot.Style;
        // Remove existing inlines from back to front to keep indices stable
        for (int i = p.Inlines.Count - 1; i >= 0; i--)
            p.RemoveInline(i);
        foreach (var snap in snapshot.Inlines)
            AppendInline(p, snap);
    }

    /// <summary>Creates a new <see cref="Paragraph"/> fully populated from a snapshot.</summary>
    internal static Paragraph CreateParagraph(ParagraphSnapshot snapshot)
    {
        var p = new Paragraph { Style = snapshot.Style };
        foreach (var snap in snapshot.Inlines)
            AppendInline(p, snap);
        return p;
    }

    private static void AppendInline(Paragraph p, InlineSnapshot snap)
    {
        switch (snap)
        {
            case TextRunSnapshot t:
                p.AppendRun(t.Text, t.Style);
                break;
            case ImageInlineSnapshot i:
                p.AddInline(new ImageInline(i.Source, i.AltText, i.Width, i.Height));
                break;
            case HyperlinkInlineSnapshot h:
                var link = new HyperlinkInline(h.Url, h.Title);
                p.AddInline(link);  // set link.Parent = p before adding children
                foreach (var c in h.Children)
                    link.AddChild(new TextRun(c.Text, c.Style));
                break;
        }
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

    public InsertTextAction(Document.Document document, DocumentPosition position, string text, TextStyle? style = null)
    {
        _document = document;
        _position = position;
        _text = text;
        _style = style;
    }

    public string Description => $"Insert \"{_text}\"";

    public DocumentPosition Execute()
    {
        _resultPosition = DocumentEditor.InsertText(_document, _position, _text, _style);
        return _resultPosition;
    }

    public DocumentPosition Undo()
    {
        DocumentEditor.DeleteRange(_document, new TextRange(_position, _resultPosition));
        return _position;
    }

    public DocumentPosition Redo() => Execute();

    /// <summary>
    /// Merges consecutive single-character (or small word) insertions at the same position
    /// so that a run of typed characters forms one undo step.
    /// </summary>
    public bool MergeWith(IUndoableAction next)
    {
        if (next is InsertTextAction insert &&
            insert._position.Equals(_resultPosition) &&
            insert._style == _style)
        {
            _text += insert._text;
            _resultPosition = insert._resultPosition;
            return true;
        }
        return false;
    }
}

// ── DeleteRangeAction ───────────────────────────────────────────────────────

/// <summary>
/// Records a range deletion. Snapshots all affected paragraphs before the delete
/// so they can be fully restored on undo.
/// </summary>
public sealed class DeleteRangeAction : IUndoableAction
{
    private readonly Document.Document _document;
    private readonly TextRange _range;
    private List<ParagraphSnapshot> _snapshots = [];

    public DeleteRangeAction(Document.Document document, TextRange range)
    {
        _document = document;
        _range = range;
    }

    public string Description => "Delete";

    public DocumentPosition Execute()
    {
        _snapshots.Clear();
        for (int i = _range.Start.BlockIndex; i <= _range.End.BlockIndex && i < _document.Blocks.Count; i++)
        {
            if (_document.Blocks[i] is Paragraph p)
                _snapshots.Add(SnapshotHelper.Capture(p));
        }
        return DocumentEditor.DeleteRange(_document, _range);
    }

    public DocumentPosition Undo()
    {
        if (_snapshots.Count == 0) return _range.Start;

        // Restore the first (possibly only) paragraph in-place
        if (_range.Start.BlockIndex < _document.Blocks.Count &&
            _document.Blocks[_range.Start.BlockIndex] is Paragraph first)
        {
            SnapshotHelper.Restore(first, _snapshots[0]);
        }

        // Re-insert additional paragraphs for multi-block ranges
        for (int i = 1; i < _snapshots.Count; i++)
        {
            var p = SnapshotHelper.CreateParagraph(_snapshots[i]);
            _document.InsertBlock(_range.Start.BlockIndex + i, p);
        }

        _document.NotifyChanged(new DocumentChangedEventArgs(DocumentChangeType.TextInserted, _range));
        return _range.Start;
    }

    public DocumentPosition Redo() => Execute();

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
    private DocumentPosition _resultPosition;

    public SplitBlockAction(Document.Document document, DocumentPosition position)
    {
        _document = document;
        _position = position;
    }

    public string Description => "Split Paragraph";

    public DocumentPosition Execute()
    {
        _resultPosition = DocumentEditor.SplitBlock(_document, _position);
        return _resultPosition;
    }

    public DocumentPosition Undo()
    {
        DocumentEditor.MergeBlocks(_document, _position.BlockIndex);
        return _position;
    }

    public DocumentPosition Redo() => Execute();

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

    public MergeBlocksAction(Document.Document document, int blockIndex)
    {
        _document = document;
        _blockIndex = blockIndex;
    }

    public string Description => "Merge Paragraphs";

    public DocumentPosition Execute()
    {
        if (_blockIndex < _document.Blocks.Count && _document.Blocks[_blockIndex] is Paragraph p1)
            _firstSnapshot = SnapshotHelper.Capture(p1);
        if (_blockIndex + 1 < _document.Blocks.Count && _document.Blocks[_blockIndex + 1] is Paragraph p2)
            _secondSnapshot = SnapshotHelper.Capture(p2);

        _resultPosition = DocumentEditor.MergeBlocks(_document, _blockIndex);
        return _resultPosition;
    }

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

    public DocumentPosition Redo() => Execute();

    public bool MergeWith(IUndoableAction next) => false;
}

// ── ApplyStyleAction ────────────────────────────────────────────────────────

/// <summary>
/// Records a style change. Snapshots all affected paragraph inlines before applying
/// so the originals can be restored on undo.
/// </summary>
public sealed class ApplyStyleAction : IUndoableAction
{
    private readonly Document.Document _document;
    private readonly TextRange _range;
    private readonly Func<TextStyle, TextStyle> _transform;
    private List<ParagraphSnapshot> _snapshots = [];

    public ApplyStyleAction(Document.Document document, TextRange range, Func<TextStyle, TextStyle> transform)
    {
        _document = document;
        _range = range;
        _transform = transform;
    }

    public string Description => "Apply Style";

    public DocumentPosition Execute()
    {
        _snapshots.Clear();
        for (int i = _range.Start.BlockIndex; i <= _range.End.BlockIndex && i < _document.Blocks.Count; i++)
        {
            if (_document.Blocks[i] is Paragraph p)
                _snapshots.Add(SnapshotHelper.Capture(p));
        }
        DocumentEditor.ApplyTextStyle(_document, _range, _transform);
        return _range.Start;
    }

    public DocumentPosition Undo()
    {
        for (int i = 0; i < _snapshots.Count; i++)
        {
            int blockIdx = _range.Start.BlockIndex + i;
            if (blockIdx < _document.Blocks.Count && _document.Blocks[blockIdx] is Paragraph p)
                SnapshotHelper.Restore(p, _snapshots[i]);
        }
        _document.NotifyChanged(new DocumentChangedEventArgs(DocumentChangeType.StyleChanged, _range));
        return _range.Start;
    }

    public DocumentPosition Redo() => Execute();

    public bool MergeWith(IUndoableAction next) => false;
}

// ── CompositeAction ─────────────────────────────────────────────────────────

/// <summary>
/// Wraps multiple undoable actions into a single undo step (e.g. replace = delete + insert).
/// </summary>
public sealed class CompositeAction : IUndoableAction
{
    private readonly IReadOnlyList<IUndoableAction> _actions;
    private DocumentPosition _resultPosition;

    public CompositeAction(IEnumerable<IUndoableAction> actions)
    {
        _actions = [.. actions];
    }

    public string Description => string.Join(" + ", _actions.Select(a => a.Description));

    public DocumentPosition Execute()
    {
        foreach (var a in _actions)
            _resultPosition = a.Execute();
        return _resultPosition;
    }

    public DocumentPosition Undo()
    {
        DocumentPosition pos = _resultPosition;
        for (int i = _actions.Count - 1; i >= 0; i--)
            pos = _actions[i].Undo();
        return pos;
    }

    public DocumentPosition Redo()
    {
        foreach (var a in _actions)
            _resultPosition = a.Redo();
        return _resultPosition;
    }

    public bool MergeWith(IUndoableAction next) => false;
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

    public string Description => "Insert Image";

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

    public DocumentPosition Undo()
    {
        if (_inserted is not null)
            ImageEngine.RemoveImage(_document, _inserted);
        return _insertPosition;
    }

    public DocumentPosition Redo() => Execute();

    public bool MergeWith(IUndoableAction next) => false;
}

// ── InsertHyperlinkAction ───────────────────────────────────────────────────

/// <summary>
/// Records a hyperlink insertion so it can be undone by removing the inserted hyperlink.
/// </summary>
public sealed class InsertHyperlinkAction : IUndoableAction
{
    private readonly Document.Document _document;
    private readonly TextRange _originalRange;
    private readonly string _url;
    private HyperlinkInline? _inserted;
    private DocumentPosition _resultPosition;

    public InsertHyperlinkAction(Document.Document document, TextRange originalRange, string url)
    {
        _document = document;
        _originalRange = originalRange;
        _url = url;
    }

    public string Description => "Insert Hyperlink";

    public DocumentPosition Execute()
    {
        _inserted = HyperlinkEngine.InsertHyperlink(_document, _originalRange, _url);
        if (_inserted.Parent is Paragraph para)
        {
            int blockIdx = _document.Blocks.IndexOf(para);
            int linkIdx = para.Inlines.IndexOf(_inserted);
            _resultPosition = new DocumentPosition(blockIdx, linkIdx, _inserted.Length);
        }
        else
        {
            _resultPosition = _originalRange.Start;
        }
        return _resultPosition;
    }

    public DocumentPosition Undo()
    {
        if (_inserted is null || _inserted.Parent is not Paragraph para)
            return _originalRange.Start;

        int blockIdx = _document.Blocks.IndexOf(para);
        int linkIdx = para.Inlines.IndexOf(_inserted);
        if (blockIdx < 0 || linkIdx < 0) return _originalRange.Start;

        // Remove hyperlink and restore plain text if the range was non-empty
        para.RemoveInline(linkIdx);
        if (!_originalRange.IsEmpty)
            para.InsertInline(linkIdx, new TextRun(_inserted.GetText()) { Parent = para });

        _document.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.TextDeleted,
            new TextRange(_originalRange.Start, _originalRange.Start)));

        return _originalRange.Start;
    }

    public DocumentPosition Redo() => Execute();

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

    public InsertTableAction(Document.Document document, DocumentPosition insertPosition, int rows, int cols)
    {
        _document = document;
        _insertPosition = insertPosition;
        _rows = rows;
        _cols = cols;
    }

    public string Description => "Insert Table";

    public DocumentPosition Execute()
    {
        _inserted = TableEngine.InsertTable(_document, _insertPosition, _rows, _cols);
        int blockIdx = _document.Blocks.IndexOf(_inserted);
        _resultPosition = blockIdx >= 0
            ? new DocumentPosition(blockIdx, 0, 0)
            : _insertPosition;
        return _resultPosition;
    }

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

    public DocumentPosition Redo() => Execute();

    public bool MergeWith(IUndoableAction next) => false;
}

// ── InsertRowAction ──────────────────────────────────────────────────────────

public sealed class InsertRowAction : IUndoableAction
{
    private readonly Document.Document _document;
    private readonly TableBlock _table;
    private readonly int _afterRowIndex;
    private readonly int _originalCellRow;
    private TableRow? _inserted;

    public InsertRowAction(Document.Document document, TableBlock table, int afterRowIndex, int originalCellRow)
    {
        _document = document;
        _table = table;
        _afterRowIndex = afterRowIndex;
        _originalCellRow = originalCellRow;
    }

    public string Description => "Insert Row";

    public DocumentPosition Execute()
    {
        _inserted = TableEngine.InsertRow(_table, _afterRowIndex);
        int blockIdx = _document.Blocks.IndexOf(_table);
        int newRowIdx = _table.Rows.IndexOf(_inserted);
        return blockIdx >= 0
            ? new DocumentPosition(blockIdx, 0, 0, newRowIdx, 0)
            : new DocumentPosition(0, 0, 0);
    }

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

    public DocumentPosition Redo() => Execute();
    public bool MergeWith(IUndoableAction next) => false;
}

// ── DeleteRowAction ──────────────────────────────────────────────────────────

public sealed class DeleteRowAction : IUndoableAction
{
    private readonly Document.Document _document;
    private readonly TableBlock _table;
    private readonly int _rowIndex;
    private TableRow? _removed;

    public DeleteRowAction(Document.Document document, TableBlock table, int rowIndex)
    {
        _document = document;
        _table = table;
        _rowIndex = rowIndex;
    }

    public string Description => "Delete Row";

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

    public DocumentPosition Redo() => Execute();
    public bool MergeWith(IUndoableAction next) => false;
}

// ── InsertColumnAction ───────────────────────────────────────────────────────

public sealed class InsertColumnAction : IUndoableAction
{
    private readonly Document.Document _document;
    private readonly TableBlock _table;
    private readonly int _afterColIndex;
    private readonly int _originalCellCol;

    public InsertColumnAction(Document.Document document, TableBlock table, int afterColIndex, int originalCellCol)
    {
        _document = document;
        _table = table;
        _afterColIndex = afterColIndex;
        _originalCellCol = originalCellCol;
    }

    public string Description => "Insert Column";

    public DocumentPosition Execute()
    {
        TableEngine.InsertColumn(_table, _afterColIndex);
        int blockIdx = _document.Blocks.IndexOf(_table);
        int newColIdx = Math.Clamp(_afterColIndex + 1, 0, _table.ColumnCount - 1);
        return blockIdx >= 0
            ? new DocumentPosition(blockIdx, 0, 0, 0, newColIdx)
            : new DocumentPosition(0, 0, 0);
    }

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

    public DocumentPosition Redo() => Execute();
    public bool MergeWith(IUndoableAction next) => false;
}

// ── DeleteColumnAction ───────────────────────────────────────────────────────

public sealed class DeleteColumnAction : IUndoableAction
{
    private readonly Document.Document _document;
    private readonly TableBlock _table;
    private readonly int _colIndex;
    private List<TableCell>? _removedCells;

    public DeleteColumnAction(Document.Document document, TableBlock table, int colIndex)
    {
        _document = document;
        _table = table;
        _colIndex = colIndex;
    }

    public string Description => "Delete Column";

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

    public DocumentPosition Redo() => Execute();
    public bool MergeWith(IUndoableAction next) => false;
}

// ── DeleteTableAction ────────────────────────────────────────────────────────

public sealed class DeleteTableAction : IUndoableAction
{
    private readonly Document.Document _document;
    private readonly TableBlock _table;
    private int _blockIndex = -1;

    public DeleteTableAction(Document.Document document, TableBlock table)
    {
        _document = document;
        _table = table;
    }

    public string Description => "Delete Table";

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

    public DocumentPosition Redo() => Execute();
    public bool MergeWith(IUndoableAction next) => false;
}
