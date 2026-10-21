using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Html;
using MintedTextEditor.Core.Input;
using MintedTextEditor.Core.Layout;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Tests;

/// <summary>
/// Layout, hit-testing and rendering of table styling: <see cref="TableStyle"/>, cell backgrounds,
/// cell borders and row/column spans.
/// </summary>
public class TableStylingTests
{
    private readonly TextLayoutEngine _engine = new();
    private readonly MockDrawingContext _ctx = new();

    // Height of a row of empty cells with the default 4-unit padding.
    private readonly float DefaultRowHeight;

    public TableStylingTests()
    {
        var empty = new EditorDocument(new Block[] { new Paragraph() });
        DefaultRowHeight = _engine.Layout(empty, 400f, _ctx).Blocks[0].TotalHeight + 8f;
    }

    private (EditorDocument Doc, TableLayoutBlock Layout, DocumentLayout DocLayout) LayoutTable(TableBlock table, float width = 400f)
    {
        var doc = new EditorDocument(new Block[] { table });
        var layout = _engine.Layout(doc, width, _ctx);
        return (doc, Assert.IsType<TableLayoutBlock>(layout.Blocks[0]), layout);
    }

    private static void SetText(TableBlock table, int row, int col, string text)
        => ((Paragraph)table.GetCell(row, col)!.Blocks[0]).AppendRun(text);

    private List<MockDrawingContext.FillRectCall> RenderFills(DocumentLayout layout)
    {
        var renderer = new DocumentRenderer { ShowScrollbar = false };
        var ctx = new MockDrawingContext();
        renderer.Render(layout, new EditorRect(0f, 0f, 400f, 400f), ctx);
        return ctx.FillRectCalls.Skip(1).ToList(); // first call is the viewport background
    }

    // ── Defaults ───────────────────────────────────────────────────────────

    [Fact]
    public void Layout_DefaultStyle_KeepsOneUnitBorderAndFourUnitPadding()
    {
        var (_, layout, _) = LayoutTable(new TableBlock(2, 2));

        Assert.Equal(1f, layout.BorderWidth);
        Assert.Equal(4f, layout.CellPadding);
        Assert.Equal(new EditorColor(160, 160, 160), layout.BorderColor);
        Assert.Equal((400f - 3f) / 2f, layout.ColumnWidths[0]);
        Assert.Equal(DefaultRowHeight, layout.RowHeights[0]);
        Assert.Equal(2 * DefaultRowHeight + 3f, layout.TotalHeight);
        Assert.All(layout.CellInfo.SelectMany(r => r), i =>
        {
            Assert.False(i.IsCovered);
            Assert.Null(i.Background);
        });
    }

    [Fact]
    public void Render_DefaultStyle_DrawsOnlyTheGrid()
    {
        var (_, layout, docLayout) = LayoutTable(new TableBlock(2, 2));
        var fills = RenderFills(docLayout);

        float totalWidth = layout.ColumnWidths.Sum() + 3f;
        // 3 full-width horizontal lines + 3 vertical lines per row, all 1 unit in the default grey.
        Assert.Equal(9, fills.Count);
        Assert.All(fills, f => Assert.Equal(new EditorColor(160, 160, 160), f.Paint.Color));
        Assert.Equal(3, fills.Count(f => f.Rect.Height == 1f && f.Rect.Width == totalWidth));
        Assert.Equal(6, fills.Count(f => f.Rect.Width == 1f && f.Rect.Height == DefaultRowHeight));
        Assert.Contains(fills, f => f.Rect == new EditorRect(0f, 0f, totalWidth, 1f));
        Assert.Contains(fills, f => f.Rect == new EditorRect(1f + layout.ColumnWidths[0], 1f, 1f, DefaultRowHeight));
    }

    // ── TableStyle ─────────────────────────────────────────────────────────

    [Fact]
    public void Layout_AppliesTableStyleBorderWidthAndPadding()
    {
        var table = new TableBlock(2, 2);
        table.Style.BorderWidth = 3f;
        table.Style.CellPadding = 10f;
        var (_, layout, _) = LayoutTable(table);

        Assert.Equal(3f, layout.BorderWidth);
        Assert.Equal(10f, layout.CellPadding);
        Assert.Equal((400f - 9f) / 2f, layout.ColumnWidths[0]);
        float rowHeight = DefaultRowHeight - 8f + 20f;
        Assert.Equal(rowHeight, layout.RowHeights[0]);
        Assert.Equal(2 * rowHeight + 9f, layout.TotalHeight);
        Assert.Equal(new EditorRect(3f + layout.ColumnWidths[0] + 3f, 3f + rowHeight + 3f, layout.ColumnWidths[1], rowHeight),
            layout.GetCellBounds(1, 1));
    }

    [Fact]
    public void Render_UsesTableStyleBorderColorAndWidth()
    {
        var table = new TableBlock(1, 1);
        table.Style.BorderWidth = 2f;
        table.Style.BorderColor = new EditorColor(255, 0, 0);
        var (_, layout, docLayout) = LayoutTable(table);
        var fills = RenderFills(docLayout);

        Assert.Equal(4, fills.Count);
        Assert.All(fills, f => Assert.Equal(new EditorColor(255, 0, 0), f.Paint.Color));
        Assert.Contains(fills, f => f.Rect == new EditorRect(0f, 0f, layout.ColumnWidths[0] + 4f, 2f));
        Assert.Contains(fills, f => f.Rect == new EditorRect(0f, 2f, 2f, layout.RowHeights[0]));
    }

    [Fact]
    public void Render_ZeroBorderWidth_DrawsNoGrid()
    {
        var table = new TableBlock(2, 2);
        table.Style.BorderWidth = 0f;
        var (_, _, docLayout) = LayoutTable(table);

        Assert.Empty(RenderFills(docLayout));
    }

    [Fact]
    public void Render_HeaderRow_FillsFirstRowWithHeaderBackground()
    {
        var header = new EditorColor(200, 220, 255);
        var table = new TableBlock(2, 2);
        table.Style.HasHeaderRow = true;
        table.Style.HeaderBackground = header;
        var (_, layout, docLayout) = LayoutTable(table);
        var fills = RenderFills(docLayout).Where(f => f.Paint.Color == header).ToList();

        Assert.Equal(2, fills.Count);
        Assert.Contains(fills, f => f.Rect == layout.GetCellBounds(0, 0));
        Assert.Contains(fills, f => f.Rect == layout.GetCellBounds(0, 1));
    }

    [Fact]
    public void Layout_HeaderRowWithoutColor_UsesDefaultTint_AndCellBackgroundWins()
    {
        var own = new EditorColor(0, 128, 0);
        var table = new TableBlock(2, 2);
        table.Style.HasHeaderRow = true;
        table.GetCell(0, 1)!.Background = own;
        var (_, layout, _) = LayoutTable(table);

        Assert.Equal(TableStyle.DefaultHeaderBackground, layout.GetCellInfo(0, 0).Background);
        Assert.Equal(own, layout.GetCellInfo(0, 1).Background);
        Assert.Null(layout.GetCellInfo(1, 0).Background);
    }

    // ── Cell background and border ─────────────────────────────────────────

    [Fact]
    public void Render_CellBackground_FillsCellArea()
    {
        var yellow = new EditorColor(255, 255, 0);
        var table = new TableBlock(2, 2);
        table.GetCell(1, 0)!.Background = yellow;
        var (_, layout, docLayout) = LayoutTable(table);
        var fill = Assert.Single(RenderFills(docLayout), f => f.Paint.Color == yellow);

        Assert.Equal(new EditorRect(1f, 1f + DefaultRowHeight + 1f, layout.ColumnWidths[0], DefaultRowHeight), fill.Rect);
    }

    [Fact]
    public void Render_CellBackground_DrawnBeforeCellText()
    {
        var yellow = new EditorColor(255, 255, 0);
        var table = new TableBlock(1, 1);
        table.GetCell(0, 0)!.Background = yellow;
        SetText(table, 0, 0, "Hi");
        var (_, _, docLayout) = LayoutTable(table);

        var renderer = new DocumentRenderer { ShowScrollbar = false };
        var ctx = new MockDrawingContext();
        renderer.Render(docLayout, new EditorRect(0f, 0f, 400f, 400f), ctx);

        Assert.Contains(ctx.FillRectCalls, f => f.Paint.Color == yellow);
        Assert.Contains(ctx.DrawTextCalls, t => t.Text == "Hi");
    }

    [Fact]
    public void Render_CellBorder_UsesWiderSideOnSharedEdges()
    {
        var table = new TableBlock(1, 2);
        table.GetCell(0, 0)!.Border = new EditorBorder(top: 0f, right: 3f, bottom: 0f, left: 5f);
        var (_, layout, docLayout) = LayoutTable(table);
        var fills = RenderFills(docLayout);
        float rowTop = 1f;
        float h = layout.RowHeights[0];

        // Left outer edge: the cell's own 5-unit side.
        Assert.Contains(fills, f => f.Rect == new EditorRect(0f, rowTop, 5f, h));
        // Shared edge: wider of 3 (cell 0 right) and 1 (cell 1 default left).
        Assert.Contains(fills, f => f.Rect == new EditorRect(1f + layout.ColumnWidths[0], rowTop, 3f, h));
        // Top edge: cell 0 has 0, so only cell 1's default 1-unit top is drawn.
        float col1Line = 1f + layout.ColumnWidths[0];
        Assert.Contains(fills, f => f.Rect == new EditorRect(col1Line, 0f, layout.ColumnWidths[1] + 2f, 1f));
        Assert.DoesNotContain(fills, f => f.Rect.Y == 0f && f.Rect.X == 0f && f.Rect.Height == 1f);
    }

    // ── Spans: layout ──────────────────────────────────────────────────────

    [Fact]
    public void Layout_ColumnSpan_CellOccupiesCombinedWidth()
    {
        var table = new TableBlock(2, 3);
        TableEngine.MergeCells(table, startRow: 0, startCol: 0, rowSpan: 1, colSpan: 2);
        // 25 chars = 200px: wider than one column's content area (~124px), narrower than two.
        SetText(table, 0, 0, new string('x', 25));
        var (_, layout, _) = LayoutTable(table);

        float colWidth = (400f - 4f) / 3f;
        var bounds = layout.GetCellBounds(0, 0);
        Assert.Equal(1f, bounds.X);
        Assert.Equal(1f, bounds.Y);
        Assert.Equal(2 * colWidth + 1f, bounds.Width, 3);
        Assert.Equal(DefaultRowHeight, bounds.Height);

        Assert.Single(layout.Cells[0][0].Lines);
        Assert.Equal(2, layout.GetCellInfo(0, 0).ColumnSpan);
        Assert.True(layout.GetCellInfo(0, 1).IsCovered);
        Assert.Equal((0, 0), layout.GetOwningCell(0, 1));
        Assert.Equal(bounds, layout.GetCellBounds(0, 1));
        Assert.False(layout.GetCellInfo(0, 2).IsCovered);
    }

    [Fact]
    public void Layout_RowSpan_TallCellGrowsLastSpannedRow()
    {
        var table = new TableBlock(3, 2);
        TableEngine.MergeCells(table, startRow: 0, startCol: 0, rowSpan: 2, colSpan: 1);
        var anchor = table.GetCell(0, 0)!;
        for (int i = 0; i < 5; i++)
        {
            var p = new Paragraph();
            p.AppendRun("line");
            anchor.Blocks.Add(p);
        }
        var (_, layout, _) = LayoutTable(table);

        var bounds = layout.GetCellBounds(0, 0);
        Assert.Equal(DefaultRowHeight, layout.RowHeights[0]);
        Assert.True(layout.RowHeights[1] > DefaultRowHeight);
        Assert.Equal(DefaultRowHeight, layout.RowHeights[2]);
        Assert.Equal(layout.RowHeights[0] + layout.RowHeights[1] + 1f, bounds.Height);
        Assert.Equal(layout.Cells[0][0].TotalHeight + 2f * layout.CellPadding, bounds.Height, 3);
        Assert.True(layout.GetCellInfo(1, 0).IsCovered);
        Assert.Equal((0, 0), layout.GetOwningCell(1, 0));
    }

    [Fact]
    public void Layout_ShortRowSpan_DoesNotGrowRows()
    {
        var table = new TableBlock(2, 2);
        TableEngine.MergeCells(table, 0, 0, 2, 1);
        var (_, layout, _) = LayoutTable(table);

        Assert.Equal(DefaultRowHeight, layout.RowHeights[0]);
        Assert.Equal(DefaultRowHeight, layout.RowHeights[1]);
        Assert.Equal(2 * DefaultRowHeight + 1f, layout.GetCellBounds(0, 0).Height);
    }

    [Fact]
    public void Layout_SpanBeyondGrid_IsClamped()
    {
        var table = new TableBlock(2, 2);
        table.GetCell(1, 1)!.ColumnSpan = 5;
        table.GetCell(1, 1)!.RowSpan = 5;
        var (_, layout, _) = LayoutTable(table);

        Assert.Equal(1, layout.GetCellInfo(1, 1).ColumnSpan);
        Assert.Equal(1, layout.GetCellInfo(1, 1).RowSpan);
    }

    [Fact]
    public void GetSpanAnchor_ReturnsSpanningCellForCoveredSlot()
    {
        var table = new TableBlock(3, 3);
        TableEngine.MergeCells(table, 1, 1, 2, 2);

        Assert.Equal((1, 1), table.GetSpanAnchor(2, 2));
        Assert.Equal((1, 1), table.GetSpanAnchor(1, 2));
        Assert.Equal((0, 2), table.GetSpanAnchor(0, 2));
        Assert.Equal((2, 0), table.GetSpanAnchor(2, 0));
    }

    // ── Spans: rendering ───────────────────────────────────────────────────

    [Fact]
    public void Render_ColumnSpan_SkipsInteriorBorderAndCoveredCell()
    {
        var table = new TableBlock(2, 2);
        TableEngine.MergeCells(table, 0, 0, 1, 2);
        SetText(table, 0, 1, "hidden");
        var (_, layout, docLayout) = LayoutTable(table);

        var renderer = new DocumentRenderer { ShowScrollbar = false };
        var ctx = new MockDrawingContext();
        renderer.Render(docLayout, new EditorRect(0f, 0f, 400f, 400f), ctx);
        var fills = ctx.FillRectCalls.Skip(1).ToList();

        float midLine = 1f + layout.ColumnWidths[0];
        // No vertical line through the merged first row, but row 2 still has one.
        Assert.DoesNotContain(fills, f => f.Rect.X == midLine && f.Rect.Y == 1f);
        Assert.Contains(fills, f => f.Rect.X == midLine && f.Rect.Y == 1f + DefaultRowHeight + 1f && f.Rect.Width == 1f);
        // The covered cell's content is not drawn.
        Assert.DoesNotContain(ctx.DrawTextCalls, t => t.Text == "hidden");
        // The spanning cell is clipped to its combined width.
        float spanWidth = layout.ColumnWidths[0] + layout.ColumnWidths[1] + 1f;
        Assert.Contains($"ClipRect(0,0,{spanWidth - 8f},{DefaultRowHeight - 8f})", ctx.TransformCalls);
    }

    [Fact]
    public void Render_RowSpan_BackgroundCoversSpannedRows()
    {
        var blue = new EditorColor(0, 0, 255);
        var table = new TableBlock(3, 2);
        TableEngine.MergeCells(table, 0, 1, 2, 1);
        table.GetCell(0, 1)!.Background = blue;
        var (_, layout, docLayout) = LayoutTable(table);
        var fills = RenderFills(docLayout);

        var fill = Assert.Single(fills, f => f.Paint.Color == blue);
        Assert.Equal(layout.GetCellBounds(0, 1), fill.Rect);
        Assert.Equal(2 * DefaultRowHeight + 1f, fill.Rect.Height);
        // The horizontal line between rows 0 and 1 is only drawn under column 0.
        float y = 1f + DefaultRowHeight;
        var line = Assert.Single(fills, f => f.Rect.Y == y && f.Rect.Height == 1f);
        Assert.Equal(new EditorRect(0f, y, layout.ColumnWidths[0] + 2f, 1f), line.Rect);
    }

    // ── Spans: hit-testing and caret ───────────────────────────────────────

    [Fact]
    public void HitTest_InCoveredSlotOfColumnSpan_ResolvesToSpanningCell()
    {
        var table = new TableBlock(2, 2);
        TableEngine.MergeCells(table, 0, 0, 1, 2);
        SetText(table, 0, 0, "Hello");
        var (doc, layout, docLayout) = LayoutTable(table);

        // Click in the second column's area of the merged first row, past the text.
        float x = 1f + layout.ColumnWidths[0] + 1f + 20f;
        var result = DocumentHitTester.HitTest(x, 10f, docLayout, doc, _ctx);

        Assert.Equal(0, result.Position.CellRow);
        Assert.Equal(0, result.Position.CellCol);
        Assert.Equal(5, result.Position.Offset);
    }

    [Fact]
    public void HitTest_InCoveredSlotOfRowSpan_ResolvesToSpanningCell()
    {
        var table = new TableBlock(3, 2);
        TableEngine.MergeCells(table, 0, 1, 2, 1);
        SetText(table, 0, 1, "Hello");
        var (doc, layout, docLayout) = LayoutTable(table);

        float x = 1f + layout.ColumnWidths[0] + 1f + 4f + 16f; // between "He" and "llo"
        float y = 1f + DefaultRowHeight + 1f + 10f;           // inside row 1
        var result = DocumentHitTester.HitTest(x, y, docLayout, doc, _ctx);

        Assert.Equal(0, result.Position.CellRow);
        Assert.Equal(1, result.Position.CellCol);
        Assert.Equal(2, result.Position.Offset);
    }

    [Fact]
    public void HitTest_UsesTableStyleBorderAndPadding()
    {
        var table = new TableBlock(1, 2);
        table.Style.BorderWidth = 5f;
        table.Style.CellPadding = 10f;
        SetText(table, 0, 1, "Hello");
        var (doc, layout, docLayout) = LayoutTable(table);

        float x = 5f + layout.ColumnWidths[0] + 5f + 10f + 24f; // after "Hel"
        var result = DocumentHitTester.HitTest(x, 15f, docLayout, doc, _ctx);

        Assert.Equal(1, result.Position.CellCol);
        Assert.Equal(3, result.Position.Offset);
    }

    [Fact]
    public void CaretRect_InSpanningCell_UsesCellOrigin()
    {
        var table = new TableBlock(2, 2);
        table.Style.CellPadding = 6f;
        TableEngine.MergeCells(table, 0, 1, 2, 1);
        var (doc, layout, docLayout) = LayoutTable(table);

        var rect = new CaretRenderer().GetCaretRect(new DocumentPosition(0, 0, 0, 1, 1), docLayout, doc, _ctx);

        Assert.Equal(1f + layout.ColumnWidths[0] + 1f + 6f, rect.X);
        Assert.Equal(1f + 6f, rect.Y);
    }

    [Fact]
    public void Tab_SkipsCellsCoveredBySpan()
    {
        var table = new TableBlock(2, 3);
        TableEngine.MergeCells(table, 0, 0, 1, 2);
        var doc = new EditorDocument(new Block[] { table });
        var docLayout = _engine.Layout(doc, 400f, _ctx);
        var caret = new Caret();
        var ctrl = new EditorInputController(caret, new CaretRenderer());
        caret.MoveTo(new DocumentPosition(0, 0, 0, 0, 0));

        ctrl.HandleKeyDown(new EditorKeyEventArgs(EditorKey.Tab), doc, docLayout, _ctx);
        Assert.Equal(new DocumentPosition(0, 0, 0, 0, 2), caret.Position);

        ctrl.HandleKeyDown(new EditorKeyEventArgs(EditorKey.Tab, modifiers: InputModifiers.Shift), doc, docLayout, _ctx);
        Assert.Equal(new DocumentPosition(0, 0, 0, 0, 0), caret.Position);
    }

    // ── Spans: caret normalization ─────────────────────────────────────────

    private EditorInputController NewController(Caret caret) => new(caret, new CaretRenderer());

    private bool Press(EditorInputController ctrl, EditorDocument doc, EditorKey key, InputModifiers modifiers = InputModifiers.None)
        => ctrl.HandleKeyDown(new EditorKeyEventArgs(key, modifiers: modifiers), doc, _engine.Layout(doc, 400f, _ctx), _ctx);

    private static string CellText(TableBlock table, int row, int col) => table.GetCell(row, col)!.Blocks[0].GetText();

    [Fact]
    public void MoveDocEnd_LastSlotCoveredByMerge_GoesToEndOfSpanningCell_AndTypingEditsIt()
    {
        var table = new TableBlock(1, 2);
        TableEngine.MergeCells(table, 0, 0, 1, 2);
        SetText(table, 0, 0, "Hi");
        var doc = new EditorDocument(new Block[] { table });
        var caret = new Caret();
        var ctrl = NewController(caret);

        Press(ctrl, doc, EditorKey.End, InputModifiers.Control);
        Assert.Equal(new DocumentPosition(0, 0, 2, 0, 0), caret.Position);
        Assert.Equal(caret.Position, ctrl.Selection.Active);

        ctrl.HandleTextInput("!", doc);
        Assert.Equal("Hi!", CellText(table, 0, 0));
        Assert.Equal("", CellText(table, 0, 1));
    }

    [Fact]
    public void MoveLeft_OutOfTableIntoPreviousTable_LandsInCellCoveringItsLastSlot()
    {
        var table = new TableBlock(2, 2);
        TableEngine.MergeCells(table, 0, 1, 2, 1);
        SetText(table, 0, 1, "abc");
        var doc = new EditorDocument(new Block[] { table, new TableBlock(1, 1) });
        var caret = new Caret();
        var ctrl = NewController(caret);
        caret.MoveTo(new DocumentPosition(1, 0, 0, 0, 0));

        Press(ctrl, doc, EditorKey.Left);

        Assert.Equal(new DocumentPosition(0, 0, 3, 0, 1), caret.Position);
    }

    [Fact]
    public void TextInput_CaretSetInCoveredSlot_EditsSpanningCell()
    {
        var table = new TableBlock(2, 2);
        TableEngine.MergeCells(table, 0, 0, 2, 1);
        SetText(table, 0, 0, "ab");
        var doc = new EditorDocument(new Block[] { table });
        var caret = new Caret();
        var ctrl = NewController(caret);
        caret.MoveTo(new DocumentPosition(0, 0, 7, 1, 0)); // offset past the anchor's content

        ctrl.HandleTextInput("c", doc);

        Assert.Equal("abc", CellText(table, 0, 0));
        Assert.Equal("", CellText(table, 1, 0));
        Assert.Equal(new DocumentPosition(0, 0, 3, 0, 0), caret.Position);
    }

    [Fact]
    public void ToVisibleCell_ClampsOffsetsToSpanningCellContent()
    {
        var table = new TableBlock(1, 2);
        TableEngine.MergeCells(table, 0, 0, 1, 2);
        SetText(table, 0, 0, "abcd");
        var doc = new EditorDocument(new Block[] { table });

        Assert.Equal(new DocumentPosition(0, 0, 4, 0, 0), EditorInputController.ToVisibleCell(doc, new DocumentPosition(0, 0, 9, 0, 1)));
        Assert.Equal(new DocumentPosition(0, 0, 4, 0, 0), EditorInputController.ToVisibleCell(doc, new DocumentPosition(0, 3, 0, 0, 1)));
        Assert.Equal(new DocumentPosition(0, 0, 2, 0, 0), EditorInputController.ToVisibleCell(doc, new DocumentPosition(0, 0, 2, 0, 1)));
        // Uncovered slots are left alone.
        var visible = new DocumentPosition(0, 0, 9, 0, 0);
        Assert.Equal(visible, EditorInputController.ToVisibleCell(doc, visible));
    }

    [Fact]
    public void MoveRight_AtEndOfCellCoveringLastSlot_LeavesTable()
    {
        var table = new TableBlock(1, 2);
        TableEngine.MergeCells(table, 0, 0, 1, 2);
        SetText(table, 0, 0, "ab");
        var doc = new EditorDocument(new Block[] { table, new Paragraph() });
        var caret = new Caret();
        var ctrl = NewController(caret);
        caret.MoveTo(new DocumentPosition(0, 0, 2, 0, 0));

        Press(ctrl, doc, EditorKey.Right);

        Assert.Equal(new DocumentPosition(1, 0, 0), caret.Position);
    }

    [Fact]
    public void Tab_InMergedLastCell_LeavesTableInsteadOfInsertingTab()
    {
        var table = new TableBlock(1, 2);
        TableEngine.MergeCells(table, 0, 0, 1, 2);
        var doc = new EditorDocument(new Block[] { table });
        var caret = new Caret();
        var ctrl = NewController(caret);
        caret.MoveTo(new DocumentPosition(0, 0, 0, 0, 0));

        Assert.True(Press(ctrl, doc, EditorKey.Tab));

        Assert.Equal("", CellText(table, 0, 0));
        Assert.Equal(2, doc.Blocks.Count);
        Assert.Equal(new DocumentPosition(1, 0, 0), caret.Position);
    }

    [Fact]
    public void Tab_FromLastVisibleCell_WhenLastSlotCoveredByRowSpan_LeavesTable()
    {
        // (0,1) spans both rows, so (1,0) is the last cell in reading order and (1,1) is covered.
        var table = new TableBlock(2, 2);
        TableEngine.MergeCells(table, 0, 1, 2, 1);
        var doc = new EditorDocument(new Block[] { table, new Paragraph() });
        var caret = new Caret();
        var ctrl = NewController(caret);
        caret.MoveTo(new DocumentPosition(0, 0, 0, 1, 0));

        Press(ctrl, doc, EditorKey.Tab);

        Assert.Equal(new DocumentPosition(1, 0, 0), caret.Position);
        Assert.Equal("", CellText(table, 1, 0));
    }

    // ── Spans: resize boundaries ───────────────────────────────────────────

    [Fact]
    public void FindColumnResizeBoundary_IgnoresBoundaryInsideMergedCellOfPointerRow()
    {
        var table = new TableBlock(2, 3);
        TableEngine.MergeCells(table, 0, 0, 1, 2);
        var (_, layout, _) = LayoutTable(table);
        float boundary0 = layout.GetColumnX(0) + layout.ColumnWidths[0];
        float boundary1 = layout.GetColumnX(1) + layout.ColumnWidths[1];
        float row0Y = layout.GetRowY(0) + 5f;
        float row1Y = layout.GetRowY(1) + 5f;

        Assert.Equal(-1, layout.FindColumnResizeBoundary(boundary0 + 2f, row0Y, 6f));
        Assert.Equal(1, layout.FindColumnResizeBoundary(boundary1 - 2f, row0Y, 6f));
        Assert.Equal(0, layout.FindColumnResizeBoundary(boundary0 + 2f, row1Y, 6f));
        Assert.Equal(-1, layout.FindColumnResizeBoundary(boundary0 + 2f, layout.TotalHeight + 1f, 6f));
        Assert.Equal(-1, layout.FindColumnResizeBoundary(boundary0 + 20f, row1Y, 6f));
    }

    [Fact]
    public void FindRowResizeBoundary_IgnoresBoundaryInsideMergedCellOfPointerColumn()
    {
        var table = new TableBlock(3, 2);
        TableEngine.MergeCells(table, 0, 1, 2, 1);
        var (_, layout, _) = LayoutTable(table);
        float boundary0 = layout.GetRowY(0) + layout.RowHeights[0];
        float boundary1 = layout.GetRowY(1) + layout.RowHeights[1];
        float col0X = layout.GetColumnX(0) + 5f;
        float col1X = layout.GetColumnX(1) + 5f;

        Assert.Equal(-1, layout.FindRowResizeBoundary(col1X, boundary0 + 2f, 6f));
        Assert.Equal(0, layout.FindRowResizeBoundary(col0X, boundary0 + 2f, 6f));
        Assert.Equal(1, layout.FindRowResizeBoundary(col1X, boundary1 - 2f, 6f));
        Assert.Equal(-1, layout.FindRowResizeBoundary(layout.GetColumnX(2) + 1f, boundary0, 6f));
    }

    // ── Outer borders ──────────────────────────────────────────────────────

    [Fact]
    public void Render_WideOuterBottomAndRightBorders_StayInsideTableBounds()
    {
        var table = new TableBlock(2, 2);
        table.GetCell(1, 1)!.Border = new EditorBorder(top: 1f, right: 6f, bottom: 4f, left: 1f);
        var (_, layout, docLayout) = LayoutTable(table);
        var fills = RenderFills(docLayout);
        float totalWidth = layout.GetColumnX(2);

        Assert.All(fills, f =>
        {
            Assert.True(f.Rect.Y + f.Rect.Height <= layout.TotalHeight + 0.001f, $"{f.Rect} extends below the table");
            Assert.True(f.Rect.X + f.Rect.Width <= totalWidth + 0.001f, $"{f.Rect} extends past the right edge");
        });
        // The 4-unit bottom edge under column 1 ends at the table's bottom edge.
        Assert.Contains(fills, f => f.Rect.Height == 4f && f.Rect.Y == layout.TotalHeight - 4f);
        // The 6-unit right edge of row 1 ends at the table's right edge.
        Assert.Contains(fills, f => f.Rect.Width == 6f && f.Rect.X == totalWidth - 6f && f.Rect.Y == layout.GetRowY(1));
    }

    [Fact]
    public void Render_WideOuterBottomBorder_DrawnWhenOnlyItsInsetPartIsVisible()
    {
        var table = new TableBlock(1, 1);
        table.GetCell(0, 0)!.Border = new EditorBorder(top: 1f, right: 1f, bottom: 10f, left: 1f);
        var (_, layout, docLayout) = LayoutTable(table);

        var renderer = new DocumentRenderer { ShowScrollbar = false };
        var ctx = new MockDrawingContext();
        // Viewport ends 5 units above the table's bottom edge, inside the 10-unit bottom border.
        renderer.Render(docLayout, new EditorRect(0f, 0f, 400f, layout.TotalHeight - 5f), ctx);

        Assert.Contains(ctx.FillRectCalls, f => f.Rect.Height == 10f && f.Rect.Y == layout.TotalHeight - 10f);
    }

    // ── HTML ───────────────────────────────────────────────────────────────

    [Fact]
    public void HtmlExport_MergedCells_WritesSpansAndSkipsCoveredCells()
    {
        var table = new TableBlock(2, 2);
        TableEngine.MergeCells(table, 0, 0, 2, 1);
        var html = new HtmlExporter().Export(new EditorDocument(new Block[] { table }));

        Assert.Contains("rowspan=\"2\"", html);
        Assert.Equal(3, html.Split("<td").Length - 1);
    }
}
