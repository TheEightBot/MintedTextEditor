namespace MintedTextEditor.Core.Document;

/// <summary>
/// A block-level element that contains a grid of <see cref="TableRow"/> / <see cref="TableCell"/> elements.
/// </summary>
public class TableBlock : Block
{
    /// <summary>The rows of the table in order.</summary>
    public List<TableRow> Rows { get; } = new();

    /// <summary>Table-wide style defaults.</summary>
    public TableStyle Style { get; set; } = new();

    /// <summary>
    /// Optional explicit per-column widths in logical pixels.
    /// When empty, the layout engine uses equal-width auto layout.
    /// </summary>
    public List<float> ColumnWidths { get; } = new();

    /// <summary>Number of rows in the table.</summary>
    public int RowCount => Rows.Count;

    /// <summary>Number of columns in the table (determined from the first row).</summary>
    public int ColumnCount => Rows.Count > 0 ? Rows[0].Cells.Count : 0;

    /// <summary>Creates an empty table with no rows.</summary>
    public TableBlock() { }

    /// <summary>Creates a table with the given number of rows and columns; each cell holds an empty paragraph.</summary>
    public TableBlock(int rows, int cols)
    {
        for (int c = 0; c < cols; c++)
            ColumnWidths.Add(0f);

        for (int r = 0; r < rows; r++)
        {
            var row = new TableRow(cols) { Parent = this };
            Rows.Add(row);
        }
    }

    /// <summary>Gets the cell at the given row and column index, or null if out of range.</summary>
    public TableCell? GetCell(int rowIndex, int colIndex)
    {
        if (rowIndex < 0 || rowIndex >= Rows.Count) return null;
        var row = Rows[rowIndex];
        if (colIndex < 0 || colIndex >= row.Cells.Count) return null;
        return row.Cells[colIndex];
    }

    /// <summary>
    /// Gets the grid position of the cell that occupies the slot at (<paramref name="rowIndex"/>,
    /// <paramref name="colIndex"/>). For a slot covered by another cell's <see cref="TableCell.RowSpan"/> /
    /// <see cref="TableCell.ColumnSpan"/>, that is the spanning cell; otherwise it is the slot itself.
    /// Out-of-range positions are returned unchanged.
    /// </summary>
    public (int Row, int Column) GetSpanAnchor(int rowIndex, int colIndex)
    {
        if (rowIndex < 0 || rowIndex >= RowCount || colIndex < 0 || colIndex >= ColumnCount)
            return (rowIndex, colIndex);

        var owners = BuildSpanOwners();
        return owners[rowIndex][colIndex];
    }

    /// <summary>
    /// Maps every slot of the <see cref="RowCount"/> x <see cref="ColumnCount"/> grid to the cell that
    /// occupies it. Cells are visited in reading order; a cell that is not covered claims the free slots
    /// of its span, clamped to the grid. Missing cells (short rows) occupy their own slot.
    /// </summary>
    internal (int Row, int Column)[][] BuildSpanOwners()
    {
        int rowCount = RowCount;
        int colCount = ColumnCount;
        var owners = new (int Row, int Column)[rowCount][];
        for (int r = 0; r < rowCount; r++)
        {
            owners[r] = new (int, int)[colCount];
            Array.Fill(owners[r], (-1, -1));
        }

        for (int r = 0; r < rowCount; r++)
        {
            for (int c = 0; c < colCount; c++)
            {
                if (owners[r][c].Row >= 0) continue; // covered by an earlier span

                var cell = GetCell(r, c);
                var (rowSpan, colSpan) = ClampSpan(owners, r, c, cell);
                for (int sr = r; sr < r + rowSpan; sr++)
                    for (int sc = c; sc < c + colSpan; sc++)
                        owners[sr][sc] = (r, c);
            }
        }

        return owners;
    }

    /// <summary>
    /// Clamps a cell's span to the grid and shrinks it so the spanned rectangle contains no slot that
    /// an earlier cell already occupies (overlapping spans keep the earlier cell).
    /// </summary>
    internal static (int RowSpan, int ColumnSpan) ClampSpan(
        (int Row, int Column)[][] owners, int row, int col, TableCell? cell)
    {
        int rowCount = owners.Length;
        int colCount = rowCount > 0 ? owners[0].Length : 0;
        int maxRowSpan = Math.Clamp(cell?.RowSpan ?? 1, 1, rowCount - row);
        int maxColSpan = Math.Clamp(cell?.ColumnSpan ?? 1, 1, colCount - col);

        int colSpan = 1;
        while (colSpan < maxColSpan && owners[row][col + colSpan].Row < 0)
            colSpan++;

        int rowSpan = 1;
        while (rowSpan < maxRowSpan)
        {
            bool free = true;
            for (int sc = col; sc < col + colSpan && free; sc++)
                free = owners[row + rowSpan][sc].Row < 0;
            if (!free) break;
            rowSpan++;
        }

        return (rowSpan, colSpan);
    }

    /// <summary>Sum of the lengths of all blocks in all cells.</summary>
    public override int Length => Rows.Sum(r => r.Cells.Sum(c => c.Blocks.Sum(b => b.Length)));

    /// <summary>Gets the table text with cells separated by tabs and rows separated by newlines.</summary>
    public override string GetText() =>
        string.Join("\n", Rows.Select(r => string.Join("\t", r.Cells.Select(c => c.GetText()))));
}
