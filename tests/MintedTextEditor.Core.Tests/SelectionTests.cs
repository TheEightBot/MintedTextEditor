using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;

namespace MintedTextEditor.Core.Tests;

public class SelectionTests
{
    // ── Selection class ───────────────────────────────────────────────

    [Fact]
    public void Selection_DefaultState_IsEmpty()
    {
        var sel = new Selection();

        Assert.True(sel.IsEmpty);
        Assert.Equal(new DocumentPosition(0, 0, 0), sel.Anchor);
        Assert.Equal(new DocumentPosition(0, 0, 0), sel.Active);
    }

    [Fact]
    public void CollapseTo_SetsAnchorAndActiveToSamePosition()
    {
        var sel = new Selection();
        sel.Set(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 5));

        sel.CollapseTo(new DocumentPosition(1, 2, 3));

        Assert.True(sel.IsEmpty);
        Assert.Equal(new DocumentPosition(1, 2, 3), sel.Anchor);
        Assert.Equal(new DocumentPosition(1, 2, 3), sel.Active);
    }

    [Fact]
    public void ExtendTo_MovesActivePreservesAnchor()
    {
        var sel = new Selection();
        var anchor = new DocumentPosition(0, 0, 2);
        sel.CollapseTo(anchor);

        sel.ExtendTo(new DocumentPosition(0, 0, 7));

        Assert.False(sel.IsEmpty);
        Assert.Equal(anchor, sel.Anchor);
        Assert.Equal(new DocumentPosition(0, 0, 7), sel.Active);
    }

    [Fact]
    public void Set_SetsAnchorAndActive_ForwardSelection()
    {
        var sel = new Selection();

        sel.Set(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 10));

        Assert.Equal(new DocumentPosition(0, 0, 0), sel.Anchor);
        Assert.Equal(new DocumentPosition(0, 0, 10), sel.Active);
        Assert.False(sel.IsEmpty);
    }

    [Fact]
    public void Set_SetsAnchorAndActive_BackwardSelection()
    {
        var sel = new Selection();

        // Backward: anchor after active
        sel.Set(new DocumentPosition(0, 0, 10), new DocumentPosition(0, 0, 2));

        Assert.Equal(new DocumentPosition(0, 0, 10), sel.Anchor);
        Assert.Equal(new DocumentPosition(0, 0, 2), sel.Active);
        // Range should normalize: Start <= End
        Assert.Equal(new DocumentPosition(0, 0, 2),  sel.Range.Start);
        Assert.Equal(new DocumentPosition(0, 0, 10), sel.Range.End);
    }

    [Fact]
    public void Range_ReturnsNormalizedTextRange()
    {
        var sel = new Selection();
        sel.Set(new DocumentPosition(0, 0, 5), new DocumentPosition(0, 0, 2));

        var range = sel.Range;

        Assert.Equal(new DocumentPosition(0, 0, 2), range.Start);
        Assert.Equal(new DocumentPosition(0, 0, 5), range.End);
    }

    [Fact]
    public void SelectAll_SingleParagraph_SelectsFromStartToEnd()
    {
        var doc = new EditorDocument([new Paragraph("Hello world")]);
        var sel = new Selection();

        sel.SelectAll(doc);

        Assert.Equal(new DocumentPosition(0, 0, 0), sel.Anchor);
        Assert.Equal(new DocumentPosition(0, 0, 11), sel.Active);
        Assert.False(sel.IsEmpty);
    }

    [Fact]
    public void SelectAll_MultiParagraph_SelectsToEndOfLastBlock()
    {
        var doc = new EditorDocument([
            new Paragraph("First"),
            new Paragraph("Second"),
        ]);
        var sel = new Selection();

        sel.SelectAll(doc);

        Assert.Equal(new DocumentPosition(0, 0, 0), sel.Anchor);
        Assert.Equal(new DocumentPosition(1, 0, 6), sel.Active);
    }

    [Fact]
    public void SelectAll_EmptyDocument_RemainsEmpty()
    {
        var doc = new EditorDocument();
        var sel = new Selection();

        sel.SelectAll(doc);

        Assert.Equal(new DocumentPosition(0, 0, 0), sel.Anchor);
        Assert.Equal(new DocumentPosition(0, 0, 0), sel.Active);
        Assert.True(sel.IsEmpty);
    }

    // ── DocumentEditor.GetSelectedText ────────────────────────────────

    [Fact]
    public void GetSelectedText_EmptyRange_ReturnsEmptyString()
    {
        var doc = new EditorDocument([new Paragraph("Hello")]);
        var range = new TextRange(new DocumentPosition(0, 0, 2), new DocumentPosition(0, 0, 2));

        var text = DocumentEditor.GetSelectedText(doc, range);

        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void GetSelectedText_PartialRun_ReturnsSubstring()
    {
        var doc = new EditorDocument([new Paragraph("Hello world")]);
        var range = new TextRange(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 11));

        var text = DocumentEditor.GetSelectedText(doc, range);

        Assert.Equal("world", text);
    }

    [Fact]
    public void GetSelectedText_FullRun_ReturnsAllText()
    {
        var doc = new EditorDocument([new Paragraph("Hello")]);
        var range = new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 5));

        var text = DocumentEditor.GetSelectedText(doc, range);

        Assert.Equal("Hello", text);
    }

    [Fact]
    public void GetSelectedText_MultiParagraph_JoinedWithNewline()
    {
        var doc = new EditorDocument([
            new Paragraph("First"),
            new Paragraph("Second"),
        ]);
        var range = new TextRange(
            new DocumentPosition(0, 0, 0),
            new DocumentPosition(1, 0, 6));

        var text = DocumentEditor.GetSelectedText(doc, range);

        Assert.Equal("First\nSecond", text);
    }

    [Fact]
    public void GetSelectedText_BackwardRange_SameAsForward()
    {
        var doc = new EditorDocument([new Paragraph("Hello world")]);
        // TextRange normalizes, so swapping start/end gives the same result
        var range1 = new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 5));
        var range2 = new TextRange(new DocumentPosition(0, 0, 5), new DocumentPosition(0, 0, 0));

        Assert.Equal(DocumentEditor.GetSelectedText(doc, range1),
                     DocumentEditor.GetSelectedText(doc, range2));
    }

    [Fact]
    public void GetSelectedText_SpansMultipleInlines_ConcatenatesRuns()
    {
        var para = new Paragraph();
        para.AppendRun("Hello ", TextStyle.Default);
        para.AppendRun("world", TextStyle.Default.WithBold(true));
        var doc = new EditorDocument([para]);
        var range = new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 1, 5));

        var text = DocumentEditor.GetSelectedText(doc, range);

        Assert.Equal("Hello world", text);
    }
}
