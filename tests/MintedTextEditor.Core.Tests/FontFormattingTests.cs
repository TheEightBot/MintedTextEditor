using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Tests;

public class FontFormattingTests
{
    private readonly FontFormattingEngine _engine = new();

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>A document with a single paragraph containing one run of <paramref name="text"/>.</summary>
    private static (EditorDocument doc, TextRange range) MakeDoc(string text = "Hello world")
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun(text);
        var range = new TextRange(
            new DocumentPosition(0, 0, 0),
            new DocumentPosition(0, 0, text.Length));
        return (doc, range);
    }

    // ── ApplyFontFamily ───────────────────────────────────────────────────

    [Fact]
    public void ApplyFontFamily_UpdatesRunFontFamily()
    {
        var (doc, range) = MakeDoc();
        _engine.ApplyFontFamily(doc, range, "Arial");

        var para = (Paragraph)doc.Blocks[0];
        Assert.Equal("Arial", ((TextRun)para.Inlines[0]).Style.FontFamily);
    }

    [Fact]
    public void ApplyFontFamily_SplitsRunAtBoundary()
    {
        var (doc, _) = MakeDoc("HelloWorld");
        // apply only to first 5 chars
        var range = new TextRange(
            new DocumentPosition(0, 0, 0),
            new DocumentPosition(0, 0, 5));
        _engine.ApplyFontFamily(doc, range, "Courier");

        var para = (Paragraph)doc.Blocks[0];
        // Should have split into 2 runs
        Assert.True(para.Inlines.Count >= 2);
        Assert.Equal("Courier", ((TextRun)para.Inlines[0]).Style.FontFamily);
    }

    // ── ApplyFontSize ─────────────────────────────────────────────────────

    [Fact]
    public void ApplyFontSize_UpdatesRunFontSize()
    {
        var (doc, range) = MakeDoc();
        _engine.ApplyFontSize(doc, range, 24f);

        var para = (Paragraph)doc.Blocks[0];
        Assert.Equal(24f, ((TextRun)para.Inlines[0]).Style.FontSize);
    }

    [Fact]
    public void ApplyFontSize_PartialRange_OtherRunUnchanged()
    {
        var (doc, _) = MakeDoc("AB");
        // apply only to first char
        var range = new TextRange(
            new DocumentPosition(0, 0, 0),
            new DocumentPosition(0, 0, 1));
        _engine.ApplyFontSize(doc, range, 32f);

        var para = (Paragraph)doc.Blocks[0];
        // First run should be 32f; original unstyled run should still be default
        Assert.Equal(32f, ((TextRun)para.Inlines[0]).Style.FontSize);
        Assert.Equal(TextStyle.Default.FontSize, ((TextRun)para.Inlines[1]).Style.FontSize);
    }

    // ── ApplyTextColor ────────────────────────────────────────────────────

    [Fact]
    public void ApplyTextColor_UpdatesRunColor()
    {
        var (doc, range) = MakeDoc();
        var red = new EditorColor(255, 0, 0, 255);
        _engine.ApplyTextColor(doc, range, red);

        var para = (Paragraph)doc.Blocks[0];
        Assert.Equal(red, ((TextRun)para.Inlines[0]).Style.TextColor);
    }

    // ── ApplyHighlightColor ───────────────────────────────────────────────

    [Fact]
    public void ApplyHighlightColor_UpdatesHighlight()
    {
        var (doc, range) = MakeDoc();
        var yellow = new EditorColor(255, 255, 0, 255);
        _engine.ApplyHighlightColor(doc, range, yellow);

        var para = (Paragraph)doc.Blocks[0];
        Assert.Equal(yellow, ((TextRun)para.Inlines[0]).Style.HighlightColor);
    }

    // ── RemoveHighlightColor ──────────────────────────────────────────────

    [Fact]
    public void RemoveHighlightColor_SetsTransparent()
    {
        var (doc, range) = MakeDoc();
        // First apply a color
        _engine.ApplyHighlightColor(doc, range, new EditorColor(255, 255, 0, 255));
        // Then remove it
        _engine.RemoveHighlightColor(doc, range);

        var para = (Paragraph)doc.Blocks[0];
        Assert.Equal(EditorColor.Transparent, ((TextRun)para.Inlines[0]).Style.HighlightColor);
    }

    // ── GetCurrentTextStyle ───────────────────────────────────────────────

    [Fact]
    public void GetCurrentTextStyle_ReturnsRunStyle()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        var style = new TextStyle(fontFamily: "Georgia", fontSize: 18f);
        para.Inlines.Add(new TextRun("Hello", style));

        var result = FontFormattingEngine.GetCurrentTextStyle(doc, new DocumentPosition(0, 0, 0));

        Assert.Equal("Georgia", result.FontFamily);
        Assert.Equal(18f, result.FontSize);
    }

    [Fact]
    public void GetCurrentTextStyle_EmptyParagraph_ReturnsDefault()
    {
        var doc = new EditorDocument();
        var result = FontFormattingEngine.GetCurrentTextStyle(doc, new DocumentPosition(0, 0, 0));
        Assert.Equal(TextStyle.Default, result);
    }

    [Fact]
    public void GetCurrentTextStyle_OutOfBounds_ReturnsDefault()
    {
        var doc = new EditorDocument();
        var result = FontFormattingEngine.GetCurrentTextStyle(doc, new DocumentPosition(99, 0, 0));
        Assert.Equal(TextStyle.Default, result);
    }

    // ── GetTextStyleForRange ──────────────────────────────────────────────

    [Fact]
    public void GetTextStyleForRange_UniformRange_ReturnsStyle()
    {
        var (doc, range) = MakeDoc();
        // Apply a style to the whole range first
        _engine.ApplyFontSize(doc, range, 20f);

        var result = FontFormattingEngine.GetTextStyleForRange(doc, range);

        Assert.NotNull(result);
        Assert.Equal(20f, result!.FontSize);
    }

    [Fact]
    public void GetTextStyleForRange_MixedStyles_ReturnsNull()
    {
        var (doc, _) = MakeDoc("AB");

        // Apply different sizes to the two halves
        var range1 = new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 1));
        var range2 = new TextRange(new DocumentPosition(0, 0, 1), new DocumentPosition(0, 0, 2));
        _engine.ApplyFontSize(doc, range1, 10f);
        _engine.ApplyFontSize(doc, range2, 20f);

        // Now query the full range
        var fullRange = new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 2));

        // Note: GetTextStyleForRange needs to address the resulting runs — re-derive positions
        // The two runs are now at inline indices 0 and 1
        var mixed = new TextRange(
            new DocumentPosition(0, 0, 0),
            new DocumentPosition(0, 1, 1));

        var result = FontFormattingEngine.GetTextStyleForRange(doc, mixed);
        Assert.Null(result);
    }

    [Fact]
    public void GetTextStyleForRange_EmptyRange_ReturnsNull()
    {
        var (doc, _) = MakeDoc();
        var empty = new TextRange(
            new DocumentPosition(0, 0, 0),
            new DocumentPosition(0, 0, 0));

        var result = FontFormattingEngine.GetTextStyleForRange(doc, empty);
        Assert.Null(result);
    }
}
