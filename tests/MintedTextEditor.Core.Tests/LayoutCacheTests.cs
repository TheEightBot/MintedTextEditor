using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Layout;

namespace MintedTextEditor.Core.Tests;

public class LayoutCacheTests
{
    private readonly MockDrawingContext _ctx = new();

    [Fact]
    public void GetLayout_ReturnsCachedBlocksOnSecondCall()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Hello");

        var cache = new LayoutCache();
        var layout1 = cache.GetLayout(doc, 500f, _ctx);
        var layout2 = cache.GetLayout(doc, 500f, _ctx);

        // Same cached LayoutBlock instance reused
        Assert.Same(layout1.Blocks[0], layout2.Blocks[0]);
    }

    [Fact]
    public void InvalidateBlock_ClearsSpecificBlock()
    {
        var para1 = new Paragraph();
        para1.AppendRun("First");
        var para2 = new Paragraph();
        para2.AppendRun("Second");
        var doc = new EditorDocument(new Block[] { para1, para2 });

        var cache = new LayoutCache();
        var before = cache.GetLayout(doc, 500f, _ctx);
        var block0Before = before.Blocks[0];
        var block1Before = before.Blocks[1];

        cache.InvalidateBlock(0);

        var after = cache.GetLayout(doc, 500f, _ctx);

        Assert.NotSame(block0Before, after.Blocks[0]); // Invalidated
        Assert.Same(block1Before, after.Blocks[1]);     // Still cached
    }

    [Fact]
    public void InvalidateAll_ClearsEntireCache()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Hello");

        var cache = new LayoutCache();
        var before = cache.GetLayout(doc, 500f, _ctx);
        var blockBefore = before.Blocks[0];

        cache.InvalidateAll();

        var after = cache.GetLayout(doc, 500f, _ctx);
        Assert.NotSame(blockBefore, after.Blocks[0]);
    }

    [Fact]
    public void OnDocumentChanged_BlockSplit_InvalidatesFromAffectedBlockOnward()
    {
        var para1 = new Paragraph();
        para1.AppendRun("A");
        var para2 = new Paragraph();
        para2.AppendRun("B");
        var para3 = new Paragraph();
        para3.AppendRun("C");
        var doc = new EditorDocument(new Block[] { para1, para2, para3 });

        var cache = new LayoutCache();
        var before = cache.GetLayout(doc, 500f, _ctx);
        var b0 = before.Blocks[0];
        var b1 = before.Blocks[1];
        var b2 = before.Blocks[2];

        // BlockSplit at block 1 should invalidate blocks >= 1
        var args = new DocumentChangedEventArgs(
            DocumentChangeType.BlockSplit,
            new TextRange(new DocumentPosition(1, 0, 0), new DocumentPosition(1, 0, 0)));
        cache.OnDocumentChanged(doc, args);

        var after = cache.GetLayout(doc, 500f, _ctx);

        Assert.Same(b0, after.Blocks[0]);         // Still cached
        Assert.NotSame(b1, after.Blocks[1]);      // Invalidated
        Assert.NotSame(b2, after.Blocks[2]);      // Invalidated
    }

    [Fact]
    public void OnDocumentChanged_TextInserted_InvalidatesAffectedBlock()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Hello");

        var cache = new LayoutCache();
        var before = cache.GetLayout(doc, 500f, _ctx);
        var blockBefore = before.Blocks[0];

        var args = new DocumentChangedEventArgs(
            DocumentChangeType.TextInserted,
            new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 5)));
        cache.OnDocumentChanged(doc, args);

        var after = cache.GetLayout(doc, 500f, _ctx);
        Assert.NotSame(blockBefore, after.Blocks[0]);
    }

    [Fact]
    public void OnDocumentChanged_BlocksMerged_InvalidatesFromAffectedBlock()
    {
        var para1 = new Paragraph();
        para1.AppendRun("A");
        var para2 = new Paragraph();
        para2.AppendRun("B");
        var doc = new EditorDocument(new Block[] { para1, para2 });

        var cache = new LayoutCache();
        var before = cache.GetLayout(doc, 500f, _ctx);
        var b0 = before.Blocks[0];
        var b1 = before.Blocks[1];

        var args = new DocumentChangedEventArgs(
            DocumentChangeType.BlocksMerged,
            new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 0)));
        cache.OnDocumentChanged(doc, args);

        var after = cache.GetLayout(doc, 500f, _ctx);

        Assert.NotSame(b0, after.Blocks[0]);
        Assert.NotSame(b1, after.Blocks[1]);
    }

    [Fact]
    public void GetLayout_WidthChange_InvalidatesCachedBlocks()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Hello world");

        var cache = new LayoutCache();
        var before = cache.GetLayout(doc, 500f, _ctx);
        var blockBefore = before.Blocks[0];

        var after = cache.GetLayout(doc, 320f, _ctx);

        Assert.NotSame(blockBefore, after.Blocks[0]);
    }

    [Fact]
    public void GetLayout_NumberedList_AssignsSequentialListNumbers()
    {
        var p1 = new Paragraph("One");
        p1.Style.ListType = ListType.Number;
        var p2 = new Paragraph("Two");
        p2.Style.ListType = ListType.Number;
        var p3 = new Paragraph("Break");
        p3.Style.ListType = ListType.None;
        var p4 = new Paragraph("Three");
        p4.Style.ListType = ListType.Number;

        var doc = new EditorDocument(new Block[] { p1, p2, p3, p4 });
        var cache = new LayoutCache();

        var layout = cache.GetLayout(doc, 500f, _ctx);

        Assert.Equal(1, layout.Blocks[0].ListNumber);
        Assert.Equal(2, layout.Blocks[1].ListNumber);
        Assert.Equal(0, layout.Blocks[2].ListNumber);
        Assert.Equal(1, layout.Blocks[3].ListNumber);
    }
}
