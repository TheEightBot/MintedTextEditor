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

    public TableBlock() { }

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

    public override int Length => Rows.Sum(r => r.Cells.Sum(c => c.Blocks.Sum(b => b.Length)));

    public override string GetText() =>
        string.Join("\n", Rows.Select(r => string.Join("\t", r.Cells.Select(c => c.GetText()))));
}
