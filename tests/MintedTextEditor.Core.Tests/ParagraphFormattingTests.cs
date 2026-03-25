using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Layout;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Tests;

public class ParagraphFormattingTests
{
    private readonly TextLayoutEngine _engine = new();
    private readonly MockDrawingContext _ctx = new();

    private static TextRange WholeDoc(EditorDocument doc)
    {
        int last = doc.Blocks.Count - 1;
        var p = (Paragraph)doc.Blocks[last];
        int offset = p.GetText().Length;
        return new TextRange(
            new DocumentPosition(0, 0, 0),
            new DocumentPosition(last, p.Inlines.Count == 0 ? 0 : p.Inlines.Count - 1, offset));
    }

    private static TextRange SingleParagraph(int blockIndex, EditorDocument doc)
    {
        var p = (Paragraph)doc.Blocks[blockIndex];
        int offset = p.GetText().Length;
        return new TextRange(
            new DocumentPosition(blockIndex, 0, 0),
            new DocumentPosition(blockIndex, p.Inlines.Count == 0 ? 0 : p.Inlines.Count - 1, offset));
    }

    // ── Alignment ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TextAlignment.Left)]
    [InlineData(TextAlignment.Center)]
    [InlineData(TextAlignment.Right)]
    [InlineData(TextAlignment.Justify)]
    public void SetAlignment_UpdatesParagraphStyle(TextAlignment alignment)
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Hello");

        ParagraphFormattingEngine.SetAlignment(doc, SingleParagraph(0, doc), alignment);

        Assert.Equal(alignment, para.Style.Alignment);
    }

    // ── Bullet list ────────────────────────────────────────────────────────

    [Fact]
    public void ToggleBulletList_OffThenOn_SetsBullet()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Item");

        ParagraphFormattingEngine.ToggleBulletList(doc, SingleParagraph(0, doc));

        Assert.Equal(ListType.Bullet, para.Style.ListType);
    }

    [Fact]
    public void ToggleBulletList_WhenAllBullet_RemovesList()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Item");
        para.Style.ListType = ListType.Bullet;

        ParagraphFormattingEngine.ToggleBulletList(doc, SingleParagraph(0, doc));

        Assert.Equal(ListType.None, para.Style.ListType);
    }

    // ── Number list ────────────────────────────────────────────────────────

    [Fact]
    public void ToggleNumberList_OffThenOn_SetsNumber()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Item");

        ParagraphFormattingEngine.ToggleNumberList(doc, SingleParagraph(0, doc));

        Assert.Equal(ListType.Number, para.Style.ListType);
    }

    [Fact]
    public void ToggleNumberList_WhenAllNumber_RemovesList()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Item");
        para.Style.ListType = ListType.Number;

        ParagraphFormattingEngine.ToggleNumberList(doc, SingleParagraph(0, doc));

        Assert.Equal(ListType.None, para.Style.ListType);
    }

    // ── Numbered list counter in layout ────────────────────────────────────

    [Fact]
    public void NumberedList_ThreeParagraphs_SequentialListNumbers()
    {
        var doc = new EditorDocument();
        // SplitBlock appends a new paragraph; do it twice to get 3 paragraphs
        DocumentEditor.SplitBlock(doc, new DocumentPosition(0, 0, 0));
        DocumentEditor.SplitBlock(doc, new DocumentPosition(1, 0, 0));

        foreach (var block in doc.Blocks)
            ((Paragraph)block).Style.ListType = ListType.Number;

        var layout = _engine.Layout(doc, 500f, _ctx);

        Assert.Equal(1, layout.Blocks[0].ListNumber);
        Assert.Equal(2, layout.Blocks[1].ListNumber);
        Assert.Equal(3, layout.Blocks[2].ListNumber);
    }

    [Fact]
    public void NumberedList_ResetByNonListParagraph()
    {
        var doc = new EditorDocument();
        DocumentEditor.SplitBlock(doc, new DocumentPosition(0, 0, 0));
        DocumentEditor.SplitBlock(doc, new DocumentPosition(1, 0, 0));

        ((Paragraph)doc.Blocks[0]).Style.ListType = ListType.Number;
        // Block 1 is plain (no list)
        ((Paragraph)doc.Blocks[2]).Style.ListType = ListType.Number;

        var layout = _engine.Layout(doc, 500f, _ctx);

        Assert.Equal(1, layout.Blocks[0].ListNumber);
        Assert.Equal(0, layout.Blocks[1].ListNumber);   // not a list item
        Assert.Equal(1, layout.Blocks[2].ListNumber);   // counter restarted
    }

    // ── Indent ──────────────────────────────────────────────────────────────

    [Fact]
    public void IncreaseIndent_IncrementsIndentLevel()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Text");
        Assert.Equal(0, para.Style.IndentLevel);

        ParagraphFormattingEngine.IncreaseIndent(doc, SingleParagraph(0, doc));
        Assert.Equal(1, para.Style.IndentLevel);

        ParagraphFormattingEngine.IncreaseIndent(doc, SingleParagraph(0, doc));
        Assert.Equal(2, para.Style.IndentLevel);
    }

    [Fact]
    public void DecreaseIndent_DecrementsIndentLevel()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Text");
        para.Style.IndentLevel = 2;

        ParagraphFormattingEngine.DecreaseIndent(doc, SingleParagraph(0, doc));
        Assert.Equal(1, para.Style.IndentLevel);
    }

    [Fact]
    public void DecreaseIndent_AtZero_DoesNotGoNegative()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Text");
        Assert.Equal(0, para.Style.IndentLevel);

        ParagraphFormattingEngine.DecreaseIndent(doc, SingleParagraph(0, doc));
        Assert.Equal(0, para.Style.IndentLevel);
    }

    // ── Heading level ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public void SetHeadingLevel_SetsLevel(int level)
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Title");

        ParagraphFormattingEngine.SetHeadingLevel(doc, SingleParagraph(0, doc), level);

        Assert.Equal(level, para.Style.HeadingLevel);
    }

    [Fact]
    public void SetHeadingLevel_ClampsAboveSix()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Title");

        ParagraphFormattingEngine.SetHeadingLevel(doc, SingleParagraph(0, doc), 10);
        Assert.Equal(6, para.Style.HeadingLevel);
    }

    [Fact]
    public void SetHeadingLevel_ClampsBelowZero()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Title");

        ParagraphFormattingEngine.SetHeadingLevel(doc, SingleParagraph(0, doc), -1);
        Assert.Equal(0, para.Style.HeadingLevel);
    }

    // ── Heading layout font sizes ────────────────────────────────────────────

    [Theory]
    [InlineData(1, 2.0f)]
    [InlineData(2, 1.5f)]
    [InlineData(3, 1.17f)]
    [InlineData(6, 0.67f)]
    public void HeadingLevel_ScalesFontSizeInLayout(int headingLevel, float multiplier)
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        float baseSize = TextStyle.Default.FontSize;
        para.AppendRun("Heading");
        para.Style.HeadingLevel = headingLevel;

        var layout = _engine.Layout(doc, 500f, _ctx);

        // MockDrawingContext records measured style; verify by checking run style matches
        var run = layout.Blocks[0].Lines[0].Runs[0];
        // The layout run's Style should have the scaled font size
        float expected = baseSize * multiplier;
        Assert.Equal(expected, run.Style.FontSize, precision: 1);
    }

    // ── SetParagraphFormat ───────────────────────────────────────────────────

    [Fact]
    public void SetParagraphFormat_Normal_ClearsHeadingAndBlockQuote()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Text");
        para.Style.HeadingLevel = 2;
        para.Style.IsBlockQuote = true;

        ParagraphFormattingEngine.SetParagraphFormat(doc, SingleParagraph(0, doc), "Normal");

        Assert.Equal(0, para.Style.HeadingLevel);
        Assert.False(para.Style.IsBlockQuote);
    }

    [Theory]
    [InlineData("Heading1", 1)]
    [InlineData("Heading3", 3)]
    [InlineData("Heading6", 6)]
    public void SetParagraphFormat_Heading_SetsHeadingLevel(string format, int expectedLevel)
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Text");

        ParagraphFormattingEngine.SetParagraphFormat(doc, SingleParagraph(0, doc), format);

        Assert.Equal(expectedLevel, para.Style.HeadingLevel);
        Assert.False(para.Style.IsBlockQuote);
    }

    [Fact]
    public void SetParagraphFormat_Quote_SetsBlockQuote()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Quoted text");

        ParagraphFormattingEngine.SetParagraphFormat(doc, SingleParagraph(0, doc), "Quote");

        Assert.True(para.Style.IsBlockQuote);
        Assert.Equal(0, para.Style.HeadingLevel);
    }

    // ── Line spacing ─────────────────────────────────────────────────────────

    [Fact]
    public void SetLineSpacing_UpdatesStyle()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Text");

        ParagraphFormattingEngine.SetLineSpacing(doc, SingleParagraph(0, doc), 1.5f);

        Assert.Equal(1.5f, para.Style.LineSpacing);
    }

    // ── Layout: list indent offset ──────────────────────────────────────────

    [Fact]
    public void BulletList_RunStartsAfterGlyphIndent()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Item text");
        para.Style.ListType = ListType.Bullet;

        var layout = _engine.Layout(doc, 500f, _ctx);

        var run = layout.Blocks[0].Lines[0].Runs[0];
        // Text should start at IndentWidth (24f) offset from indent level 0
        Assert.Equal(24f, run.X);
    }

    // ── Layout: ParagraphStyle stored on LayoutBlock ────────────────────────

    [Fact]
    public void Layout_BlockHasParagraphStyle()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Text");
        para.Style.Alignment = TextAlignment.Center;

        var layout = _engine.Layout(doc, 500f, _ctx);

        Assert.NotNull(layout.Blocks[0].ParagraphStyle);
        Assert.Equal(TextAlignment.Center, layout.Blocks[0].ParagraphStyle!.Alignment);
    }
}
