using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Html;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Tests;

public class HtmlTests
{
    // ──────────────────────────────────────────────────────────────────
    // Export tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Export_SimpleParagraph_ProducesHtml()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Hello world");

        var html = new HtmlExporter().Export(doc);

        Assert.Equal("<p>Hello world</p>", html);
    }

    [Fact]
    public void Export_BoldText_ProducesStrongTag()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("bold", TextStyle.Default.WithBold(true));

        var html = new HtmlExporter().Export(doc);

        Assert.Contains("<strong>", html);
        Assert.Contains("</strong>", html);
        Assert.Contains("bold", html);
    }

    [Fact]
    public void Export_ItalicText_ProducesEmTag()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("italic", TextStyle.Default.WithItalic(true));

        var html = new HtmlExporter().Export(doc);

        Assert.Contains("<em>", html);
        Assert.Contains("italic", html);
    }

    [Fact]
    public void Export_UnderlineText_ProducesUTag()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("underlined", TextStyle.Default.WithUnderline(true));

        var html = new HtmlExporter().Export(doc);

        Assert.Contains("<u>", html);
        Assert.Contains("underlined", html);
    }

    [Fact]
    public void Export_StrikethroughText_ProducesSTag()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("struck", TextStyle.Default.WithStrikethrough(true));

        var html = new HtmlExporter().Export(doc);

        Assert.Contains("<s>", html);
        Assert.Contains("struck", html);
    }

    [Fact]
    public void Export_Hyperlink_ProducesAnchorTag()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        var link = new HyperlinkInline("https://example.com");
        link.AddChild(new TextRun("click me"));
        para.AddInline(link);

        var html = new HtmlExporter().Export(doc);

        Assert.Contains("<a href=\"https://example.com\">", html);
        Assert.Contains("click me", html);
        Assert.Contains("</a>", html);
    }

    [Fact]
    public void Export_HyperlinkWithTitle_IncludesTitleAttribute()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        var link = new HyperlinkInline("https://example.com", "Example Site");
        link.AddChild(new TextRun("link"));
        para.AddInline(link);

        var html = new HtmlExporter().Export(doc);

        Assert.Contains("title=\"Example Site\"", html);
    }

    [Fact]
    public void Export_Image_ProducesImgTag()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AddInline(new ImageInline("photo.png", "A photo", 200, 100));

        var html = new HtmlExporter().Export(doc);

        Assert.Contains("<img ", html);
        Assert.Contains("src=\"photo.png\"", html);
        Assert.Contains("alt=\"A photo\"", html);
        Assert.Contains("width=\"200\"", html);
        Assert.Contains("height=\"100\"", html);
    }

    [Fact]
    public void Export_Heading1_ProducesH1Tag()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.Style.HeadingLevel = 1;
        para.AppendRun("Chapter One");

        var html = new HtmlExporter().Export(doc);

        Assert.Equal("<h1>Chapter One</h1>", html);
    }

    [Fact]
    public void Export_HeadingLevels_ProduceCorrectTags()
    {
        for (int level = 1; level <= 6; level++)
        {
            var doc = new EditorDocument();
            var para = (Paragraph)doc.Blocks[0];
            para.Style.HeadingLevel = level;
            para.AppendRun("text");

            var html = new HtmlExporter().Export(doc);

            Assert.Contains($"<h{level}>", html);
        }
    }

    [Fact]
    public void Export_BulletList_ProducesUlAndLi()
    {
        var doc = new EditorDocument();
        doc.Blocks.Clear();
        var p1 = new Paragraph("Item A");
        p1.Style.ListType = ListType.Bullet;
        var p2 = new Paragraph("Item B");
        p2.Style.ListType = ListType.Bullet;
        doc.Blocks.Add(p1);
        doc.Blocks.Add(p2);

        var html = new HtmlExporter().Export(doc);

        Assert.Contains("<ul>", html);
        Assert.Contains("</ul>", html);
        Assert.Contains("<li>Item A</li>", html);
        Assert.Contains("<li>Item B</li>", html);
    }

    [Fact]
    public void Export_NumberedList_ProducesOlAndLi()
    {
        var doc = new EditorDocument();
        doc.Blocks.Clear();
        var p1 = new Paragraph("First");
        p1.Style.ListType = ListType.Number;
        var p2 = new Paragraph("Second");
        p2.Style.ListType = ListType.Number;
        doc.Blocks.Add(p1);
        doc.Blocks.Add(p2);

        var html = new HtmlExporter().Export(doc);

        Assert.Contains("<ol>", html);
        Assert.Contains("</ol>", html);
        Assert.Contains("<li>First</li>", html);
        Assert.Contains("<li>Second</li>", html);
    }

    [Fact]
    public void Export_Blockquote_ProducesBlockquoteTag()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.Style.IsBlockQuote = true;
        para.AppendRun("A wise saying");

        var html = new HtmlExporter().Export(doc);

        Assert.Contains("<blockquote>", html);
        Assert.Contains("A wise saying", html);
        Assert.Contains("</blockquote>", html);
    }

    [Fact]
    public void Export_WithDocumentWrapper_IncludesDoctypeAndHtmlTags()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("wrapped");

        var options = new HtmlExportOptions { IncludeDocumentWrapper = true };
        var html = new HtmlExporter(options).Export(doc);

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<html>", html);
        Assert.Contains("<body>", html);
        Assert.Contains("</body>", html);
        Assert.Contains("</html>", html);
    }

    [Fact]
    public void Export_CustomFontSize_ProducesSpanWithFontSizeStyle()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("large", TextStyle.Default.WithFontSize(20f));

        var html = new HtmlExporter().Export(doc);

        Assert.Contains("font-size:20pt", html);
        Assert.Contains("large", html);
    }

    [Fact]
    public void Export_TextColor_ProducesSpanWithColorStyle()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("red text", TextStyle.Default.WithTextColor(EditorColor.Red));

        var html = new HtmlExporter().Export(doc);

        Assert.Contains("color:", html);
        Assert.Contains("red text", html);
    }

    [Fact]
    public void Export_CenterAlignment_ProducesTextAlignStyle()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.Style.Alignment = TextAlignment.Center;
        para.AppendRun("centered");

        var html = new HtmlExporter().Export(doc);

        Assert.Contains("text-align:center", html);
        Assert.Contains("centered", html);
    }

    [Fact]
    public void Export_HtmlSpecialChars_AreEncoded()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("a < b & c > d");

        var html = new HtmlExporter().Export(doc);

        Assert.Contains("&lt;", html);
        Assert.Contains("&amp;", html);
        Assert.Contains("&gt;", html);
    }

    [Fact]
    public void Export_MultipleConsecutiveParagraphs_EmitsSeparateTags()
    {
        var doc = new EditorDocument();
        doc.Blocks.Clear();
        doc.Blocks.Add(new Paragraph("First"));
        doc.Blocks.Add(new Paragraph("Second"));

        var html = new HtmlExporter().Export(doc);

        Assert.Equal("<p>First</p><p>Second</p>", html);
    }

    // ──────────────────────────────────────────────────────────────────
    // Import tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Import_SimpleParagraph_CreatesTextRun()
    {
        var doc = new HtmlImporter().Import("<p>Hello world</p>");

        Assert.Single(doc.Blocks);
        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Single(para.Inlines);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.Equal("Hello world", run.Text);
    }

    [Fact]
    public void Import_BoldText_SetsBoldStyle()
    {
        var doc = new HtmlImporter().Import("<p><strong>bold</strong></p>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.True(run.Style.IsBold);
        Assert.Equal("bold", run.Text);
    }

    [Fact]
    public void Import_BTagAlsoBold()
    {
        var doc = new HtmlImporter().Import("<p><b>also bold</b></p>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.True(run.Style.IsBold);
    }

    [Fact]
    public void Import_ItalicText_SetsItalicStyle()
    {
        var doc = new HtmlImporter().Import("<p><em>italic</em></p>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.True(run.Style.IsItalic);
    }

    [Fact]
    public void Import_Underline_SetsUnderlineStyle()
    {
        var doc = new HtmlImporter().Import("<p><u>underlined</u></p>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.True(run.Style.IsUnderline);
    }

    [Fact]
    public void Import_Strikethrough_SetsStrikethroughStyle()
    {
        var doc = new HtmlImporter().Import("<p><s>struck</s></p>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.True(run.Style.IsStrikethrough);
    }

    [Fact]
    public void Import_Subscript_SetsSubscriptStyle()
    {
        var doc = new HtmlImporter().Import("<p><sub>sub</sub></p>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.True(run.Style.IsSubscript);
    }

    [Fact]
    public void Import_Superscript_SetsSuperscriptStyle()
    {
        var doc = new HtmlImporter().Import("<p><sup>sup</sup></p>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.True(run.Style.IsSuperscript);
    }

    [Fact]
    public void Import_NestedBoldItalic_CombinesStyles()
    {
        var doc = new HtmlImporter().Import("<p><strong><em>bold italic</em></strong></p>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.True(run.Style.IsBold);
        Assert.True(run.Style.IsItalic);
        Assert.Equal("bold italic", run.Text);
    }

    [Fact]
    public void Import_Hyperlink_CreatesHyperlinkInline()
    {
        var doc = new HtmlImporter().Import("<p><a href=\"https://example.com\">link text</a></p>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var link = Assert.IsType<HyperlinkInline>(para.Inlines[0]);
        Assert.Equal("https://example.com", link.Url);
        Assert.Single(link.Children);
        var child = Assert.IsType<TextRun>(link.Children[0]);
        Assert.Equal("link text", child.Text);
    }

    [Fact]
    public void Import_HyperlinkWithTitle_SetsTitle()
    {
        var doc = new HtmlImporter().Import("<p><a href=\"u\" title=\"My Title\">x</a></p>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var link = Assert.IsType<HyperlinkInline>(para.Inlines[0]);
        Assert.Equal("My Title", link.Title);
    }

    [Fact]
    public void Import_Image_CreatesImageInline()
    {
        var doc = new HtmlImporter().Import(
            "<p><img src=\"photo.png\" alt=\"A photo\" width=\"200\" height=\"100\" /></p>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var img = Assert.IsType<ImageInline>(para.Inlines[0]);
        Assert.Equal("photo.png", img.Source);
        Assert.Equal("A photo", img.AltText);
        Assert.Equal(200f, img.Width);
        Assert.Equal(100f, img.Height);
    }

    [Fact]
    public void Import_LineBreak_AddsLineBreakInline()
    {
        var doc = new HtmlImporter().Import("<p>line1<br />line2</p>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal(3, para.Inlines.Count);
        Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.IsType<LineBreak>(para.Inlines[1]);
        Assert.IsType<TextRun>(para.Inlines[2]);
    }

    [Fact]
    public void Import_Heading_SetsHeadingLevel()
    {
        var doc = new HtmlImporter().Import("<h2>My Title</h2>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal(2, para.Style.HeadingLevel);
        Assert.Equal("My Title", para.GetText());
    }

    [Fact]
    public void Import_AllHeadingLevels_SetCorrectLevel()
    {
        for (int level = 1; level <= 6; level++)
        {
            var doc = new HtmlImporter().Import($"<h{level}>text</h{level}>");
            var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
            Assert.Equal(level, para.Style.HeadingLevel);
        }
    }

    [Fact]
    public void Import_BulletList_SetsListTypeBullet()
    {
        var doc = new HtmlImporter().Import("<ul><li>Item 1</li><li>Item 2</li></ul>");

        Assert.Equal(2, doc.Blocks.Count);
        var p1 = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var p2 = Assert.IsType<Paragraph>(doc.Blocks[1]);
        Assert.Equal(ListType.Bullet, p1.Style.ListType);
        Assert.Equal(ListType.Bullet, p2.Style.ListType);
        Assert.Equal("Item 1", p1.GetText());
        Assert.Equal("Item 2", p2.GetText());
    }

    [Fact]
    public void Import_NumberedList_SetsListTypeNumber()
    {
        var doc = new HtmlImporter().Import("<ol><li>First</li><li>Second</li></ol>");

        Assert.Equal(2, doc.Blocks.Count);
        var p1 = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal(ListType.Number, p1.Style.ListType);
    }

    [Fact]
    public void Import_Blockquote_SetsIsBlockQuote()
    {
        var doc = new HtmlImporter().Import("<blockquote><p>Quote text</p></blockquote>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.True(para.Style.IsBlockQuote);
        Assert.Equal("Quote text", para.GetText());
    }

    [Fact]
    public void Import_SpanWithCssColor_MapsToTextColor()
    {
        var doc = new HtmlImporter().Import(
            "<p><span style=\"color:#FF0000\">red text</span></p>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.Equal(EditorColor.Red, run.Style.TextColor);
        Assert.Equal("red text", run.Text);
    }

    [Fact]
    public void Import_SpanWithCssNamedColor_MapsToTextColor()
    {
        var doc = new HtmlImporter().Import(
            "<p><span style=\"color:blue\">blue text</span></p>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.Equal(EditorColor.Blue, run.Style.TextColor);
    }

    [Fact]
    public void Import_SpanWithCssFontSize_MapsToFontSize()
    {
        var doc = new HtmlImporter().Import(
            "<p><span style=\"font-size:18pt\">big text</span></p>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.Equal(18f, run.Style.FontSize);
    }

    [Fact]
    public void Import_SpanWithCssFontFamily_MapsToFontFamily()
    {
        var doc = new HtmlImporter().Import(
            "<p><span style=\"font-family:Arial\">Arial text</span></p>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.Equal("Arial", run.Style.FontFamily);
    }

    [Fact]
    public void Import_ParagraphWithTextAlignCenter_SetsCenterAlignment()
    {
        var doc = new HtmlImporter().Import(
            "<p style=\"text-align:center\">centered</p>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal(TextAlignment.Center, para.Style.Alignment);
    }

    [Fact]
    public void Import_UnknownTag_PreservesTextContent()
    {
        var doc = new HtmlImporter().Import("<figure><p>inside figure</p></figure>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal("inside figure", para.GetText());
    }

    [Fact]
    public void Import_HtmlEntities_AreDecoded()
    {
        var doc = new HtmlImporter().Import("<p>a &amp; b &lt;c&gt;</p>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal("a & b <c>", para.GetText());
    }

    [Fact]
    public void Import_NumericHtmlEntity_IsDecoded()
    {
        var doc = new HtmlImporter().Import("<p>&#65;</p>"); // 'A'

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal("A", para.GetText());
    }

    [Fact]
    public void Import_HexHtmlEntity_IsDecoded()
    {
        var doc = new HtmlImporter().Import("<p>&#x41;</p>"); // 'A'

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal("A", para.GetText());
    }

    [Fact]
    public void Import_Table_CreatesTableBlock()
    {
        var doc = new HtmlImporter().Import(
            "<table><tr><td>A</td><td>B</td></tr></table>");

        Assert.Single(doc.Blocks);
        var table = Assert.IsType<TableBlock>(doc.Blocks[0]);
        Assert.Single(table.Rows);
        Assert.Equal(2, table.Rows[0].Cells.Count);
        Assert.Equal("A", table.Rows[0].Cells[0].GetText());
        Assert.Equal("B", table.Rows[0].Cells[1].GetText());
    }

    [Fact]
    public void Import_TableMultipleRows_ParsesAllRows()
    {
        var doc = new HtmlImporter().Import(
            "<table><tr><td>R1C1</td></tr><tr><td>R2C1</td></tr></table>");

        var table = Assert.IsType<TableBlock>(doc.Blocks[0]);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("R1C1", table.Rows[0].Cells[0].GetText());
        Assert.Equal("R2C1", table.Rows[1].Cells[0].GetText());
    }

    [Fact]
    public void Import_EmptyDocument_ReturnsDocumentWithOneParagraph()
    {
        var doc = new HtmlImporter().Import("");

        Assert.Single(doc.Blocks);
        Assert.IsType<Paragraph>(doc.Blocks[0]);
    }

    [Fact]
    public void Import_HtmlWithHeadSection_IgnoresHeadContent()
    {
        var html = "<!DOCTYPE html><html><head><title>Page</title></head><body><p>visible</p></body></html>";
        var doc = new HtmlImporter().Import(html);

        Assert.Single(doc.Blocks);
        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal("visible", para.GetText());
    }

    [Fact]
    public void Import_HtmlComment_IsIgnored()
    {
        var doc = new HtmlImporter().Import("<p>before<!-- comment -->after</p>");

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal("beforeafter", para.GetText());
    }

    // ──────────────────────────────────────────────────────────────────
    // Round-trip tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_SimpleParagraph_PreservesText()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Round trip text");

        var html = new HtmlExporter().Export(doc);
        var imported = new HtmlImporter().Import(html);

        Assert.Single(imported.Blocks);
        var importedPara = Assert.IsType<Paragraph>(imported.Blocks[0]);
        Assert.Equal("Round trip text", importedPara.GetText());
    }

    [Fact]
    public void RoundTrip_BoldText_PreservesBoldStyle()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("bold content", TextStyle.Default.WithBold(true));

        var html = new HtmlExporter().Export(doc);
        var imported = new HtmlImporter().Import(html);

        var importedPara = Assert.IsType<Paragraph>(imported.Blocks[0]);
        var run = Assert.IsType<TextRun>(importedPara.Inlines[0]);
        Assert.True(run.Style.IsBold);
        Assert.Equal("bold content", run.Text);
    }

    [Fact]
    public void RoundTrip_Heading_PreservesLevelAndText()
    {
        var doc = new EditorDocument();
        doc.Blocks.Clear();
        var para = new Paragraph("Section Title");
        para.Style.HeadingLevel = 3;
        doc.Blocks.Add(para);

        var html = new HtmlExporter().Export(doc);
        var imported = new HtmlImporter().Import(html);

        var importedPara = Assert.IsType<Paragraph>(imported.Blocks[0]);
        Assert.Equal(3, importedPara.Style.HeadingLevel);
        Assert.Equal("Section Title", importedPara.GetText());
    }

    [Fact]
    public void RoundTrip_BulletList_PreservesListTypeAndText()
    {
        var doc = new EditorDocument();
        doc.Blocks.Clear();
        for (int i = 1; i <= 3; i++)
        {
            var p = new Paragraph($"Item {i}");
            p.Style.ListType = ListType.Bullet;
            doc.Blocks.Add(p);
        }

        var html = new HtmlExporter().Export(doc);
        var imported = new HtmlImporter().Import(html);

        Assert.Equal(3, imported.Blocks.Count);
        for (int i = 0; i < 3; i++)
        {
            var ip = Assert.IsType<Paragraph>(imported.Blocks[i]);
            Assert.Equal(ListType.Bullet, ip.Style.ListType);
            Assert.Equal($"Item {i + 1}", ip.GetText());
        }
    }

    [Fact]
    public void RoundTrip_MultipleFormattingStyles_PreservesAll()
    {
        var baseDoc = new EditorDocument();
        var para = (Paragraph)baseDoc.Blocks[0];
        para.AppendRun("styled", TextStyle.Default
            .WithBold(true)
            .WithItalic(true)
            .WithUnderline(true));

        var html = new HtmlExporter().Export(baseDoc);
        var imported = new HtmlImporter().Import(html);

        var importedPara = Assert.IsType<Paragraph>(imported.Blocks[0]);
        var run = Assert.IsType<TextRun>(importedPara.Inlines[0]);
        Assert.True(run.Style.IsBold);
        Assert.True(run.Style.IsItalic);
        Assert.True(run.Style.IsUnderline);
    }

    [Fact]
    public void RoundTrip_ColorsWithAlpha_PreservesTextAndHighlightColor()
    {
        var text = new EditorColor(10, 20, 30, 128);
        var highlight = new EditorColor(255, 255, 0, 64);
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("translucent", TextStyle.Default.WithTextColor(text).WithHighlightColor(highlight));
        para.AppendRun(" clear", TextStyle.Default.WithHighlightColor(EditorColor.FromArgb(0, 1, 2, 3)));

        var html = new HtmlExporter().Export(doc);
        Assert.Contains("rgba(", html);
        var imported = new HtmlImporter().Import(html);

        var importedPara = Assert.IsType<Paragraph>(imported.Blocks[0]);
        var run = Assert.IsType<TextRun>(importedPara.Inlines[0]);
        Assert.Equal(text, run.Style.TextColor);
        Assert.Equal(highlight, run.Style.HighlightColor);
        var run2 = Assert.IsType<TextRun>(importedPara.Inlines[1]);
        Assert.Equal(EditorColor.FromArgb(0, 1, 2, 3), run2.Style.HighlightColor);
    }

    [Fact]
    public void RoundTrip_EveryAlphaValue_IsExact()
    {
        for (int a = 0; a < 255; a++)
        {
            var color = new EditorColor(1, 2, 3, (byte)a);
            var doc = new EditorDocument();
            ((Paragraph)doc.Blocks[0]).AppendRun("x", TextStyle.Default.WithTextColor(color));

            var imported = new HtmlImporter().Import(new HtmlExporter().Export(doc));

            var run = Assert.IsType<TextRun>(((Paragraph)imported.Blocks[0]).Inlines[0]);
            Assert.Equal(color, run.Style.TextColor);
        }
    }

    [Fact]
    public void Export_AlphaColor_UsesInvariantCulture()
    {
        var saved = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var doc = new EditorDocument();
            ((Paragraph)doc.Blocks[0]).AppendRun("x",
                TextStyle.Default.WithTextColor(new EditorColor(1, 2, 3, 128)));

            var html = new HtmlExporter().Export(doc);

            Assert.Contains("rgba(1,2,3,0.502)", html);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void RoundTrip_TableSpans_PreservesGridAndSpans()
    {
        // 3x3 grid: A spans 2 columns in row 0, D spans 2 rows in column 0.
        var doc = new EditorDocument();
        doc.Blocks.Clear();
        var table = new TableBlock(3, 3);
        doc.Blocks.Add(table);
        string[,] labels = { { "A", "", "B" }, { "D", "E", "F" }, { "", "H", "I" } };
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                if (labels[r, c].Length > 0)
                    ((Paragraph)table.Rows[r].Cells[c].Blocks[0]).AppendRun(labels[r, c]);
        Formatting.TableEngine.MergeCells(table, 0, 0, rowSpan: 1, colSpan: 2);
        Formatting.TableEngine.MergeCells(table, 1, 0, rowSpan: 2, colSpan: 1);

        var html = new HtmlExporter().Export(doc);
        Assert.Contains("colspan=\"2\"", html);
        Assert.Contains("rowspan=\"2\"", html);
        var imported = Assert.IsType<TableBlock>(Assert.Single(new HtmlImporter().Import(html).Blocks));

        Assert.Equal(3, imported.RowCount);
        for (int r = 0; r < 3; r++)
        {
            Assert.Equal(3, imported.Rows[r].Cells.Count);
            for (int c = 0; c < 3; c++)
            {
                var expected = table.Rows[r].Cells[c];
                var actual = imported.Rows[r].Cells[c];
                Assert.Equal(expected.IsMerged, actual.IsMerged);
                Assert.Equal(expected.ColumnSpan, actual.ColumnSpan);
                Assert.Equal(expected.RowSpan, actual.RowSpan);
                Assert.Same(imported.Rows[r], actual.Parent);
                if (!expected.IsMerged)
                    Assert.Equal(labels[r, c], actual.GetText());
            }
        }

        // Exporting the imported table gives the same HTML.
        Assert.Equal(html, new HtmlExporter().Export(new HtmlImporter().Import(html)));
    }

    [Fact]
    public void Import_RowspanInMiddleColumn_PlacesFollowingCellsAfterIt()
    {
        var doc = new HtmlImporter().Import(
            "<table><tr><td>A</td><td rowspan=\"2\">B</td><td>C</td></tr>" +
            "<tr><td>D</td><td>F</td></tr></table>");

        var table = Assert.IsType<TableBlock>(doc.Blocks[0]);
        var row1 = table.Rows[1].Cells;
        Assert.Equal(3, row1.Count);
        Assert.Equal("D", row1[0].GetText());
        Assert.True(row1[1].IsMerged);
        Assert.Equal("F", row1[2].GetText());
        Assert.Equal(2, table.Rows[0].Cells[1].RowSpan);
    }

    [Fact]
    public void Import_RowspanInLastColumn_FillsCoveredSlotAtRowEnd()
    {
        var doc = new HtmlImporter().Import(
            "<table><tr><td>A</td><td rowspan=\"2\">B</td></tr><tr><td>C</td></tr></table>");

        var table = Assert.IsType<TableBlock>(doc.Blocks[0]);
        Assert.Equal(2, table.Rows[1].Cells.Count);
        Assert.True(table.Rows[1].Cells[1].IsMerged);
    }

    [Theory]
    [InlineData("5")]
    [InlineData("0")]
    public void Import_RowspanPastLastRow_IsClampedToTable(string rowspan)
    {
        var doc = new HtmlImporter().Import(
            $"<table><tr><td rowspan=\"{rowspan}\">A</td><td>B</td></tr><tr><td>C</td></tr></table>");

        var table = Assert.IsType<TableBlock>(doc.Blocks[0]);
        Assert.Equal(2, table.Rows[0].Cells[0].RowSpan);
        Assert.True(table.Rows[1].Cells[0].IsMerged);
        Assert.Equal("C", table.Rows[1].Cells[1].GetText());
    }

    [Fact]
    public void Import_UnclosedTable_StillClampsRowspan()
    {
        var doc = new HtmlImporter().Import("<table><tr><td rowspan=\"0\">A</td><td>B</td></tr><tr><td>C");

        var table = Assert.IsType<TableBlock>(doc.Blocks[0]);
        Assert.Equal(2, table.Rows[0].Cells[0].RowSpan);
        Assert.Equal(2, table.Rows[1].Cells.Count);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-3")]
    [InlineData("")]
    public void Import_InvalidColspan_TreatedAsOne(string colspan)
    {
        var doc = new HtmlImporter().Import($"<table><tr><td colspan=\"{colspan}\">A</td><td>B</td></tr></table>");

        var table = Assert.IsType<TableBlock>(doc.Blocks[0]);
        Assert.Equal(2, table.Rows[0].Cells.Count);
        Assert.Equal(1, table.Rows[0].Cells[0].ColumnSpan);
        Assert.False(table.Rows[0].Cells[1].IsMerged);
    }

    [Fact]
    public void Import_NestedTable_DoesNotEndOuterTable()
    {
        var doc = new HtmlImporter().Import(
            "<table><tr><td><table><tr><td>inner</td></tr></table></td><td>B</td></tr>" +
            "<tr><td>C</td><td>D</td></tr></table>");

        var outer = Assert.IsType<TableBlock>(Assert.Single(doc.Blocks));
        Assert.Equal(2, outer.RowCount);
        Assert.Equal(2, outer.Rows[0].Cells.Count);
        Assert.IsType<TableBlock>(outer.Rows[0].Cells[0].Blocks[0]);
        Assert.Equal("B", outer.Rows[0].Cells[1].GetText());
        Assert.Equal("D", outer.Rows[1].Cells[1].GetText());
    }

    // ──────────────────────────────────────────────────────────────────
    // CSS colour parsing
    // ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("#f00", 255, 0, 0, 255)]
    [InlineData("#F008", 255, 0, 0, 136)]
    [InlineData("#0a141e", 10, 20, 30, 255)]
    [InlineData("#0A141E80", 10, 20, 30, 128)]
    [InlineData("rgb(10, 20, 30)", 10, 20, 30, 255)]
    [InlineData("RGB(10,20,30)", 10, 20, 30, 255)]
    [InlineData("rgba(10, 20, 30, 0.5)", 10, 20, 30, 128)]
    [InlineData("rgba(10,20,30,0.502)", 10, 20, 30, 128)]
    [InlineData("rgba(10, 20, 30, 25%)", 10, 20, 30, 64)]
    [InlineData("rgba(10, 20, 30, .25)", 10, 20, 30, 64)]
    [InlineData("rgba(10, 20, 30, 0)", 10, 20, 30, 0)]
    [InlineData("rgba(10, 20, 30, 1)", 10, 20, 30, 255)]
    [InlineData("rgba(10, 20, 30, 2)", 10, 20, 30, 255)]
    [InlineData("rgb(10, 20, 30, 0.5)", 10, 20, 30, 128)]
    [InlineData("rgb(10 20 30)", 10, 20, 30, 255)]
    [InlineData("rgb(10 20 30 / 50%)", 10, 20, 30, 128)]
    [InlineData("rgb(100%, 0%, 50%)", 255, 0, 128, 255)]
    [InlineData("rgb(300, -5, 20)", 255, 0, 20, 255)]
    [InlineData("rgb(10.4, 20.6, 30)", 10, 21, 30, 255)]
    [InlineData("Yellow", 255, 255, 0, 255)]
    [InlineData("transparent", 0, 0, 0, 0)]
    [InlineData("  #fff !important", 255, 255, 255, 255)]
    public void TryParseCssColor_ValidValues(string css, int r, int g, int b, int a)
    {
        Assert.True(HtmlImporter.TryParseCssColor(css, out var color));
        Assert.Equal(new EditorColor((byte)r, (byte)g, (byte)b, (byte)a), color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#ff")]
    [InlineData("#ggg")]
    [InlineData("#12345")]
    [InlineData("rgb(1, 2)")]
    [InlineData("rgb(1, 2, x)")]
    [InlineData("rgba(1, 2, 3, 4, 5)")]
    [InlineData("rgb(1 2 3 / 4 / 5)")]
    [InlineData("hsl(0, 100%, 50%)")]
    [InlineData("notacolor")]
    public void TryParseCssColor_InvalidValues_ReturnFalse(string css)
    {
        Assert.False(HtmlImporter.TryParseCssColor(css, out _));
    }

    [Fact]
    public void Import_InvalidColor_KeepsInheritedColor()
    {
        var doc = new HtmlImporter().Import(
            "<p><span style=\"color:#0000ff\"><span style=\"color:rgb(1,2)\">x</span></span></p>");

        var run = Assert.IsType<TextRun>(((Paragraph)doc.Blocks[0]).Inlines[0]);
        Assert.Equal(EditorColor.Blue, run.Style.TextColor);
    }

    // ──────────────────────────────────────────────────────────────────
    // Public API extension methods
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void GetHtml_ExtensionMethod_ExportsFullDocument()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Hello");

        var html = doc.GetHtml();

        Assert.Equal("<p>Hello</p>", html);
    }

    [Fact]
    public void GetHtml_WithOptions_UsesOptions()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Hi");

        var html = doc.GetHtml(new HtmlExportOptions { IncludeDocumentWrapper = true });

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("Hi", html);
    }

    [Fact]
    public void GetHtml_EmptyRange_ReturnsEmptyString()
    {
        var doc = new EditorDocument();
        var html = doc.GetHtml(TextRange.Empty);

        Assert.Equal(string.Empty, html);
    }

    [Fact]
    public void GetHtml_RangeWithinParagraph_ExportsSelectedText()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Hello world");

        // Select "Hello"
        var start = new DocumentPosition(0, 0, 0);
        var end   = new DocumentPosition(0, 0, 5);
        var html = doc.GetHtml(new TextRange(start, end));

        Assert.Contains("Hello", html);
        Assert.DoesNotContain("world", html);
    }

    [Fact]
    public void LoadHtml_String_ReturnsDocument()
    {
        var doc = HtmlDocumentExtensions.LoadHtml("<p>Imported</p>");

        Assert.Single(doc.Blocks);
        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal("Imported", para.GetText());
    }

    [Fact]
    public void LoadHtml_Stream_ReturnsDocument()
    {
        var html = "<p>From stream</p>";
        using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(html));

        var doc = HtmlDocumentExtensions.LoadHtml(stream);

        var para = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal("From stream", para.GetText());
    }
}
