using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Tests;

public class TableTests
{
    // ── Helper ─────────────────────────────────────────────────────────────

    private static Document.Document MakeDoc() => new();
    private static DocumentPosition Pos(int block = 0) => new(block, 0, 0);

    // ── InsertTable ────────────────────────────────────────────────────────

    [Fact]
    public void InsertTable_AddsTableBlockToDocument()
    {
        var doc = MakeDoc();
        TableEngine.InsertTable(doc, Pos(), 2, 3);
        Assert.Contains(doc.Blocks, b => b is TableBlock);
    }

    [Fact]
    public void InsertTable_CorrectRowCount()
    {
        var doc = MakeDoc();
        var table = TableEngine.InsertTable(doc, Pos(), 3, 4);
        Assert.Equal(3, table.RowCount);
    }

    [Fact]
    public void InsertTable_CorrectColumnCount()
    {
        var doc = MakeDoc();
        var table = TableEngine.InsertTable(doc, Pos(), 2, 5);
        Assert.Equal(5, table.ColumnCount);
    }

    [Fact]
    public void TableBlock_InitializesColumnWidths_ForAllColumns()
    {
        var table = new TableBlock(2, 4);
        Assert.Equal(4, table.ColumnWidths.Count);
    }

    [Fact]
    public void InsertTable_CellsHaveDefaultParagraph()
    {
        var doc = MakeDoc();
        var table = TableEngine.InsertTable(doc, Pos(), 2, 2);
        var cell = table.GetCell(0, 0)!;
        Assert.Single(cell.Blocks);
        Assert.IsType<Paragraph>(cell.Blocks[0]);
    }

    [Fact]
    public void InsertTable_ThrowsOnZeroRows()
    {
        var doc = MakeDoc();
        Assert.Throws<ArgumentOutOfRangeException>(() => TableEngine.InsertTable(doc, Pos(), 0, 2));
    }

    [Fact]
    public void InsertTable_ThrowsOnZeroCols()
    {
        var doc = MakeDoc();
        Assert.Throws<ArgumentOutOfRangeException>(() => TableEngine.InsertTable(doc, Pos(), 2, 0));
    }

    [Fact]
    public void InsertTable_NotifiesDocumentChanged()
    {
        var doc = MakeDoc();
        bool notified = false;
        doc.Changed += (_, _) => notified = true;
        TableEngine.InsertTable(doc, Pos(), 2, 2);
        Assert.True(notified);
    }

    // ── InsertRow / DeleteRow ──────────────────────────────────────────────

    [Fact]
    public void InsertRow_IncreasesRowCount()
    {
        var doc = MakeDoc();
        var table = TableEngine.InsertTable(doc, Pos(), 2, 3);
        TableEngine.InsertRow(table, 0);
        Assert.Equal(3, table.RowCount);
    }

    [Fact]
    public void InsertRow_NewRowHasCorrectCellCount()
    {
        var doc = MakeDoc();
        var table = TableEngine.InsertTable(doc, Pos(), 2, 4);
        var row = TableEngine.InsertRow(table, 1);
        Assert.Equal(4, row.Cells.Count);
    }

    [Fact]
    public void DeleteRow_DecreasesRowCount()
    {
        var doc = MakeDoc();
        var table = TableEngine.InsertTable(doc, Pos(), 3, 2);
        TableEngine.DeleteRow(table, 0);
        Assert.Equal(2, table.RowCount);
    }

    [Fact]
    public void DeleteRow_ThrowsWhenOnlyOneRowLeft()
    {
        var doc = MakeDoc();
        var table = TableEngine.InsertTable(doc, Pos(), 1, 2);
        Assert.Throws<InvalidOperationException>(() => TableEngine.DeleteRow(table, 0));
    }

    // ── InsertColumn / DeleteColumn ────────────────────────────────────────

    [Fact]
    public void InsertColumn_IncreasesColumnCount()
    {
        var doc = MakeDoc();
        var table = TableEngine.InsertTable(doc, Pos(), 2, 3);
        TableEngine.InsertColumn(table, 1);
        Assert.Equal(4, table.ColumnCount);
    }

    [Fact]
    public void InsertColumn_AddsCellToEveryRow()
    {
        var doc = MakeDoc();
        var table = TableEngine.InsertTable(doc, Pos(), 3, 2);
        TableEngine.InsertColumn(table, 0);
        Assert.All(table.Rows, r => Assert.Equal(3, r.Cells.Count));
    }

    [Fact]
    public void DeleteColumn_DecreasesColumnCount()
    {
        var doc = MakeDoc();
        var table = TableEngine.InsertTable(doc, Pos(), 2, 3);
        TableEngine.DeleteColumn(table, 0);
        Assert.Equal(2, table.ColumnCount);
    }

    [Fact]
    public void DeleteColumn_ThrowsWhenOnlyOneColumnLeft()
    {
        var doc = MakeDoc();
        var table = TableEngine.InsertTable(doc, Pos(), 2, 1);
        Assert.Throws<InvalidOperationException>(() => TableEngine.DeleteColumn(table, 0));
    }

    // ── MergeCells ────────────────────────────────────────────────────────

    [Fact]
    public void MergeCells_SetsSpanOnAnchorCell()
    {
        var doc = MakeDoc();
        var table = TableEngine.InsertTable(doc, Pos(), 2, 3);
        TableEngine.MergeCells(table, 0, 0, 1, 2);
        var anchor = table.GetCell(0, 0)!;
        Assert.Equal(2, anchor.ColumnSpan);
    }

    [Fact]
    public void MergeCells_MarksAbsorbedCellsAsMerged()
    {
        var doc = MakeDoc();
        var table = TableEngine.InsertTable(doc, Pos(), 2, 3);
        TableEngine.MergeCells(table, 0, 0, 2, 2);
        Assert.True(table.GetCell(0, 1)!.IsMerged);
        Assert.True(table.GetCell(1, 0)!.IsMerged);
        Assert.True(table.GetCell(1, 1)!.IsMerged);
    }

    [Fact]
    public void MergeCells_AnchorCellNotMarkedMerged()
    {
        var doc = MakeDoc();
        var table = TableEngine.InsertTable(doc, Pos(), 2, 2);
        TableEngine.MergeCells(table, 0, 0, 2, 2);
        Assert.False(table.GetCell(0, 0)!.IsMerged);
    }

    // ── SplitCell ─────────────────────────────────────────────────────────

    [Fact]
    public void SplitCell_ResetsSpanToOne()
    {
        var doc = MakeDoc();
        var table = TableEngine.InsertTable(doc, Pos(), 2, 3);
        TableEngine.MergeCells(table, 0, 0, 1, 2);
        var anchor = table.GetCell(0, 0)!;
        TableEngine.SplitCell(table, anchor);
        Assert.Equal(1, anchor.ColumnSpan);
        Assert.Equal(1, anchor.RowSpan);
    }

    [Fact]
    public void SplitCell_UnmarksPreviouslyMergedCells()
    {
        var doc = MakeDoc();
        var table = TableEngine.InsertTable(doc, Pos(), 2, 2);
        TableEngine.MergeCells(table, 0, 0, 2, 2);
        var anchor = table.GetCell(0, 0)!;
        TableEngine.SplitCell(table, anchor);
        Assert.False(table.GetCell(0, 1)!.IsMerged);
        Assert.False(table.GetCell(1, 0)!.IsMerged);
        Assert.False(table.GetCell(1, 1)!.IsMerged);
    }

    // ── SetCellBackground ─────────────────────────────────────────────────

    [Fact]
    public void SetCellBackground_SetsColor()
    {
        var doc = MakeDoc();
        var table = TableEngine.InsertTable(doc, Pos(), 2, 2);
        var cell = table.GetCell(0, 0)!;
        TableEngine.SetCellBackground(table, cell, EditorColor.Blue);
        Assert.Equal(EditorColor.Blue, cell.Background);
    }

    [Fact]
    public void SetCellBackground_NotifiesDocumentChanged()
    {
        var doc = MakeDoc();
        var table = TableEngine.InsertTable(doc, Pos(), 2, 2);
        bool notified = false;
        doc.Changed += (_, _) => notified = true;
        TableEngine.SetCellBackground(table, table.GetCell(0, 0)!, EditorColor.Red);
        Assert.True(notified);
    }

    // ── GetText / nested content ───────────────────────────────────────────

    [Fact]
    public void GetText_ReturnsTabSeparatedCellsNewLineSeparatedRows()
    {
        var table = new TableBlock(2, 2);
        ((Paragraph)table.GetCell(0, 0)!.Blocks[0]).AppendRun("A");
        ((Paragraph)table.GetCell(0, 1)!.Blocks[0]).AppendRun("B");
        ((Paragraph)table.GetCell(1, 0)!.Blocks[0]).AppendRun("C");
        ((Paragraph)table.GetCell(1, 1)!.Blocks[0]).AppendRun("D");
        Assert.Equal("A\tB\nC\tD", table.GetText());
    }
}
