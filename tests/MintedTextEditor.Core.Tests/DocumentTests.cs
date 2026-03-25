using MintedTextEditor.Core.Document;


namespace MintedTextEditor.Core.Tests;

public class DocumentTests
{
    [Fact]
    public void NewDocument_HasOneEmptyParagraph()
    {
        var doc = new EditorDocument();
        Assert.Equal(1, doc.BlockCount);
        Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal("", doc.GetText());
    }

    [Fact]
    public void Constructor_WithBlocks_SetsBlocks()
    {
        var p1 = new Paragraph("Hello");
        var p2 = new Paragraph("World");
        var doc = new EditorDocument([p1, p2]);

        Assert.Equal(2, doc.BlockCount);
        Assert.Equal("Hello\nWorld", doc.GetText());
    }

    [Fact]
    public void Constructor_WithEmptyBlocks_AddsDefaultParagraph()
    {
        var doc = new EditorDocument(Enumerable.Empty<Block>());
        Assert.Equal(1, doc.BlockCount);
    }

    [Fact]
    public void AddBlock_AppendsToEnd()
    {
        var doc = new EditorDocument();
        doc.AddBlock(new Paragraph("Second"));

        Assert.Equal(2, doc.BlockCount);
        Assert.Equal("Second", doc.Blocks[1].GetText());
    }

    [Fact]
    public void InsertBlock_InsertsAtIndex()
    {
        var doc = new EditorDocument([new Paragraph("A"), new Paragraph("C")]);
        doc.InsertBlock(1, new Paragraph("B"));

        Assert.Equal(3, doc.BlockCount);
        Assert.Equal("B", doc.Blocks[1].GetText());
    }

    [Fact]
    public void RemoveBlock_RemovesAtIndex()
    {
        var doc = new EditorDocument([new Paragraph("A"), new Paragraph("B")]);
        doc.RemoveBlock(0);

        Assert.Equal(1, doc.BlockCount);
        Assert.Equal("B", doc.Blocks[0].GetText());
    }

    [Fact]
    public void RemoveBlock_DoesNotRemoveLastBlock()
    {
        var doc = new EditorDocument();
        doc.RemoveBlock(0); // Should not remove the only block
        Assert.Equal(1, doc.BlockCount);
    }

    [Fact]
    public void ChangeListener_ReceivesNotifications()
    {
        var doc = new EditorDocument();
        var listener = new TestChangeListener();
        doc.AddChangeListener(listener);

        doc.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.TextInserted,
            new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 5))));

        Assert.Equal(1, listener.ChangeCount);
    }

    [Fact]
    public void ChangedEvent_Fires()
    {
        var doc = new EditorDocument();
        int eventCount = 0;
        doc.Changed += (_, _) => eventCount++;

        doc.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.TextInserted,
            new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 5))));

        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void RemoveChangeListener_StopsNotifications()
    {
        var doc = new EditorDocument();
        var listener = new TestChangeListener();
        doc.AddChangeListener(listener);
        doc.RemoveChangeListener(listener);

        doc.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.TextInserted,
            new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 5))));

        Assert.Equal(0, listener.ChangeCount);
    }

    [Fact]
    public void Block_ParentIsSet()
    {
        var doc = new EditorDocument();
        Assert.Same(doc, doc.Blocks[0].Parent);
    }

    private class TestChangeListener : IDocumentChangeListener
    {
        public int ChangeCount { get; private set; }
        public void OnDocumentChanged(EditorDocument document, DocumentChangedEventArgs e) => ChangeCount++;
    }
}
