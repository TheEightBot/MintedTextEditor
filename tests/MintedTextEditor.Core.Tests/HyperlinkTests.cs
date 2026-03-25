using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Formatting;

namespace MintedTextEditor.Core.Tests;

public class HyperlinkTests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>A document with a single paragraph containing one run of <paramref name="text"/>.</summary>
    private static (EditorDocument doc, Paragraph para) MakeDoc(string text = "Visit example.com today")
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun(text);
        return (doc, para);
    }

    // ── InsertHyperlink ───────────────────────────────────────────────────

    [Fact]
    public void InsertHyperlink_WrapsSelectedTextInHyperlinkInline()
    {
        var (doc, para) = MakeDoc("Visit example.com today");
        // Select "example.com" (chars 6–17)
        var range = new TextRange(
            new DocumentPosition(0, 0, 6),
            new DocumentPosition(0, 0, 17));

        HyperlinkEngine.InsertHyperlink(doc, range, "https://example.com");

        // Paragraph should now contain a HyperlinkInline somewhere
        Assert.Contains(para.Inlines, i => i is HyperlinkInline);
    }

    [Fact]
    public void InsertHyperlink_SetsUrl()
    {
        var (doc, para) = MakeDoc("Click here");
        var range = new TextRange(
            new DocumentPosition(0, 0, 6),
            new DocumentPosition(0, 0, 10));

        var link = HyperlinkEngine.InsertHyperlink(doc, range, "https://test.com");

        Assert.Equal("https://test.com", link.Url);
    }

    [Fact]
    public void InsertHyperlink_SetsTitle()
    {
        var (doc, para) = MakeDoc("Click here");
        var range = new TextRange(
            new DocumentPosition(0, 0, 6),
            new DocumentPosition(0, 0, 10));

        var link = HyperlinkEngine.InsertHyperlink(doc, range, "https://test.com", "Test Site");

        Assert.Equal("Test Site", link.Title);
    }

    [Fact]
    public void InsertHyperlink_PreservesLinkText()
    {
        var (doc, para) = MakeDoc("Click here");
        var range = new TextRange(
            new DocumentPosition(0, 0, 6),
            new DocumentPosition(0, 0, 10));

        var link = HyperlinkEngine.InsertHyperlink(doc, range, "https://test.com");

        Assert.Equal("here", link.GetText());
    }

    [Fact]
    public void InsertHyperlink_EmptyRange_InsertsUrlAsText()
    {
        var (doc, para) = MakeDoc("before  after");
        var pos = new DocumentPosition(0, 0, 7);

        var link = HyperlinkEngine.InsertHyperlink(doc, new TextRange(pos, pos), "https://new.io");

        Assert.Equal("https://new.io", link.GetText());
        Assert.Equal("https://new.io", link.Url);
    }

    // ── EditHyperlink ─────────────────────────────────────────────────────

    [Fact]
    public void EditHyperlink_UpdatesUrl()
    {
        var (doc, para) = MakeDoc("Click here");
        var range = new TextRange(
            new DocumentPosition(0, 0, 6),
            new DocumentPosition(0, 0, 10));
        var link = HyperlinkEngine.InsertHyperlink(doc, range, "https://old.com");

        HyperlinkEngine.EditHyperlink(doc, link, "https://new.com");

        Assert.Equal("https://new.com", link.Url);
    }

    [Fact]
    public void EditHyperlink_UpdatesTitle()
    {
        var (doc, para) = MakeDoc("Click here");
        var range = new TextRange(
            new DocumentPosition(0, 0, 6),
            new DocumentPosition(0, 0, 10));
        var link = HyperlinkEngine.InsertHyperlink(doc, range, "https://old.com", "Old");

        HyperlinkEngine.EditHyperlink(doc, link, "https://old.com", "New Title");

        Assert.Equal("New Title", link.Title);
    }

    // ── RemoveHyperlink ───────────────────────────────────────────────────

    [Fact]
    public void RemoveHyperlink_RemovesHyperlinkInline()
    {
        var (doc, para) = MakeDoc("Click here");
        var range = new TextRange(
            new DocumentPosition(0, 0, 6),
            new DocumentPosition(0, 0, 10));
        var link = HyperlinkEngine.InsertHyperlink(doc, range, "https://test.com");

        // Find the position where the hyperlink was inserted
        int idx = para.Inlines.IndexOf(link);
        var pos = new DocumentPosition(0, idx, 0);
        HyperlinkEngine.RemoveHyperlink(doc, pos);

        Assert.DoesNotContain(para.Inlines, i => i is HyperlinkInline);
    }

    [Fact]
    public void RemoveHyperlink_PreservesText()
    {
        var (doc, para) = MakeDoc("Click here");
        var range = new TextRange(
            new DocumentPosition(0, 0, 6),
            new DocumentPosition(0, 0, 10));
        var link = HyperlinkEngine.InsertHyperlink(doc, range, "https://test.com");

        int idx = para.Inlines.IndexOf(link);
        var pos = new DocumentPosition(0, idx, 0);
        HyperlinkEngine.RemoveHyperlink(doc, pos);

        // The plain text of the whole paragraph should still contain "here"
        string fullText = string.Concat(para.Inlines.Select(i => i.GetText()));
        Assert.Contains("here", fullText);
    }

    // ── GetHyperlinkAtPosition ────────────────────────────────────────────

    [Fact]
    public void GetHyperlinkAtPosition_ReturnsHyperlinkInline()
    {
        var (doc, para) = MakeDoc("Click here");
        var range = new TextRange(
            new DocumentPosition(0, 0, 6),
            new DocumentPosition(0, 0, 10));
        var link = HyperlinkEngine.InsertHyperlink(doc, range, "https://test.com");

        int idx = para.Inlines.IndexOf(link);
        var pos = new DocumentPosition(0, idx, 0);
        var found = HyperlinkEngine.GetHyperlinkAtPosition(doc, pos);

        Assert.Same(link, found);
    }

    [Fact]
    public void GetHyperlinkAtPosition_NoHyperlink_ReturnsNull()
    {
        var (doc, para) = MakeDoc("Plain text");
        var pos = new DocumentPosition(0, 0, 0);
        var found = HyperlinkEngine.GetHyperlinkAtPosition(doc, pos);
        Assert.Null(found);
    }

    [Fact]
    public void GetHyperlinkAtPosition_OutOfBounds_ReturnsNull()
    {
        var (doc, _) = MakeDoc("Plain text");
        var pos = new DocumentPosition(0, 99, 0);
        var found = HyperlinkEngine.GetHyperlinkAtPosition(doc, pos);
        Assert.Null(found);
    }

    // ── AutoDetectUrl ──────────────────────────────────────────────────────

    [Fact]
    public void AutoDetectUrl_RecognisesHttpsUrl()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("https://example.com ");

        // Caret is immediately after the space (offset 20)
        var link = HyperlinkEngine.AutoDetectUrl(doc, 0, 0, 20);

        Assert.NotNull(link);
        Assert.Equal("https://example.com", link!.Url);
    }

    [Fact]
    public void AutoDetectUrl_NoUrl_ReturnsNull()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("just a normal word ");

        var link = HyperlinkEngine.AutoDetectUrl(doc, 0, 0, 19);

        Assert.Null(link);
    }
}
