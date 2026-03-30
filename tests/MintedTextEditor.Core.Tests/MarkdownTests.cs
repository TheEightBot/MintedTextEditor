using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Markdown;

namespace MintedTextEditor.Core.Tests;

public class MarkdownTests
{
    // ──────────────────────────────────────────────────────────────────
    // Export tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Export_SimpleParagraph_ProducesMarkdown()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Hello world");

        var md = new MarkdownExporter().Export(doc);

        Assert.Equal("Hello world\n", md);
    }

    [Fact]
    public void Export_BoldText_ProducesDoubleAsterisks()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("bold", TextStyle.Default.WithBold(true));

        var md = new MarkdownExporter().Export(doc);

        Assert.Equal("**bold**\n", md);
    }

    [Fact]
    public void Export_ItalicText_ProducesSingleAsterisk()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("italic", TextStyle.Default.WithItalic(true));

        var md = new MarkdownExporter().Export(doc);

        Assert.Equal("*italic*\n", md);
    }

    [Fact]
    public void Export_BoldAndItalicText_ProducesTripleAsterisks()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("both", TextStyle.Default.WithBold(true).WithItalic(true));

        var md = new MarkdownExporter().Export(doc);

        Assert.Equal("***both***\n", md);
    }

    [Fact]
    public void Export_StrikethroughText_ProducesTildes()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("struck", TextStyle.Default.WithStrikethrough(true));

        var md = new MarkdownExporter().Export(doc);

        Assert.Equal("~~struck~~\n", md);
    }

    [Fact]
    public void Export_StrikethroughWithGfmDisabled_ProducesPlainText()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("struck", TextStyle.Default.WithStrikethrough(true));

        var md = new MarkdownExporter(new MarkdownExportOptions { UseGfmExtensions = false }).Export(doc);

        Assert.Equal("struck\n", md);
    }

    [Fact]
    public void Export_SubscriptText_ProducesSubHtmlTag()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("2", TextStyle.Default.WithSubscript(true));

        var md = new MarkdownExporter().Export(doc);

        Assert.Equal("<sub>2</sub>\n", md);
    }

    [Fact]
    public void Export_SuperscriptText_ProducesSupHtmlTag()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("2", TextStyle.Default.WithSuperscript(true));

        var md = new MarkdownExporter().Export(doc);

        Assert.Equal("<sup>2</sup>\n", md);
    }

    [Theory]
    [InlineData(1, "# ")]
    [InlineData(2, "## ")]
    [InlineData(3, "### ")]
    [InlineData(4, "#### ")]
    [InlineData(5, "##### ")]
    [InlineData(6, "###### ")]
    public void Export_Heading_ProducesHashPrefix(int level, string expectedPrefix)
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.Style.HeadingLevel = level;
        para.AppendRun("Title");

        var md = new MarkdownExporter().Export(doc);

        Assert.StartsWith(expectedPrefix, md);
        Assert.Contains("Title", md);
    }

    [Fact]
    public void Export_BulletList_ProducesDashPrefix()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.Style.ListType = ListType.Bullet;
        para.AppendRun("item one");
        var para2 = new Paragraph("item two");
        para2.Style.ListType = ListType.Bullet;
        doc.Blocks.Add(para2);

        var md = new MarkdownExporter().Export(doc);

        Assert.Equal("- item one\n- item two\n", md);
    }

    [Fact]
    public void Export_NumberedList_ProducesNumberedPrefix()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.Style.ListType = ListType.Number;
        para.AppendRun("first");
        var para2 = new Paragraph("second");
        para2.Style.ListType = ListType.Number;
        doc.Blocks.Add(para2);

        var md = new MarkdownExporter().Export(doc);

        Assert.Equal("1. first\n2. second\n", md);
    }

    [Fact]
    public void Export_Blockquote_ProducesGreaterThanPrefix()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.Style.IsBlockQuote = true;
        para.AppendRun("quoted text");

        var md = new MarkdownExporter().Export(doc);

        Assert.Equal("> quoted text\n", md);
    }

    [Fact]
    public void Export_Hyperlink_ProducesLinkSyntax()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        var link = new HyperlinkInline("https://example.com");
        link.AddChild(new TextRun("click here"));
        para.AddInline(link);

        var md = new MarkdownExporter().Export(doc);

        Assert.Equal("[click here](https://example.com)\n", md);
    }

    [Fact]
    public void Export_HyperlinkWithTitle_IncludesTitleInQuotes()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        var link = new HyperlinkInline("https://example.com", "Example");
        link.AddChild(new TextRun("link"));
        para.AddInline(link);

        var md = new MarkdownExporter().Export(doc);

        Assert.Equal("[link](https://example.com \"Example\")\n", md);
    }

    [Fact]
    public void Export_Image_ProducesImageSyntax()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AddInline(new ImageInline("img.png", "alt text"));

        var md = new MarkdownExporter().Export(doc);

        Assert.Equal("![alt text](img.png)\n", md);
    }

    [Fact]
    public void Export_LineBreak_ProducesTwoSpacesThenNewline()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("before");
        para.AddInline(new LineBreak());
        para.AppendRun("after");

        var md = new MarkdownExporter().Export(doc);

        Assert.Equal("before  \nafter\n", md);
    }

    [Fact]
    public void Export_SpecialChars_AreEscaped()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("**not bold**");

        var md = new MarkdownExporter().Export(doc);

        Assert.Equal(@"\*\*not bold\*\*" + "\n", md);
    }

    [Fact]
    public void Export_MultipleParagraphs_SeparatedByBlankLine()
    {
        var doc = new EditorDocument();
        var para1 = (Paragraph)doc.Blocks[0];
        para1.AppendRun("first");
        var para2 = new Paragraph("second");
        doc.Blocks.Add(para2);

        var md = new MarkdownExporter().Export(doc);

        Assert.Equal("first\n\nsecond\n", md);
    }

    [Fact]
    public void Export_GfmTable_ProducesTableSyntax()
    {
        var doc = new EditorDocument();
        doc.Blocks.Clear();

        var table = new TableBlock(2, 2) { Parent = doc };
        SetCellText(table, 0, 0, "Name");
        SetCellText(table, 0, 1, "Value");
        SetCellText(table, 1, 0, "Alice");
        SetCellText(table, 1, 1, "30");
        doc.Blocks.Add(table);

        var md = new MarkdownExporter().Export(doc);

        Assert.Contains("| Name | Value |", md);
        Assert.Contains("| --- |", md);
        Assert.Contains("| Alice | 30 |", md);
    }

    [Fact]
    public void Export_TableWithGfmDisabled_SkipsTable()
    {
        var doc = new EditorDocument();
        doc.Blocks.Clear();

        var table = new TableBlock(1, 2) { Parent = doc };
        SetCellText(table, 0, 0, "A");
        SetCellText(table, 0, 1, "B");
        doc.Blocks.Add(table);

        var md = new MarkdownExporter(new MarkdownExportOptions { UseGfmExtensions = false }).Export(doc);

        Assert.DoesNotContain("|", md);
    }

    // ──────────────────────────────────────────────────────────────────
    // Import tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Import_SimpleParagraph_CreatesSingleParagraph()
    {
        var doc = new MarkdownImporter().Import("Hello world\n");

        Assert.Single(doc.Blocks);
        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal("Hello world", para.GetText());
    }

    [Fact]
    public void Import_BoldDoubleAsterisks_CreatesBoldRun()
    {
        var doc = new MarkdownImporter().Import("**bold**\n");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.True(run.Style.IsBold);
        Assert.Equal("bold", run.Text);
    }

    [Fact]
    public void Import_BoldDoubleUnderscores_CreatesBoldRun()
    {
        var doc = new MarkdownImporter().Import("__bold__\n");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.True(run.Style.IsBold);
        Assert.Equal("bold", run.Text);
    }

    [Fact]
    public void Import_ItalicSingleAsterisk_CreatesItalicRun()
    {
        var doc = new MarkdownImporter().Import("*italic*\n");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.True(run.Style.IsItalic);
        Assert.Equal("italic", run.Text);
    }

    [Fact]
    public void Import_ItalicSingleUnderscore_CreatesItalicRun()
    {
        var doc = new MarkdownImporter().Import("_italic_\n");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.True(run.Style.IsItalic);
        Assert.Equal("italic", run.Text);
    }

    [Fact]
    public void Import_BoldItalicTripleAsterisks_CreatesBoldItalicRun()
    {
        var doc = new MarkdownImporter().Import("***bolditalic***\n");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.True(run.Style.IsBold);
        Assert.True(run.Style.IsItalic);
        Assert.Equal("bolditalic", run.Text);
    }

    [Fact]
    public void Import_Strikethrough_CreatesStrikethroughRun()
    {
        var doc = new MarkdownImporter().Import("~~struck~~\n");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.True(run.Style.IsStrikethrough);
        Assert.Equal("struck", run.Text);
    }

    [Fact]
    public void Import_SubTag_CreatesSubscriptRun()
    {
        var doc = new MarkdownImporter().Import("H<sub>2</sub>O\n");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal(3, para.Inlines.Count);
        var subRun = Assert.IsType<TextRun>(para.Inlines[1]);
        Assert.True(subRun.Style.IsSubscript);
        Assert.Equal("2", subRun.Text);
    }

    [Fact]
    public void Import_SupTag_CreatesSuperscriptRun()
    {
        var doc = new MarkdownImporter().Import("x<sup>2</sup>\n");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var supRun = Assert.IsType<TextRun>(para.Inlines[1]);
        Assert.True(supRun.Style.IsSuperscript);
        Assert.Equal("2", supRun.Text);
    }

    [Theory]
    [InlineData("# H1", 1)]
    [InlineData("## H2", 2)]
    [InlineData("### H3", 3)]
    [InlineData("#### H4", 4)]
    [InlineData("##### H5", 5)]
    [InlineData("###### H6", 6)]
    public void Import_Heading_SetsCorrectHeadingLevel(string input, int expectedLevel)
    {
        var doc = new MarkdownImporter().Import(input);

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal(expectedLevel, para.Style.HeadingLevel);
    }

    [Fact]
    public void Import_BulletList_CreatesBulletParagraphs()
    {
        var doc = new MarkdownImporter().Import("- item one\n- item two\n");

        Assert.Equal(2, doc.Blocks.Count);
        foreach (var block in doc.Blocks)
        {
            var para = Assert.IsType<Paragraph>(block);
            Assert.Equal(ListType.Bullet, para.Style.ListType);
        }
        Assert.Equal("item one", ((Paragraph)doc.Blocks[0]).GetText());
        Assert.Equal("item two", ((Paragraph)doc.Blocks[1]).GetText());
    }

    [Fact]
    public void Import_NumberedList_CreatesNumberedParagraphs()
    {
        var doc = new MarkdownImporter().Import("1. first\n2. second\n");

        Assert.Equal(2, doc.Blocks.Count);
        foreach (var block in doc.Blocks)
        {
            var para = Assert.IsType<Paragraph>(block);
            Assert.Equal(ListType.Number, para.Style.ListType);
        }
        Assert.Equal("first", ((Paragraph)doc.Blocks[0]).GetText());
    }

    [Fact]
    public void Import_Blockquote_SetsIsBlockQuote()
    {
        var doc = new MarkdownImporter().Import("> quoted\n");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.True(para.Style.IsBlockQuote);
        Assert.Equal("quoted", para.GetText());
    }

    [Fact]
    public void Import_HyperlinkWithoutTitle_CreatesHyperlinkInline()
    {
        var doc = new MarkdownImporter().Import("[click](https://example.com)\n");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var link = Assert.IsType<HyperlinkInline>(para.Inlines[0]);
        Assert.Equal("https://example.com", link.Url);
        Assert.Null(link.Title);
        Assert.Equal("click", link.GetText());
    }

    [Fact]
    public void Import_HyperlinkWithTitle_SetsTitleProperty()
    {
        var doc = new MarkdownImporter().Import("[link](https://example.com \"My Title\")\n");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var link = Assert.IsType<HyperlinkInline>(para.Inlines[0]);
        Assert.Equal("https://example.com", link.Url);
        Assert.Equal("My Title", link.Title);
    }

    [Fact]
    public void Import_Image_CreatesImageInline()
    {
        var doc = new MarkdownImporter().Import("![alt text](img.png)\n");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var img = Assert.IsType<ImageInline>(para.Inlines[0]);
        Assert.Equal("img.png", img.Source);
        Assert.Equal("alt text", img.AltText);
    }

    [Fact]
    public void Import_HardLineBreakTwoSpaces_CreatesLineBreak()
    {
        var doc = new MarkdownImporter().Import("Hello  \nWorld\n");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        // TextRun("Hello") + LineBreak + TextRun("World")
        Assert.Equal(3, para.Inlines.Count);
        Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.IsType<LineBreak>(para.Inlines[1]);
        Assert.IsType<TextRun>(para.Inlines[2]);
    }

    [Fact]
    public void Import_BrTag_CreatesLineBreak()
    {
        var doc = new MarkdownImporter().Import("before<br>after\n");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Contains(para.Inlines, i => i is LineBreak);
    }

    [Fact]
    public void Import_BrSelfClosingTag_CreatesLineBreak()
    {
        var doc = new MarkdownImporter().Import("before<br/>after\n");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Contains(para.Inlines, i => i is LineBreak);
    }

    [Fact]
    public void Import_EscapeSequence_ProducesLiteralChar()
    {
        var doc = new MarkdownImporter().Import("\\*not italic\\*\n");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal("*not italic*", para.GetText());
    }

    [Fact]
    public void Import_GfmTable_CreatesTableBlock()
    {
        var md = "| Name | Age |\n| --- | --- |\n| Alice | 30 |\n| Bob | 25 |\n";
        var doc = new MarkdownImporter().Import(md);

        var table = Assert.IsType<TableBlock>(doc.Blocks[0]);
        Assert.Equal(3, table.Rows.Count); // header + 2 data rows
        Assert.Equal(2, table.Rows[0].Cells.Count);
        Assert.Equal("Name", table.Rows[0].Cells[0].GetText().Trim());
        Assert.Equal("Alice", table.Rows[1].Cells[0].GetText().Trim());
        Assert.Equal("25", table.Rows[2].Cells[1].GetText().Trim());
    }

    [Fact]
    public void Import_EmptyString_ReturnsDocumentWithEmptyParagraph()
    {
        var doc = new MarkdownImporter().Import("");

        Assert.Single(doc.Blocks);
        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal(0, para.Length);
    }

    [Fact]
    public void Import_WhitespaceOnly_ReturnsDocumentWithEmptyParagraph()
    {
        var doc = new MarkdownImporter().Import("   \n\n  \n");

        Assert.Single(doc.Blocks);
    }

    [Fact]
    public void Import_MultipleParagraphsSeparatedByBlankLine_CreatesTwoBlocks()
    {
        var doc = new MarkdownImporter().Import("first\n\nsecond\n");

        Assert.Equal(2, doc.Blocks.Count);
        Assert.Equal("first", ((Paragraph)doc.Blocks[0]).GetText());
        Assert.Equal("second", ((Paragraph)doc.Blocks[1]).GetText());
    }

    [Fact]
    public void Import_HorizontalRule_IsSkipped()
    {
        var doc = new MarkdownImporter().Import("before\n\n---\n\nafter\n");

        Assert.Equal(2, doc.Blocks.Count);
        Assert.Equal("before", ((Paragraph)doc.Blocks[0]).GetText());
        Assert.Equal("after", ((Paragraph)doc.Blocks[1]).GetText());
    }

    // ──────────────────────────────────────────────────────────────────
    // Round-trip tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_Text_PreservesContent()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Hello world");

        var md = new MarkdownExporter().Export(doc);
        var doc2 = new MarkdownImporter().Import(md);

        var para2 = Assert.IsType<Paragraph>(doc2.Blocks[0]);
        Assert.Equal("Hello world", para2.GetText());
    }

    [Fact]
    public void RoundTrip_Heading_PreservesLevelAndText()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.Style.HeadingLevel = 2;
        para.AppendRun("Section Title");

        var md = new MarkdownExporter().Export(doc);
        var doc2 = new MarkdownImporter().Import(md);

        var para2 = Assert.IsType<Paragraph>(doc2.Blocks[0]);
        Assert.Equal(2, para2.Style.HeadingLevel);
        Assert.Equal("Section Title", para2.GetText());
    }

    [Fact]
    public void RoundTrip_BulletList_PreservesListType()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.Style.ListType = ListType.Bullet;
        para.AppendRun("item one");
        var para2 = new Paragraph("item two");
        para2.Style.ListType = ListType.Bullet;
        doc.Blocks.Add(para2);

        var md = new MarkdownExporter().Export(doc);
        var doc2 = new MarkdownImporter().Import(md);

        Assert.Equal(2, doc2.Blocks.Count);
        Assert.Equal(ListType.Bullet, ((Paragraph)doc2.Blocks[0]).Style.ListType);
        Assert.Equal(ListType.Bullet, ((Paragraph)doc2.Blocks[1]).Style.ListType);
        Assert.Equal("item one", ((Paragraph)doc2.Blocks[0]).GetText());
    }

    [Fact]
    public void RoundTrip_Bold_PreservesBoldStyle()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("plain ");
        para.AppendRun("bold", TextStyle.Default.WithBold(true));
        para.AppendRun(" end");

        var md = new MarkdownExporter().Export(doc);
        var doc2 = new MarkdownImporter().Import(md);

        var para2 = Assert.IsType<Paragraph>(doc2.Blocks[0]);
        Assert.Contains(para2.Inlines, i => i is TextRun r && r.Style.IsBold && r.Text == "bold");
    }

    [Fact]
    public void RoundTrip_Italic_PreservesItalicStyle()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("em", TextStyle.Default.WithItalic(true));

        var md = new MarkdownExporter().Export(doc);
        var doc2 = new MarkdownImporter().Import(md);

        var para2 = Assert.IsType<Paragraph>(doc2.Blocks[0]);
        var run = Assert.IsType<TextRun>(para2.Inlines[0]);
        Assert.True(run.Style.IsItalic);
        Assert.Equal("em", run.Text);
    }

    // ──────────────────────────────────────────────────────────────────
    // Extension method tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void GetMarkdown_ReturnsExportedString()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Hello");

        var md = doc.GetMarkdown();

        Assert.Equal("Hello\n", md);
    }

    [Fact]
    public void LoadMarkdown_ReturnsImportedDocument()
    {
        var doc = MarkdownDocumentExtensions.LoadMarkdown("# Title\n");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal(1, para.Style.HeadingLevel);
        Assert.Equal("Title", para.GetText());
    }

    [Fact]
    public void AppendMarkdown_AddsBlocksToDocument()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("existing");

        doc.AppendMarkdown("\n\nnew paragraph\n");

        Assert.Equal(2, doc.Blocks.Count);
        Assert.Equal("new paragraph", ((Paragraph)doc.Blocks[1]).GetText());
    }

    [Fact]
    public void AppendMarkdown_EmptyString_DoesNothing()
    {
        var doc = new EditorDocument();
        var initialCount = doc.Blocks.Count;

        doc.AppendMarkdown("");

        Assert.Equal(initialCount, doc.Blocks.Count);
    }

    [Fact]
    public void LoadMarkdown_FromStream_ReturnsImportedDocument()
    {
        var text = "Hello from stream\n";
        using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));

        var doc = MarkdownDocumentExtensions.LoadMarkdown(stream);

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal("Hello from stream", para.GetText());
    }

    // ──────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────

    private static void SetCellText(TableBlock table, int row, int col, string text)
    {
        var cell = table.Rows[row].Cells[col];
        var para = (Paragraph)cell.Blocks[0];
        para.AppendRun(text);
    }
}
