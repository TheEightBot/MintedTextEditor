namespace MintedTextEditor.Core.Document;

/// <summary>
/// A single row within a <see cref="TableBlock"/>, containing an ordered list of <see cref="TableCell"/> elements.
/// </summary>
public class TableRow
{
    /// <summary>The cells in column order.</summary>
    public List<TableCell> Cells { get; } = new();

    /// <summary>Fixed row height in pixels, or 0 for auto.</summary>
    public float Height { get; set; }

    /// <summary>The parent table that owns this row.</summary>
    public TableBlock? Parent { get; internal set; }

    /// <summary>Creates an empty row with no cells.</summary>
    public TableRow() { }

    /// <summary>Creates a row with the given number of cells, each holding an empty paragraph.</summary>
    public TableRow(int columnCount)
    {
        for (int i = 0; i < columnCount; i++)
        {
            var cell = new TableCell { Parent = this };
            Cells.Add(cell);
        }
    }
}
