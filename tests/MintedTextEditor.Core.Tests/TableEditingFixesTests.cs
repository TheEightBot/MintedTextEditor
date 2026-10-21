using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
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
