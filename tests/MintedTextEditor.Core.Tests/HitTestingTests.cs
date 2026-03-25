using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Input;
using MintedTextEditor.Core.Layout;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Tests;

public class HitTestingTests
{
    private readonly MockDrawingContext _context = new();
    private readonly TextLayoutEngine _engine = new();

    private (EditorDocument doc, DocumentLayout layout) CreateSingleLineDoc(string text)
    {
        var para = new Paragraph();
        para.AppendRun(text);
        var doc = new EditorDocument(new Block[] { para });
        var layout = _engine.Layout(doc, 800f, _context);
        return (doc, layout);
    }

    private (EditorDocument doc, DocumentLayout layout) CreateMultiLineDoc(params string[] lines)
    {
        var blocks = lines.Select(text =>
        {
            var para = new Paragraph();
            para.AppendRun(text);
            return (Block)para;
        }).ToArray();
        var doc = new EditorDocument(blocks);
        var layout = _engine.Layout(doc, 800f, _context);
        return (doc, layout);
    }

    [Fact]
    public void HitTest_ClickWithinWord_ReturnsCorrectPosition()
    {
        // "Hello" — each char is 8px wide, so "Hel" = 24px
        var (doc, layout) = CreateSingleLineDoc("Hello");

        // Click at x=24 (start of 'l' at index 3), y in first line
        var result = DocumentHitTester.HitTest(24f, 5f, layout, doc, _context);

        Assert.Equal(0, result.Position.BlockIndex);
        Assert.Equal(0, result.Position.InlineIndex);
        Assert.Equal(3, result.Position.Offset);
        Assert.False(result.IsAtLineEnd);
    }

    [Fact]
    public void HitTest_ClickAtStartOfLine_ReturnsOffsetZero()
    {
        var (doc, layout) = CreateSingleLineDoc("Hello");

        var result = DocumentHitTester.HitTest(0f, 5f, layout, doc, _context);

        Assert.Equal(0, result.Position.Offset);
    }

    [Fact]
    public void HitTest_ClickBeyondEndOfLine_ReturnsLineEnd()
    {
        var (doc, layout) = CreateSingleLineDoc("Hello");

        // "Hello" is 40px wide, click at 100
        var result = DocumentHitTester.HitTest(100f, 5f, layout, doc, _context);

        Assert.Equal(5, result.Position.Offset);
        Assert.True(result.IsAtLineEnd);
    }

    [Fact]
    public void HitTest_ClickInMargin_ReturnsLineStart()
    {
        var (doc, layout) = CreateSingleLineDoc("Hello");

        // Negative X (left margin)
        var result = DocumentHitTester.HitTest(-10f, 5f, layout, doc, _context);

        Assert.Equal(0, result.Position.Offset);
    }

    [Fact]
    public void HitTest_ClickBelowDocument_ReturnsLastBlockLastLine()
    {
        var (doc, layout) = CreateMultiLineDoc("First", "Second");

        // Click way below both blocks
        var result = DocumentHitTester.HitTest(0f, 5000f, layout, doc, _context);

        Assert.Equal(1, result.Position.BlockIndex);
        Assert.True(result.IsAfterLastBlock);
    }

    [Fact]
    public void HitTest_ClickInSecondBlock_ReturnsCorrectBlockIndex()
    {
        var (doc, layout) = CreateMultiLineDoc("First", "Second");

        // Second block starts after the first block's height
        var secondBlock = layout.Blocks[1];
        float y = secondBlock.Y + 5f;

        var result = DocumentHitTester.HitTest(16f, y, layout, doc, _context);

        Assert.Equal(1, result.Position.BlockIndex);
        // 16px / 8px per char = 2 chars
        Assert.Equal(2, result.Position.Offset);
    }

    [Fact]
    public void HitTest_EmptyDocument_ReturnsZeroPosition()
    {
        var doc = new EditorDocument();
        var layout = _engine.Layout(doc, 800f, _context);

        var result = DocumentHitTester.HitTest(50f, 50f, layout, doc, _context);

        Assert.Equal(new DocumentPosition(0, 0, 0), result.Position);
    }

    [Fact]
    public void HitTest_SnapToNearestCharacter()
    {
        var (doc, layout) = CreateSingleLineDoc("ABCDE");

        // Each char is 8px. Midpoint of char at index 2 ('C') is 20px.
        // Click at x=19 (just before midpoint of 'C') should snap to index 2.
        // Click at x=21 (just after midpoint of 'C') should snap to index 3.
        var resultBefore = DocumentHitTester.HitTest(19f, 5f, layout, doc, _context);
        var resultAfter = DocumentHitTester.HitTest(21f, 5f, layout, doc, _context);

        Assert.Equal(2, resultBefore.Position.Offset);
        Assert.Equal(3, resultAfter.Position.Offset);
    }

    [Fact]
    public void HitTest_AboveDocument_ReturnsFirstBlock()
    {
        var (doc, layout) = CreateMultiLineDoc("First", "Second");

        var result = DocumentHitTester.HitTest(0f, -100f, layout, doc, _context);

        Assert.Equal(0, result.Position.BlockIndex);
    }
}
