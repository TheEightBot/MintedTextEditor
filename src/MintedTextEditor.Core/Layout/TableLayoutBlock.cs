namespace MintedTextEditor.Core.Layout;

/// <summary>
/// Layout result for a <see cref="Document.TableBlock"/>.
/// Stores per-cell nested layouts used for rendering and hit-testing the table grid.
/// </summary>
public sealed class TableLayoutBlock : LayoutBlock
{
    /// <summary>Width of each column in logical pixels.</summary>
    public float[] ColumnWidths { get; set; } = [];

    /// <summary>Height of each row in logical pixels (includes cell padding).</summary>
    public float[] RowHeights { get; set; } = [];

    /// <summary>Per-cell inner layout results, indexed [rowIndex][colIndex].</summary>
    public LayoutBlock[][] Cells { get; set; } = [];

    /// <summary>Inner padding applied on each side of a cell's content area.</summary>
    public float CellPadding { get; set; }
}
