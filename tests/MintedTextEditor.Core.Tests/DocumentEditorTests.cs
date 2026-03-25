using MintedTextEditor.Core.Document;


namespace MintedTextEditor.Core.Tests;

public class DocumentEditorTests
{
    [Fact]
    public void InsertText_IntoEmptyDocument_AddsRun()
    {
        var doc = new EditorDocument();
        var pos = new DocumentPosition(0, 0, 0);

        var newPos = DocumentEditor.InsertText(doc, pos, "Hello");

        Assert.Equal("Hello", doc.GetText());
        Assert.Equal(new DocumentPosition(0, 0, 5), newPos);
    }

    [Fact]
    public void InsertText_IntoExistingRun_SameStyle()
    {
        var doc = new EditorDocument([new Paragraph("Hello")]);
        var pos = new DocumentPosition(0, 0, 5);

        var newPos = DocumentEditor.InsertText(doc, pos, " World");

        Assert.Equal("Hello World", doc.GetText());
        Assert.Equal(new DocumentPosition(0, 0, 11), newPos);
    }

    [Fact]
    public void InsertText_AtMiddle_SameStyle()
    {
        var doc = new EditorDocument([new Paragraph("HeWorld")]);
        var pos = new DocumentPosition(0, 0, 2);

        DocumentEditor.InsertText(doc, pos, "llo ");

        Assert.Equal("Hello World", doc.GetText());
    }

    [Fact]
    public void InsertText_DifferentStyle_SplitsRun()
    {
        var doc = new EditorDocument([new Paragraph("HelloWorld")]);
        var boldStyle = TextStyle.Default.WithBold(true);

        DocumentEditor.InsertText(doc, new DocumentPosition(0, 0, 5), " Bold ", boldStyle);

        var para = (Paragraph)doc.Blocks[0];
        Assert.True(para.Inlines.Count >= 2); // At least 2 runs due to style split
        Assert.Equal("Hello Bold World", doc.GetText());
    }

    [Fact]
    public void InsertText_EmptyString_ReturnsOriginalPosition()
    {
        var doc = new EditorDocument();
        var pos = new DocumentPosition(0, 0, 0);
        var result = DocumentEditor.InsertText(doc, pos, "");
        Assert.Equal(pos, result);
    }

    [Fact]
    public void InsertText_FiresChangeNotification()
    {
        var doc = new EditorDocument();
        int changeCount = 0;
        doc.Changed += (_, e) =>
        {
            Assert.Equal(DocumentChangeType.TextInserted, e.ChangeType);
            changeCount++;
        };

        DocumentEditor.InsertText(doc, new DocumentPosition(0, 0, 0), "Hello");
        Assert.Equal(1, changeCount);
    }

    [Fact]
    public void DeleteRange_SingleRun_DeletesText()
    {
        var doc = new EditorDocument([new Paragraph("Hello World")]);
        var range = new TextRange(
            new DocumentPosition(0, 0, 5),
            new DocumentPosition(0, 0, 11));

        var pos = DocumentEditor.DeleteRange(doc, range);

        Assert.Equal("Hello", doc.GetText());
        Assert.Equal(new DocumentPosition(0, 0, 5), pos);
    }

    [Fact]
    public void DeleteRange_EmptyRange_NoChange()
    {
        var doc = new EditorDocument([new Paragraph("Hello")]);
        var pos = new DocumentPosition(0, 0, 2);
        var range = new TextRange(pos, pos);

        var result = DocumentEditor.DeleteRange(doc, range);
        Assert.Equal("Hello", doc.GetText());
        Assert.Equal(pos, result);
    }

    [Fact]
    public void InsertText_DeleteRange_RoundTrip()
    {
        var doc = new EditorDocument();
        var pos = new DocumentPosition(0, 0, 0);

        var afterInsert = DocumentEditor.InsertText(doc, pos, "Hello World");
        Assert.Equal("Hello World", doc.GetText());

        var range = new TextRange(pos, afterInsert);
        DocumentEditor.DeleteRange(doc, range);
        Assert.Equal("", doc.GetText());
    }

    [Fact]
    public void SplitBlock_CreatesNewBlock()
    {
        var doc = new EditorDocument([new Paragraph("HelloWorld")]);
        var pos = new DocumentPosition(0, 0, 5);

        var newPos = DocumentEditor.SplitBlock(doc, pos);

        Assert.Equal(2, doc.BlockCount);
        Assert.Equal("Hello", doc.Blocks[0].GetText());
        Assert.Equal("World", doc.Blocks[1].GetText());
        Assert.Equal(new DocumentPosition(1, 0, 0), newPos);
    }

    [Fact]
    public void MergeBlocks_CombinesBlocks()
    {
        var doc = new EditorDocument([new Paragraph("Hello"), new Paragraph(" World")]);

        var pos = DocumentEditor.MergeBlocks(doc, 0);

        Assert.Equal(1, doc.BlockCount);
        Assert.Equal("Hello World", doc.GetText());
    }

    [Fact]
    public void SplitBlock_ThenMerge_RoundTrip()
    {
        var doc = new EditorDocument([new Paragraph("HelloWorld")]);

        DocumentEditor.SplitBlock(doc, new DocumentPosition(0, 0, 5));
        Assert.Equal(2, doc.BlockCount);

        DocumentEditor.MergeBlocks(doc, 0);
        Assert.Equal(1, doc.BlockCount);
        Assert.Equal("HelloWorld", doc.GetText());
    }

    [Fact]
    public void MergeBlocks_InvalidIndex_Throws()
    {
        var doc = new EditorDocument();
        Assert.Throws<ArgumentOutOfRangeException>(() => DocumentEditor.MergeBlocks(doc, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => DocumentEditor.MergeBlocks(doc, -1));
    }

    [Fact]
    public void ApplyTextStyle_ChangesStyleOnRange()
    {
        var doc = new EditorDocument([new Paragraph("Hello World")]);
        var range = new TextRange(
            new DocumentPosition(0, 0, 0),
            new DocumentPosition(0, 0, 5));

        DocumentEditor.ApplyTextStyle(doc, range, s => s.WithBold(true));

        var para = (Paragraph)doc.Blocks[0];
        var firstRun = (TextRun)para.Inlines[0];
        Assert.True(firstRun.Style.IsBold);
        Assert.Equal("Hello", firstRun.Text);
    }

    [Fact]
    public void ApplyParagraphStyle_ChangesAlignment()
    {
        var doc = new EditorDocument([new Paragraph("Hello")]);
        var range = new TextRange(
            new DocumentPosition(0, 0, 0),
            new DocumentPosition(0, 0, 5));

        DocumentEditor.ApplyParagraphStyle(doc, range, s => s.Alignment = Rendering.TextAlignment.Center);

        var para = (Paragraph)doc.Blocks[0];
        Assert.Equal(Rendering.TextAlignment.Center, para.Style.Alignment);
    }

    [Fact]
    public void DeleteRange_AcrossBlocks_MergesContent()
    {
        var doc = new EditorDocument([new Paragraph("Hello"), new Paragraph("World")]);
        var range = new TextRange(
            new DocumentPosition(0, 0, 3),
            new DocumentPosition(1, 0, 2));

        DocumentEditor.DeleteRange(doc, range);

        Assert.Equal(1, doc.BlockCount);
        Assert.Equal("Helrld", doc.GetText());
    }
}
