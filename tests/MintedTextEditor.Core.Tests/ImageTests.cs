using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Formatting;

namespace MintedTextEditor.Core.Tests;

public class ImageTests
{
    // ── Helpers ───────────────────────────────────────────────────────────

    private static (EditorDocument doc, Paragraph para) MakeDoc(string text = "Hello world")
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun(text);
        return (doc, para);
    }

    private static (EditorDocument doc, Paragraph para) MakeEmptyDoc()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        return (doc, para);
    }

    // ── InsertImage ───────────────────────────────────────────────────────

    [Fact]
    public void InsertImage_AddsImageInlineToParagraph()
    {
        var (doc, para) = MakeEmptyDoc();
        var pos = new DocumentPosition(0, 0, 0);

        ImageEngine.InsertImage(doc, pos, "img.png");

        Assert.Contains(para.Inlines, i => i is ImageInline);
    }

    [Fact]
    public void InsertImage_SetsSource()
    {
        var (doc, para) = MakeEmptyDoc();
        var pos = new DocumentPosition(0, 0, 0);

        var img = ImageEngine.InsertImage(doc, pos, "photo.jpg");

        Assert.Equal("photo.jpg", img.Source);
    }

    [Fact]
    public void InsertImage_SetsAltText()
    {
        var (doc, para) = MakeEmptyDoc();
        var pos = new DocumentPosition(0, 0, 0);

        var img = ImageEngine.InsertImage(doc, pos, "img.png", altText: "A nice photo");

        Assert.Equal("A nice photo", img.AltText);
    }

    [Fact]
    public void InsertImage_SetsDimensions()
    {
        var (doc, para) = MakeEmptyDoc();
        var pos = new DocumentPosition(0, 0, 0);

        var img = ImageEngine.InsertImage(doc, pos, "img.png", width: 320, height: 200);

        Assert.Equal(320f, img.Width);
        Assert.Equal(200f, img.Height);
    }

    [Fact]
    public void InsertImage_MidRun_SplitsTextRun()
    {
        var (doc, para) = MakeDoc("HelloWorld");
        // Insert after offset 5 in the single run at inline 0
        var pos = new DocumentPosition(0, 0, 5);

        ImageEngine.InsertImage(doc, pos, "mid.png");

        // Should have: TextRun("Hello"), ImageInline, TextRun("World")
        Assert.Equal(3, para.Inlines.Count);
        Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.IsType<ImageInline>(para.Inlines[1]);
        Assert.IsType<TextRun>(para.Inlines[2]);
        Assert.Equal("Hello", ((TextRun)para.Inlines[0]).Text);
        Assert.Equal("World", ((TextRun)para.Inlines[2]).Text);
    }

    [Fact]
    public void InsertImage_AtEnd_AppendsToInlines()
    {
        var (doc, para) = MakeDoc("Hello");
        // Past end-of-run offset → insert at inlineIndex == count
        var pos = new DocumentPosition(0, 1, 0);

        var img = ImageEngine.InsertImage(doc, pos, "end.png");

        Assert.Same(img, para.Inlines.Last());
    }

    [Fact]
    public void InsertImage_NotifiesDocumentChanged()
    {
        var (doc, para) = MakeEmptyDoc();
        bool notified = false;
        doc.Changed += (_, _) => notified = true;

        ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 0), "img.png");

        Assert.True(notified);
    }

    // ── RemoveImage ───────────────────────────────────────────────────────

    [Fact]
    public void RemoveImage_RemovesInlineFromParagraph()
    {
        var (doc, para) = MakeEmptyDoc();
        var img = ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 0), "img.png");

        ImageEngine.RemoveImage(doc, img);

        Assert.DoesNotContain(para.Inlines, i => i is ImageInline);
    }

    [Fact]
    public void RemoveImage_ClearsParentReference()
    {
        var (doc, para) = MakeEmptyDoc();
        var img = ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 0), "img.png");

        ImageEngine.RemoveImage(doc, img);

        Assert.Null(img.Parent);
    }

    [Fact]
    public void RemoveImage_NotifiesDocumentChanged()
    {
        var (doc, para) = MakeEmptyDoc();
        var img = ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 0), "img.png");
        bool notified = false;
        doc.Changed += (_, _) => notified = true;

        ImageEngine.RemoveImage(doc, img);

        Assert.True(notified);
    }

    // ── ResizeImage ───────────────────────────────────────────────────────

    [Fact]
    public void ResizeImage_UpdatesDimensions()
    {
        var (doc, para) = MakeEmptyDoc();
        var img = ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 0), "img.png", width: 100, height: 50);

        ImageEngine.ResizeImage(doc, img, 200, 100);

        Assert.Equal(200f, img.Width);
        Assert.Equal(100f, img.Height);
    }

    [Fact]
    public void ResizeImage_MaintainsAspectRatio_WhenOnlyWidthGiven()
    {
        var (doc, para) = MakeEmptyDoc();
        // 200x100 → aspect = 2.0
        var img = ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 0), "img.png", width: 200, height: 100);

        // Provide new width=400, height=0 → height should become 200
        ImageEngine.ResizeImage(doc, img, 400, 0, maintainAspectRatio: true);

        Assert.Equal(400f, img.Width);
        Assert.Equal(200f, img.Height);
    }

    [Fact]
    public void ResizeImage_MaintainsAspectRatio_WhenOnlyHeightGiven()
    {
        var (doc, para) = MakeEmptyDoc();
        // 200x100 → aspect = 2.0
        var img = ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 0), "img.png", width: 200, height: 100);

        // Provide height=50, width=0 → width should become 100
        ImageEngine.ResizeImage(doc, img, 0, 50, maintainAspectRatio: true);

        Assert.Equal(100f, img.Width);
        Assert.Equal(50f, img.Height);
    }

    [Fact]
    public void ResizeImage_MaintainsAspectRatio_WhenBothDimensionsGiven()
    {
        var (doc, para) = MakeEmptyDoc();
        var img = ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 0), "img.png", width: 200, height: 100);

        ImageEngine.ResizeImage(doc, img, 300, 300, maintainAspectRatio: true);

        Assert.Equal(300f, img.Width);
        Assert.Equal(150f, img.Height);
    }

    [Fact]
    public void ResizeImage_DoesNotMaintainAspectRatio_WhenDisabled()
    {
        var (doc, para) = MakeEmptyDoc();
        var img = ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 0), "img.png", width: 200, height: 100);

        ImageEngine.ResizeImage(doc, img, 300, 50, maintainAspectRatio: false);

        Assert.Equal(300f, img.Width);
        Assert.Equal(50f, img.Height);
    }

    [Fact]
    public void ResizeImage_NotifiesDocumentChanged()
    {
        var (doc, para) = MakeEmptyDoc();
        var img = ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 0), "img.png");
        bool notified = false;
        doc.Changed += (_, _) => notified = true;

        ImageEngine.ResizeImage(doc, img, 100, 100);

        Assert.True(notified);
    }

    // ── ReplaceImage ──────────────────────────────────────────────────────

    [Fact]
    public void ReplaceImage_UpdatesSource()
    {
        var (doc, para) = MakeEmptyDoc();
        var img = ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 0), "old.png");

        ImageEngine.ReplaceImage(doc, img, "new.png");

        Assert.Equal("new.png", img.Source);
    }

    [Fact]
    public void ReplaceImage_PreservesDimensions()
    {
        var (doc, para) = MakeEmptyDoc();
        var img = ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 0), "old.png", width: 150, height: 75);

        ImageEngine.ReplaceImage(doc, img, "new.png");

        Assert.Equal(150f, img.Width);
        Assert.Equal(75f, img.Height);
    }

    [Fact]
    public void ReplaceImage_NotifiesDocumentChanged()
    {
        var (doc, para) = MakeEmptyDoc();
        var img = ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 0), "old.png");
        bool notified = false;
        doc.Changed += (_, _) => notified = true;

        ImageEngine.ReplaceImage(doc, img, "new.png");

        Assert.True(notified);
    }
}
