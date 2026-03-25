using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Formatting;

/// <summary>
/// Operations for inserting, editing, and removing tables within a document.
/// All mutating methods fire <see cref="Document.Document.NotifyChanged"/>.
/// </summary>
public static class TableEngine
{
    // ── Insert ────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a new <see cref="TableBlock"/> with <paramref name="rows"/> rows and
    /// <paramref name="cols"/> columns at the given document block index.
    /// </summary>
    public static TableBlock InsertTable(
        Document.Document doc,
        DocumentPosition position,
        int rows,
        int cols)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(cols, 1);

        var table = new TableBlock(rows, cols) { Parent = doc };
        int insertAt = Math.Clamp(position.BlockIndex + 1, 0, doc.Blocks.Count);
        doc.Blocks.Insert(insertAt, table);

        doc.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.TextInserted,
            new TextRange(position, position)));

        return table;
    }

    // ── Row operations ────────────────────────────────────────────────────

    /// <summary>Inserts a new empty row after <paramref name="afterRowIndex"/>.</summary>
    public static TableRow InsertRow(TableBlock table, int afterRowIndex)
    {
        int cols = table.ColumnCount;
        var row = new TableRow(cols) { Parent = table };
        int insertAt = Math.Clamp(afterRowIndex + 1, 0, table.Rows.Count);
        table.Rows.Insert(insertAt, row);

        NotifyTableChanged(table);
        return row;
    }

    /// <summary>Deletes the row at <paramref name="rowIndex"/>. Must leave at least one row.</summary>
    public static void DeleteRow(TableBlock table, int rowIndex)
    {
        if (table.RowCount <= 1)
            throw new InvalidOperationException("A table must have at least one row.");
        if (rowIndex < 0 || rowIndex >= table.RowCount)
            throw new ArgumentOutOfRangeException(nameof(rowIndex));

        var row = table.Rows[rowIndex];
        row.Parent = null;
        table.Rows.RemoveAt(rowIndex);

        NotifyTableChanged(table);
    }

    // ── Column operations ─────────────────────────────────────────────────

    /// <summary>Inserts a new empty column after <paramref name="afterColIndex"/> in every row.</summary>
    public static void InsertColumn(TableBlock table, int afterColIndex)
    {
        int insertAt = Math.Clamp(afterColIndex + 1, 0, table.ColumnCount);

        foreach (var row in table.Rows)
        {
            var cell = new TableCell { Parent = row };
            row.Cells.Insert(insertAt, cell);
        }

        if (table.ColumnWidths.Count > 0)
        {
            float newWidth = 120f;
            if (insertAt > 0 && insertAt - 1 < table.ColumnWidths.Count)
                newWidth = Math.Max(24f, table.ColumnWidths[insertAt - 1]);
            table.ColumnWidths.Insert(Math.Min(insertAt, table.ColumnWidths.Count), newWidth);
        }

        NotifyTableChanged(table);
    }

    /// <summary>Deletes the column at <paramref name="colIndex"/> from every row. Must leave at least one column.</summary>
    public static void DeleteColumn(TableBlock table, int colIndex)
    {
        if (table.ColumnCount <= 1)
            throw new InvalidOperationException("A table must have at least one column.");

        foreach (var row in table.Rows)
        {
            if (colIndex < 0 || colIndex >= row.Cells.Count)
                throw new ArgumentOutOfRangeException(nameof(colIndex));
            row.Cells[colIndex].Parent = null;
            row.Cells.RemoveAt(colIndex);
        }

        if (colIndex >= 0 && colIndex < table.ColumnWidths.Count)
            table.ColumnWidths.RemoveAt(colIndex);

        NotifyTableChanged(table);
    }

    // ── Cell merge / split ────────────────────────────────────────────────

    /// <summary>
    /// Merges <paramref name="colSpan"/> x <paramref name="rowSpan"/> cells starting at
    /// (<paramref name="startRow"/>, <paramref name="startCol"/>) into a single logical cell.
    /// The top-left cell absorbs the span; remaining cells are marked as merged.
    /// </summary>
    public static void MergeCells(
        TableBlock table,
        int startRow,
        int startCol,
        int rowSpan,
        int colSpan)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rowSpan, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(colSpan, 1);

        var anchor = table.GetCell(startRow, startCol)
            ?? throw new ArgumentOutOfRangeException(nameof(startRow));

        anchor.RowSpan = rowSpan;
        anchor.ColumnSpan = colSpan;

        for (int r = startRow; r < startRow + rowSpan; r++)
        {
            for (int c = startCol; c < startCol + colSpan; c++)
            {
                if (r == startRow && c == startCol) continue;
                var cell = table.GetCell(r, c);
                if (cell is not null)
                    cell.IsMerged = true;
            }
        }

        NotifyTableChanged(table);
    }

    /// <summary>
    /// Splits a previously merged cell back to individual cells.
    /// New cells are created for each position that was absorbed.
    /// </summary>
    public static void SplitCell(TableBlock table, TableCell cell)
    {
        if (cell.RowSpan == 1 && cell.ColumnSpan == 1)
            return; // nothing to split

        // Find position of the anchor cell
        int anchorRow = -1, anchorCol = -1;
        for (int r = 0; r < table.RowCount; r++)
        {
            for (int c = 0; c < table.Rows[r].Cells.Count; c++)
            {
                if (table.Rows[r].Cells[c] == cell)
                {
                    anchorRow = r;
                    anchorCol = c;
                    break;
                }
            }
            if (anchorRow >= 0) break;
        }

        if (anchorRow < 0) return;

        int rowSpan = cell.RowSpan;
        int colSpan = cell.ColumnSpan;

        cell.RowSpan = 1;
        cell.ColumnSpan = 1;

        for (int r = anchorRow; r < anchorRow + rowSpan; r++)
        {
            for (int c = anchorCol; c < anchorCol + colSpan; c++)
            {
                if (r == anchorRow && c == anchorCol) continue;
                var absorbed = table.GetCell(r, c);
                if (absorbed is not null)
                {
                    absorbed.IsMerged = false;
                    absorbed.RowSpan = 1;
                    absorbed.ColumnSpan = 1;
                }
            }
        }

        NotifyTableChanged(table);
    }

    // ── Cell formatting ───────────────────────────────────────────────────

    /// <summary>Sets the background colour of a single cell.</summary>
    public static void SetCellBackground(TableBlock table, TableCell cell, EditorColor color)
    {
        cell.Background = color;
        NotifyTableChanged(table);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void NotifyTableChanged(TableBlock table)
    {
        if (table.Parent is Document.Document doc)
        {
            var pos = new DocumentPosition(doc.Blocks.IndexOf(table), 0, 0);
            doc.NotifyChanged(new DocumentChangedEventArgs(
                DocumentChangeType.StyleChanged,
                new TextRange(pos, pos)));
        }
    }
}
