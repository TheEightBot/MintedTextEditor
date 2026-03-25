using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Layout;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Tests;

public class TextLayoutEngineTests
{
    private readonly TextLayoutEngine _engine = new();
    private readonly MockDrawingContext _ctx = new();

    // ── Single paragraph, no wrapping ────────────────────────────

    [Fact]
    public void Layout_SingleShortParagraph_ProducesSingleLine()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Hello");

        var layout = _engine.Layout(doc, 500f, _ctx);

        Assert.Single(layout.Blocks);
        Assert.Single(layout.Blocks[0].Lines);
        Assert.Single(layout.Blocks[0].Lines[0].Runs);
        Assert.Equal("Hello", layout.Blocks[0].Lines[0].Runs[0].Text);
    }

    [Fact]
    public void Layout_SingleParagraph_RunWidthMatchesMeasurement()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("ABCD");

        var layout = _engine.Layout(doc, 500f, _ctx);

        var run = layout.Blocks[0].Lines[0].Runs[0];
        // 4 chars * 8px = 32px
        Assert.Equal(32f, run.Width);
    }

    // ── Word wrapping ────────────────────────────────────────────

    [Fact]
    public void Layout_TextExceedsWidth_WrapsToMultipleLines()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        // "Hello World" = 11 chars. At 8px/char = 88px. With viewport 60px, should wrap.
        para.AppendRun("Hello World");

        var layout = _engine.Layout(doc, 60f, _ctx);

        var lines = layout.Blocks[0].Lines;
        Assert.True(lines.Count >= 2, $"Expected at least 2 lines, got {lines.Count}");
    }

    [Fact]
    public void Layout_WordWrap_PreservesAllText()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Hello World Foo");

        var layout = _engine.Layout(doc, 80f, _ctx);

        var allText = string.Concat(
            layout.Blocks[0].Lines.SelectMany(l => l.Runs.Select(r => r.Text)));
        Assert.Equal("Hello World Foo", allText);
    }

    // ── Character wrapping ───────────────────────────────────────

    [Fact]
    public void Layout_LongWordExceedsLine_CharacterWraps()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        // 20 chars * 8px = 160px, viewport 50px
        para.AppendRun("ABCDEFGHIJKLMNOPQRST");

        var layout = _engine.Layout(doc, 50f, _ctx);

        var lines = layout.Blocks[0].Lines;
        Assert.True(lines.Count >= 3, $"Expected at least 3 lines for char-wrap, got {lines.Count}");

        // All text preserved
        var allText = string.Concat(
            lines.SelectMany(l => l.Runs.Select(r => r.Text)));
        Assert.Equal("ABCDEFGHIJKLMNOPQRST", allText);
    }

    // ── Multi-paragraph ──────────────────────────────────────────

    [Fact]
    public void Layout_MultipleParagraphs_ProducesMultipleBlocks()
    {
        var para1 = new Paragraph();
        para1.AppendRun("First");
        var para2 = new Paragraph();
        para2.AppendRun("Second");
        var doc = new EditorDocument(new Block[] { para1, para2 });

        var layout = _engine.Layout(doc, 500f, _ctx);

        Assert.Equal(2, layout.Blocks.Count);
        Assert.Equal("First", layout.Blocks[0].Lines[0].Runs[0].Text);
        Assert.Equal("Second", layout.Blocks[1].Lines[0].Runs[0].Text);
    }

    [Fact]
    public void Layout_MultipleParagraphs_SecondBlockYFollowsFirst()
    {
        var para1 = new Paragraph();
        para1.AppendRun("First");
        var para2 = new Paragraph();
        para2.AppendRun("Second");
        var doc = new EditorDocument(new Block[] { para1, para2 });

        var layout = _engine.Layout(doc, 500f, _ctx);

        Assert.True(layout.Blocks[1].Y > 0, "Second block should have positive Y offset");
        Assert.Equal(layout.Blocks[0].TotalHeight, layout.Blocks[1].Y);
    }

    // ── Inline style boundaries ──────────────────────────────────

    [Fact]
    public void Layout_MultipleInlineStyles_ProducesMultipleRuns()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Hello ");
        para.AppendRun("World", TextStyle.Default.WithBold(true));

        var layout = _engine.Layout(doc, 500f, _ctx);

        var runs = layout.Blocks[0].Lines[0].Runs;
        Assert.True(runs.Count >= 2, $"Expected at least 2 runs, got {runs.Count}");
    }

    [Fact]
    public void Layout_MultipleInlineStyles_RunXPositionsAreContiguous()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("AB");
        para.AppendRun("CD", TextStyle.Default.WithBold(true));

        var layout = _engine.Layout(doc, 500f, _ctx);

        var runs = layout.Blocks[0].Lines[0].Runs;
        // First run starts at 0, second run starts at first run's end
        Assert.Equal(0f, runs[0].X);
        Assert.Equal(runs[0].X + runs[0].Width, runs[1].X);
    }

    // ── Line height and metrics ──────────────────────────────────

    [Fact]
    public void Layout_LineHeight_ReflectsFontMetrics()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Hello");

        var layout = _engine.Layout(doc, 500f, _ctx);

        var line = layout.Blocks[0].Lines[0];
        Assert.True(line.Height > 0, "Line height must be positive");
        Assert.True(line.Baseline > 0, "Baseline must be positive (offset from top of line)");
    }

    [Fact]
    public void Layout_Baseline_SmallerThanLineHeight()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Hello");

        var layout = _engine.Layout(doc, 500f, _ctx);

        var line = layout.Blocks[0].Lines[0];
        Assert.True(line.Baseline < line.Height, "Baseline should be less than line height");
    }

    // ── Empty document ───────────────────────────────────────────

    [Fact]
    public void Layout_EmptyDocument_HasOneBlockAndOneLine()
    {
        var doc = new EditorDocument();

        var layout = _engine.Layout(doc, 500f, _ctx);

        Assert.Single(layout.Blocks);
        Assert.Single(layout.Blocks[0].Lines);
        Assert.True(layout.TotalHeight > 0, "Empty document should still have height");
    }

    // ── Paragraph spacing ────────────────────────────────────────

    [Fact]
    public void Layout_ParagraphSpacing_AffectsBlockHeight()
    {
        var paraStyled = new Paragraph { Style = ParagraphStyle.Default.Clone() };
        paraStyled.Style.SpaceBefore = 10f;
        paraStyled.Style.SpaceAfter = 10f;
        paraStyled.AppendRun("Test");

        var paraPlain = new Paragraph();
        paraPlain.AppendRun("Test");

        var docStyled = new EditorDocument(new Block[] { paraStyled });
        var docPlain = new EditorDocument(new Block[] { paraPlain });

        var layoutStyled = _engine.Layout(docStyled, 500f, _ctx);
        var layoutPlain = _engine.Layout(docPlain, 500f, _ctx);

        Assert.True(layoutStyled.Blocks[0].TotalHeight > layoutPlain.Blocks[0].TotalHeight,
            "Paragraph with spacing should be taller");
    }

    // ── TotalHeight ──────────────────────────────────────────────

    [Fact]
    public void Layout_TotalHeight_SumsAllBlocks()
    {
        var para1 = new Paragraph();
        para1.AppendRun("First");
        var para2 = new Paragraph();
        para2.AppendRun("Second");
        var doc = new EditorDocument(new Block[] { para1, para2 });

        var layout = _engine.Layout(doc, 500f, _ctx);

        float expectedHeight = layout.Blocks.Sum(b => b.TotalHeight);
        Assert.Equal(expectedHeight, layout.TotalHeight);
    }

    // ── ViewportWidth is stored ──────────────────────────────────

    [Fact]
    public void Layout_ViewportWidth_StoredInResult()
    {
        var doc = new EditorDocument();

        var layout = _engine.Layout(doc, 750f, _ctx);

        Assert.Equal(750f, layout.ViewportWidth);
    }

    // ── LineBreak inline ─────────────────────────────────────────

    [Fact]
    public void Layout_LineBreakInline_ForcesNewLine()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AddInline(new TextRun("Before"));
        para.AddInline(new LineBreak());
        para.AddInline(new TextRun("After"));

        var layout = _engine.Layout(doc, 500f, _ctx);

        var lines = layout.Blocks[0].Lines;
        Assert.True(lines.Count >= 2, $"Expected at least 2 lines after LineBreak, got {lines.Count}");
    }

    // ── Source reference tracking ────────────────────────────────

    [Fact]
    public void Layout_LayoutRun_TracksSourceRun()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        var sourceRun = new TextRun("Hello");
        para.AddInline(sourceRun);

        var layout = _engine.Layout(doc, 500f, _ctx);

        var layoutRun = layout.Blocks[0].Lines[0].Runs[0];
        Assert.Same(sourceRun, layoutRun.SourceInline);
        Assert.Equal(0, layoutRun.SourceOffset);
    }

    [Fact]
    public void Layout_Table_UsesExplicitColumnWidths()
    {
        var table = new TableBlock(1, 3);
        table.ColumnWidths.Clear();
        table.ColumnWidths.Add(120f);
        table.ColumnWidths.Add(180f);
        table.ColumnWidths.Add(220f);

        var doc = new EditorDocument(new Block[] { table });
        var layout = _engine.Layout(doc, 640f, _ctx);

        var tableLayout = Assert.IsType<TableLayoutBlock>(layout.Blocks[0]);
        Assert.Equal(120f, tableLayout.ColumnWidths[0]);
        Assert.Equal(180f, tableLayout.ColumnWidths[1]);
        Assert.Equal(220f, tableLayout.ColumnWidths[2]);
    }

    [Fact]
    public void Layout_Table_HonorsExplicitRowHeight()
    {
        var table = new TableBlock(2, 1);
        table.Rows[0].Height = 96f;

        var doc = new EditorDocument(new Block[] { table });
        var layout = _engine.Layout(doc, 400f, _ctx);

        var tableLayout = Assert.IsType<TableLayoutBlock>(layout.Blocks[0]);
        Assert.True(tableLayout.RowHeights[0] >= 96f);
    }
}
