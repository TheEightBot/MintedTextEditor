using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Events;
using MintedTextEditor.Core.Formatting;

namespace MintedTextEditor.Core.Tests;

/// <summary>
/// Editing around and inside tables: deleting a selection across a table, hyperlinks and
/// character formatting in table cells, and inserting text before an image. Each edit must be
/// undoable and redoable exactly.
/// </summary>
public class TableEditingFixesTests
{
    private static readonly TextStyle Bold = TextStyle.Default.WithBold(true);

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>A 2x2 table whose cells hold "A1", "B1", "A2", "B2".</summary>
    private static TableBlock Table()
    {
        var table = new TableBlock(2, 2);
        for (int r = 0; r < 2; r++)
            for (int c = 0; c < 2; c++)
                ((Paragraph)table.GetCell(r, c)!.Blocks[0]).AppendRun($"{(char)('A' + c)}{r + 1}");
        return table;
    }

    private static Paragraph CellParagraph(TableBlock table, int row, int col)
        => (Paragraph)table.GetCell(row, col)!.Blocks[0];

    private static DocumentPosition Cell(int block, int row, int col, int inline, int offset)
        => new(block, inline, offset, row, col);

    /// <summary>Lossless text description of the document, recursing into tables and hyperlinks.</summary>
    private static string Dump(EditorDocument doc) => string.Join(" | ", doc.Blocks.Select(Describe));

    private static string Describe(Block block) => block switch
    {
        Paragraph p => "P[" + string.Join(",", p.Inlines.Select(Describe)) + "]",
        TableBlock t => "T[" + string.Join(";", t.Rows.Select(r =>
            string.Join("/", r.Cells.Select(c => string.Join("+", c.Blocks.Select(b =>
                b is Paragraph p ? string.Join(",", p.Inlines.Select(Describe)) : Describe(b))))))) + "]",
        _ => block.GetType().Name,
    };

    private static string Describe(Inline inline) => inline switch
    {
        TextRun r => $"'{r.Text}'{(r.Style.IsBold ? "b" : "")}",
        ImageInline img => $"I({img.Source})",
        HyperlinkInline h => $"L({h.Url}:{string.Join(",", h.Children.Select(Describe))})",
        _ => inline.GetType().Name,
    };

    private static EditorContext Ctx(EditorDocument doc, DocumentPosition anchor, DocumentPosition active, UndoManager undo)
    {
        var sel = new Selection();
        sel.Set(anchor, active);
        return new EditorContext(doc, sel, undo, new FormattingEngine(), new FontFormattingEngine());
    }

    // ── 1. Deleting a selection across a table ────────────────────────────────

    [Fact]
    public void DeleteRange_AcrossTable_RemovesTableAndUndoRestoresItExactly()
    {
        var first = new Paragraph("Hello");
        var table = Table();
        var last = new Paragraph("World");
        var doc = new EditorDocument([first, table, last]);
        string before = Dump(doc);
        var undo = new UndoManager();

        undo.Push(new DeleteRangeAction(doc, new TextRange(new(0, 0, 2), new(2, 0, 3))));

        Assert.Equal("P['Held']", Dump(doc));

        undo.Undo();
        Assert.Equal(before, Dump(doc));
        Assert.Same(first, doc.Blocks[0]);
        Assert.Same(table, doc.Blocks[1]);
        Assert.Same(last, doc.Blocks[2]);
        Assert.All(doc.Blocks, b => Assert.Same(doc, b.Parent));

        undo.Redo();
        Assert.Equal("P['Held']", Dump(doc));

        undo.Undo();
        Assert.Equal(before, Dump(doc));
        Assert.Same(table, doc.Blocks[1]);
    }

    [Fact]
    public void DeleteRange_InsideTableCell_UndoRestoresCellText()
    {
        var table = Table();
        var doc = new EditorDocument([new Paragraph("x"), table]);
        string before = Dump(doc);
        var undo = new UndoManager();

        undo.Push(new DeleteRangeAction(doc, new TextRange(Cell(1, 0, 1, 0, 0), Cell(1, 0, 1, 0, 1))));
        Assert.Equal("1", CellParagraph(table, 0, 1).GetText());

        undo.Undo();
        Assert.Equal(before, Dump(doc));

        undo.Redo();
        Assert.Equal("1", CellParagraph(table, 0, 1).GetText());
    }

    // ── 2. Hyperlinks in table cells ──────────────────────────────────────────

    [Fact]
    public void InsertHyperlink_AtCaretInTableCell_InsertsLinkAndUndoes()
    {
        var table = Table();
        var doc = new EditorDocument([new Paragraph("x"), table]);
        string before = Dump(doc);
        var undo = new UndoManager();
        var caret = Cell(1, 1, 0, 0, 2);

        var pos = undo.Push(new InsertHyperlinkAction(doc, new TextRange(caret, caret), "https://a.b", "link"));

        Assert.Equal("P['x'] | T['A1'/'B1';'A2',L(https://a.b:'link')/'B2']", Dump(doc));
        Assert.Equal(Cell(1, 1, 0, 1, 4), pos);

        undo.Undo();
        Assert.Equal(before, Dump(doc));

        undo.Redo();
        Assert.Equal("P['x'] | T['A1'/'B1';'A2',L(https://a.b:'link')/'B2']", Dump(doc));
    }

    [Fact]
    public void InsertHyperlinkCommand_OverSelectionInTableCell_LinksSelectedText()
    {
        var table = Table();
        var doc = new EditorDocument([table]);
        string before = Dump(doc);
        var undo = new UndoManager();
        var ctx = Ctx(doc, Cell(0, 0, 1, 0, 0), Cell(0, 0, 1, 0, 1), undo);

        new InsertHyperlinkCommand { Url = "https://a.b" }.Execute(ctx);

        Assert.Equal("T['A1'/L(https://a.b:'B'),'1';'A2'/'B2']", Dump(doc));
        Assert.Equal(Cell(0, 0, 1, 0, 1), ctx.Selection.Active);

        undo.Undo();
        Assert.Equal(before, Dump(doc));
    }

    [Fact]
    public void InsertHyperlink_SelectionAcrossCells_LinksOnlyTheStartCell()
    {
        var table = Table();
        var doc = new EditorDocument([table]);
        string before = Dump(doc);
        var undo = new UndoManager();

        undo.Push(new InsertHyperlinkAction(doc, new TextRange(Cell(0, 0, 0, 0, 1), Cell(0, 1, 1, 0, 1)), "https://a.b"));

        Assert.Equal("T['A',L(https://a.b:'1')/'B1';'A2'/'B2']", Dump(doc));

        undo.Undo();
        Assert.Equal(before, Dump(doc));
    }

    [Fact]
    public void GetSelectedText_InTableCells_ReturnsCellText()
    {
        var doc = new EditorDocument([Table()]);

        Assert.Equal("1", DocumentEditor.GetSelectedText(doc, new TextRange(Cell(0, 0, 1, 0, 1), Cell(0, 0, 1, 0, 2))));
        Assert.Equal("1\nA2\nB", DocumentEditor.GetSelectedText(doc, new TextRange(Cell(0, 0, 1, 0, 1), Cell(0, 1, 1, 0, 1))));
    }

    /// <summary>A 1x1 table whose cell holds "A", a link showing "BD", "C", a line break and "E".</summary>
    private static TableBlock TableWithLink()
    {
        var table = new TableBlock(1, 1);
        var p = CellParagraph(table, 0, 0);
        p.AppendRun("A");
        var link = new HyperlinkInline("https://x.y");
        link.AddChild(new TextRun("BD"));
        p.AddInline(link);
        p.AppendRun("C");
        p.AddInline(new LineBreak());
        p.AppendRun("E");
        return table;
    }

    [Fact]
    public void GetSelectedText_InTableCell_IncludesHyperlinkTextAndLineBreaks()
    {
        var doc = new EditorDocument([TableWithLink()]);

        Assert.Equal("ABDC\nE", DocumentEditor.GetSelectedText(doc, new TextRange(Cell(0, 0, 0, 0, 0), Cell(0, 0, 0, 4, 1))));
        // Partly selected link: only its selected characters.
        Assert.Equal("DC\nE", DocumentEditor.GetSelectedText(doc, new TextRange(Cell(0, 0, 0, 1, 1), Cell(0, 0, 0, 4, 1))));
        Assert.Equal("B", DocumentEditor.GetSelectedText(doc, new TextRange(Cell(0, 0, 0, 1, 0), Cell(0, 0, 0, 1, 1))));
    }

    [Fact]
    public void GetSelectedText_TopLevelParagraph_IncludesHyperlinkText()
    {
        var p = new Paragraph("A");
        var link = new HyperlinkInline("https://x.y");
        link.AddChild(new TextRun("BD"));
        p.AddInline(link);
        p.AppendRun("C");
        var doc = new EditorDocument([p]);

        Assert.Equal("ABDC", DocumentEditor.GetSelectedText(doc, new TextRange(new(0, 0, 0), new(0, 2, 1))));
        Assert.Equal("DC", DocumentEditor.GetSelectedText(doc, new TextRange(new(0, 1, 1), new(0, 2, 1))));
    }

    [Fact]
    public void CutCommand_LinkedTextInTableCell_CopiesIt()
    {
        var doc = new EditorDocument([TableWithLink()]);
        var clipboard = new MockClipboardProvider();
        var sel = new Selection();
        sel.Set(Cell(0, 0, 0, 1, 0), Cell(0, 0, 0, 1, 2));
        var ctx = new EditorContext(doc, sel, new UndoManager(), new FormattingEngine(), new FontFormattingEngine(), clipboard);

        new CutCommand().Execute(ctx);

        Assert.Equal("BD", clipboard.Contents);
        Assert.Equal("AC\nE", ((TableBlock)doc.Blocks[0]).GetCell(0, 0)!.GetText());
    }

    [Fact]
    public void InsertHyperlink_OverLinkedTextInTableCell_KeepsText()
    {
        var doc = new EditorDocument([TableWithLink()]);

        var link = HyperlinkEngine.InsertHyperlink(doc, new TextRange(Cell(0, 0, 0, 1, 0), Cell(0, 0, 0, 1, 2)), "https://new.z");

        Assert.Equal("BD", link.GetText());
        Assert.Equal("ABDC\nE", ((TableBlock)doc.Blocks[0]).GetCell(0, 0)!.GetText());
    }

    // ── Merged cells: covered slots are hidden and not part of a selection ────

    /// <summary>The <see cref="Table"/> with its top row merged into one cell; (0,1) keeps its hidden "B1".</summary>
    private static TableBlock TableWithMergedTopRow()
    {
        var table = Table();
        TableEngine.MergeCells(table, 0, 0, 1, 2);
        return table;
    }

    [Fact]
    public void GetSelectedText_AcrossMergedCell_SkipsCoveredSlot()
    {
        var doc = new EditorDocument([TableWithMergedTopRow()]);

        Assert.Equal("1\nA", DocumentEditor.GetSelectedText(doc, new TextRange(Cell(0, 0, 0, 0, 1), Cell(0, 1, 0, 0, 1))));
    }

    [Fact]
    public void ApplyTextStyle_AcrossMergedCell_LeavesCoveredSlotUnchanged()
    {
        var table = TableWithMergedTopRow();
        var doc = new EditorDocument([table]);
        var range = new TextRange(Cell(0, 0, 0, 0, 0), Cell(0, 1, 0, 0, 2));

        DocumentEditor.ApplyTextStyle(doc, range, s => s.WithBold(true));

        Assert.Equal("T['A1'b/'B1';'A2'b/'B2']", Dump(doc));
        Assert.True(FormattingEngine.IsAppliedToEntireRange(doc, range, s => s.IsBold));
    }

    [Fact]
    public void FormattingState_AcrossMergedCell_IgnoresCoveredSlot()
    {
        var table = TableWithMergedTopRow();
        CellParagraph(table, 0, 0).Inlines.OfType<TextRun>().Single().Style = Bold;
        CellParagraph(table, 1, 0).Inlines.OfType<TextRun>().Single().Style = Bold;
        var doc = new EditorDocument([table]);

        // The hidden, non-bold "B1" must not make the visible all-bold selection look mixed.
        var state = FormattingState.Compute(doc, new TextRange(Cell(0, 0, 0, 0, 0), Cell(0, 1, 0, 0, 2)));

        Assert.True(state.IsBold);
    }

    // ── 3. Character formatting in table cells ────────────────────────────────

    [Fact]
    public void ToggleBold_InsideTableCell_FormatsSelectedText()
    {
        var table = Table();
        var doc = new EditorDocument([table]);
        string before = Dump(doc);
        var undo = new UndoManager();
        var ctx = Ctx(doc, Cell(0, 0, 0, 0, 1), Cell(0, 0, 0, 0, 2), undo);

        new ToggleBoldCommand().Execute(ctx);

        Assert.Equal("T['A','1'b/'B1';'A2'/'B2']", Dump(doc));
        // The selection stays on the same characters of the same cell.
        Assert.Equal((0, 0), (ctx.Selection.Range.Start.CellRow, ctx.Selection.Range.Start.CellCol));
        Assert.Equal(1, DocumentEditor.GetAbsoluteCharOffset(doc, ctx.Selection.Range.Start));
        Assert.Equal(2, DocumentEditor.GetAbsoluteCharOffset(doc, ctx.Selection.Range.End));

        // Toggling again turns bold off: the whole selection is already bold.
        new ToggleBoldCommand().Execute(ctx);
        Assert.Equal(before, Dump(doc));

        undo.Undo();
        Assert.Equal("T['A','1'b/'B1';'A2'/'B2']", Dump(doc));
        undo.Undo();
        Assert.Equal(before, Dump(doc));
        undo.Redo();
        Assert.Equal("T['A','1'b/'B1';'A2'/'B2']", Dump(doc));
    }

    [Fact]
    public void ToggleBold_AcrossTableCells_FormatsEveryCellInRange()
    {
        var table = Table();
        var doc = new EditorDocument([new Paragraph("x"), table]);
        string before = Dump(doc);
        var undo = new UndoManager();
        var ctx = Ctx(doc, Cell(1, 0, 1, 0, 1), Cell(1, 1, 0, 0, 1), undo);

        new ToggleBoldCommand().Execute(ctx);

        Assert.Equal("P['x'] | T['A1'/'B','1'b;'A'b,'2'/'B2']", Dump(doc));
        Assert.True(FormattingEngine.IsAppliedToEntireRange(doc, ctx.Selection.Range, s => s.IsBold));

        undo.Undo();
        Assert.Equal(before, Dump(doc));
        undo.Redo();
        Assert.Equal("P['x'] | T['A1'/'B','1'b;'A'b,'2'/'B2']", Dump(doc));
    }

    [Fact]
    public void ApplyTextStyle_FromParagraphIntoTable_FormatsParagraphAndCellsUpToEnd()
    {
        var table = Table();
        var doc = new EditorDocument([new Paragraph("xy"), table, new Paragraph("z")]);

        DocumentEditor.ApplyTextStyle(doc, new TextRange(new(0, 0, 1), Cell(1, 1, 0, 0, 1)), s => s.WithBold(true));

        Assert.Equal("P['x','y'b] | T['A1'b/'B1'b;'A'b,'2'/'B2'] | P['z']", Dump(doc));
    }

    [Fact]
    public void ApplyTextStyle_WholeTableBetweenParagraphs_FormatsAllCells()
    {
        var table = Table();
        var doc = new EditorDocument([new Paragraph("x"), table, new Paragraph("z")]);

        DocumentEditor.ApplyTextStyle(doc, new TextRange(new(0, 0, 0), new(2, 0, 1)), s => s.WithBold(true));

        Assert.Equal("P['x'b] | T['A1'b/'B1'b;'A2'b/'B2'b] | P['z'b]", Dump(doc));
    }

    [Fact]
    public void ApplyFontSize_InTableCell_IsReadBackByGetTextStyleForRange()
    {
        var table = Table();
        var doc = new EditorDocument([table]);
        var range = new TextRange(Cell(0, 1, 1, 0, 0), Cell(0, 1, 1, 0, 2));

        new FontFormattingEngine().ApplyFontSize(doc, range, 20);

        Assert.Equal(20, CellParagraph(table, 1, 1).Inlines.OfType<TextRun>().Single().Style.FontSize);
        Assert.Equal(20, FontFormattingEngine.GetTextStyleForRange(doc, range)?.FontSize);
        Assert.Equal(TextStyle.Default, CellParagraph(table, 1, 0).Inlines.OfType<TextRun>().Single().Style);
    }

    // ── 4. Inserting text before an image ─────────────────────────────────────

    [Fact]
    public void InsertText_AtStartOfImage_InsertsBeforeImage()
    {
        var p = new Paragraph();
        p.AddInline(new ImageInline("img.png"));
        var doc = new EditorDocument([p]);

        var pos = DocumentEditor.InsertText(doc, new DocumentPosition(0, 0, 0), "ab");

        Assert.Equal("P['ab',I(img.png)]", Dump(doc));
        Assert.Equal(new DocumentPosition(0, 0, 2), pos);
    }

    [Fact]
    public void InsertText_AtStartOfImageAfterRun_AppendsToPrecedingRun()
    {
        var p = new Paragraph("x");
        p.AddInline(new ImageInline("img.png"));
        p.AppendRun("y");
        var doc = new EditorDocument([p]);

        var pos = DocumentEditor.InsertText(doc, new DocumentPosition(0, 1, 0), "ab");

        Assert.Equal("P['xab',I(img.png),'y']", Dump(doc));
        Assert.Equal(new DocumentPosition(0, 0, 3), pos);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InsertTextAction_BeforeImageOrLineBreakAfterLink_RedoKeepsTextOutsideLink(bool lineBreak)
    {
        var p = new Paragraph();
        var link = new HyperlinkInline("https://x.y");
        link.AddChild(new TextRun("site"));
        p.AddInline(link);
        p.AddInline(lineBreak ? new LineBreak() : new ImageInline("img.png"));
        var doc = new EditorDocument([p]);
        string after = lineBreak ? "P[L(https://x.y:'site'),'X',LineBreak]" : "P[L(https://x.y:'site'),'X',I(img.png)]";
        var undo = new UndoManager();

        undo.Push(new InsertTextAction(doc, new DocumentPosition(0, 1, 0), "X"));
        Assert.Equal(after, Dump(doc));

        undo.Undo();
        undo.Redo();
        Assert.Equal(after, Dump(doc));
        undo.Undo();
        Assert.Equal(lineBreak ? "P[L(https://x.y:'site'),LineBreak]" : "P[L(https://x.y:'site'),I(img.png)]", Dump(doc));
    }

    [Fact]
    public void InsertTextAction_AtLinkEnd_RedoStillExtendsLink()
    {
        // A position inside the link (at its end) keeps inserting into the link on redo.
        var p = new Paragraph();
        var link = new HyperlinkInline("https://x.y");
        link.AddChild(new TextRun("site"));
        p.AddInline(link);
        p.AddInline(new ImageInline("img.png"));
        var doc = new EditorDocument([p]);
        var undo = new UndoManager();

        undo.Push(new InsertTextAction(doc, new DocumentPosition(0, 0, 4), "X"));
        undo.Undo();
        undo.Redo();

        Assert.Equal("P[L(https://x.y:'siteX'),I(img.png)]", Dump(doc));
    }

    [Fact]
    public void InsertText_AtEndOfImage_StillInsertsAfterImage()
    {
        var p = new Paragraph();
        p.AddInline(new ImageInline("img.png"));
        var doc = new EditorDocument([p]);

        DocumentEditor.InsertText(doc, new DocumentPosition(0, 0, 1), "ab");

        Assert.Equal("P[I(img.png),'ab']", Dump(doc));
    }

    [Fact]
    public void InsertTextAction_AtStartOfImage_UndoRemovesOnlyInsertedText()
    {
        var p = new Paragraph();
        p.AddInline(new ImageInline("img.png"));
        var doc = new EditorDocument([p]);
        var undo = new UndoManager();

        undo.Push(new InsertTextAction(doc, new DocumentPosition(0, 0, 0), "ab"));
        Assert.Equal("P['ab',I(img.png)]", Dump(doc));

        undo.Undo();
        Assert.Equal("P[I(img.png)]", Dump(doc));
        undo.Redo();
        Assert.Equal("P['ab',I(img.png)]", Dump(doc));
    }
}
