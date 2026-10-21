using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Layout;

/// <summary>
/// Layout result for a <see cref="Document.TableBlock"/>.
/// Stores per-cell nested layouts used for rendering and hit-testing the table grid.
/// </summary>
/// <remarks>
/// The grid is laid out as: a border line of <see cref="BorderWidth"/>, then each column (or row)
/// followed by another border line. A cell with a column or row span occupies the combined area of
/// the slots it covers, including the border lines between them.
/// </remarks>
public sealed class TableLayoutBlock : LayoutBlock
{
    /// <summary>Width of each column in logical pixels.</summary>
    public float[] ColumnWidths { get; set; } = [];

    /// <summary>Height of each row in logical pixels (includes cell padding).</summary>
    public float[] RowHeights { get; set; } = [];

    /// <summary>
    /// Per-cell inner layout results, indexed [rowIndex][colIndex]. For a spanning cell the layout is
    /// stored at its top-left slot; slots it covers hold an empty layout.
    /// </summary>
    public LayoutBlock[][] Cells { get; set; } = [];

    /// <summary>Inner padding applied on each side of a cell's content area.</summary>
    public float CellPadding { get; set; }

    /// <summary>Width of the grid lines between and around cells, in logical pixels.</summary>
    public float BorderWidth { get; set; } = 1f;

    /// <summary>Colour of the grid lines and per-cell borders.</summary>
    public EditorColor BorderColor { get; set; } = TableStyle.DefaultBorderColor;

    /// <summary>
    /// Span and decoration info per grid slot, indexed [rowIndex][colIndex].
    /// When empty (or missing a slot), that slot is a plain 1x1 cell with no background or border override.
    /// </summary>
    public TableLayoutCell[][] CellInfo { get; set; } = [];

    /// <summary>Gets the span and decoration info for a slot, or a default 1x1 cell when none is stored.</summary>
    public TableLayoutCell GetCellInfo(int row, int col)
    {
        if (row >= 0 && row < CellInfo.Length && col >= 0 && col < CellInfo[row].Length && CellInfo[row][col] is { } info)
            return info;
        return new TableLayoutCell { AnchorRow = row, AnchorColumn = col };
    }

    /// <summary>
    /// Gets the slot of the cell that occupies (<paramref name="row"/>, <paramref name="col"/>):
    /// the spanning cell for a covered slot, otherwise the slot itself.
    /// </summary>
    public (int Row, int Column) GetOwningCell(int row, int col)
    {
        var info = GetCellInfo(row, col);
        return (info.AnchorRow, info.AnchorColumn);
    }

    /// <summary>X of the left edge of column <paramref name="col"/> (just inside its left grid line), relative to the table.</summary>
    public float GetColumnX(int col)
    {
        float x = BorderWidth;
        for (int c = 0; c < col && c < ColumnWidths.Length; c++)
            x += ColumnWidths[c] + BorderWidth;
        return x;
    }

    /// <summary>Y of the top edge of row <paramref name="row"/> (just below its top grid line), relative to the table.</summary>
    public float GetRowY(int row)
    {
        float y = BorderWidth;
        for (int r = 0; r < row && r < RowHeights.Length; r++)
            y += RowHeights[r] + BorderWidth;
        return y;
    }

    /// <summary>
    /// Gets the area of the cell that occupies (<paramref name="row"/>, <paramref name="col"/>),
    /// relative to the table's top-left corner. Covers every slot of the cell's span and the grid lines
    /// between them, but not the grid lines around it.
    /// </summary>
    public EditorRect GetCellBounds(int row, int col)
    {
        var (anchorRow, anchorCol) = GetOwningCell(row, col);
        var info = GetCellInfo(anchorRow, anchorCol);
        int rowSpan = Math.Max(1, info.RowSpan);
        int colSpan = Math.Max(1, info.ColumnSpan);

        float width = 0f;
        for (int c = anchorCol; c < anchorCol + colSpan && c < ColumnWidths.Length; c++)
            width += ColumnWidths[c];
        width += (colSpan - 1) * BorderWidth;

        float height = 0f;
        for (int r = anchorRow; r < anchorRow + rowSpan && r < RowHeights.Length; r++)
            height += RowHeights[r];
        height += (rowSpan - 1) * BorderWidth;

        return new EditorRect(GetColumnX(anchorCol), GetRowY(anchorRow), width, height);
    }
}

/// <summary>
/// Span and decoration info for one grid slot of a <see cref="TableLayoutBlock"/>.
/// </summary>
public sealed class TableLayoutCell
{
    /// <summary>Row of the cell that occupies this slot (this slot's own row unless it is covered by a span).</summary>
    public int AnchorRow { get; init; }

    /// <summary>Column of the cell that occupies this slot (this slot's own column unless it is covered by a span).</summary>
    public int AnchorColumn { get; init; }

    /// <summary>Number of rows the cell spans, after clamping to the grid. 0 for a covered slot.</summary>
    public int RowSpan { get; init; } = 1;

    /// <summary>Number of columns the cell spans, after clamping to the grid. 0 for a covered slot.</summary>
    public int ColumnSpan { get; init; } = 1;

    /// <summary>Whether this slot is covered by another cell's span (and so is not drawn or hit-tested itself).</summary>
    public bool IsCovered => RowSpan == 0;

    /// <summary>Effective background fill: the cell's own background, or the header-row background; null for none.</summary>
    public EditorColor? Background { get; init; }

    /// <summary>Per-side border widths for the cell, or null to use the table's <see cref="TableLayoutBlock.BorderWidth"/>.</summary>
    public EditorBorder? Border { get; init; }
}
