using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Events;
using MintedTextEditor.Core.Formatting;

namespace MintedTextEditor.Core.Tests;

public class DocumentImageTrackerTests
{
    private static EditorDocument DocWithText(string text)
    {
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun(text);
        return doc;
    }

    [Fact]
    public void Update_NoChange_IsEmpty()
    {
        var doc = DocWithText("abc");
        var tracker = new DocumentImageTracker();
        tracker.Reset(doc);

        Assert.True(tracker.Update(doc).IsEmpty);
    }

    [Fact]
    public void Update_ReportsInsertedImage()
    {
        var doc = DocWithText("abc");
        var tracker = new DocumentImageTracker();
        tracker.Reset(doc);

        var image = ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 1), "a.png");
        var changes = tracker.Update(doc);

        Assert.Same(image, Assert.Single(changes.Inserted));
        Assert.Empty(changes.Removed);
        Assert.True(tracker.Update(doc).IsEmpty);
    }

    [Fact]
    public void Update_ReportsRemovedImage()
    {
        var doc = DocWithText("abc");
        var image = ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 1), "a.png");
        var tracker = new DocumentImageTracker();
        tracker.Reset(doc);

        ImageEngine.RemoveImage(doc, image);
        var changes = tracker.Update(doc);

        Assert.Empty(changes.Inserted);
        Assert.Equal("a.png", Assert.Single(changes.Removed).Source);
    }

    [Fact]
    public void Update_UndoAndRedoOfInsert_ReportRemovedThenInserted()
    {
        var doc = DocWithText("abc");
        var undo = new UndoManager();
        var tracker = new DocumentImageTracker();
        tracker.Reset(doc);

        undo.Push(new InsertImageAction(doc, new DocumentPosition(0, 0, 1), "a.png"));
        Assert.Single(tracker.Update(doc).Inserted);

        undo.Undo();
        var afterUndo = tracker.Update(doc);
        Assert.Equal("a.png", Assert.Single(afterUndo.Removed).Source);
        Assert.Empty(afterUndo.Inserted);

        undo.Redo();
        var afterRedo = tracker.Update(doc);
        Assert.Equal("a.png", Assert.Single(afterRedo.Inserted).Source);
        Assert.Empty(afterRedo.Removed);
    }

    [Fact]
    public void Update_UndoOfTextDeleteInParagraphWithImage_ReportsNothing()
    {
        // Undo rebuilds the paragraph from a snapshot, creating new ImageInline instances.
        var doc = DocWithText("abcdef");
        ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 3), "a.png");
        var undo = new UndoManager();
        var tracker = new DocumentImageTracker();
        tracker.Reset(doc);

        undo.Push(new DeleteRangeAction(doc,
            new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 2))));
        Assert.True(tracker.Update(doc).IsEmpty);

        undo.Undo();
        Assert.True(tracker.Update(doc).IsEmpty);
        Assert.Single(tracker.Images);
    }

    [Fact]
    public void Update_DeleteRangeContainingImage_ReportsRemoved()
    {
        var doc = DocWithText("abcdef");
        ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 3), "a.png");
        var tracker = new DocumentImageTracker();
        tracker.Reset(doc);
        var para = (Paragraph)doc.Blocks[0];
        var end = new DocumentPosition(0, para.Inlines.Count - 1, 3);

        DocumentEditor.DeleteRange(doc, new TextRange(new DocumentPosition(0, 0, 0), end));
        var changes = tracker.Update(doc);

        Assert.Equal("a.png", Assert.Single(changes.Removed).Source);
        Assert.Empty(changes.Inserted);
    }

    [Fact]
    public void Update_ReplacingWithDifferentSource_ReportsBoth()
    {
        var doc = DocWithText("abc");
        var image = ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 1), "a.png");
        var tracker = new DocumentImageTracker();
        tracker.Reset(doc);

        ImageEngine.RemoveImage(doc, image);
        ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 1), "b.png");
        var changes = tracker.Update(doc);

        Assert.Equal("b.png", Assert.Single(changes.Inserted).Source);
        Assert.Equal("a.png", Assert.Single(changes.Removed).Source);
    }

    [Fact]
    public void Update_ReplaceImage_ReportsOldSourceRemovedAndUpdatedImageInserted()
    {
        var doc = DocWithText("abc");
        var image = ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 1), "old.png");
        var tracker = new DocumentImageTracker();
        tracker.Reset(doc);

        ImageEngine.ReplaceImage(doc, image, "new.png");
        var changes = tracker.Update(doc);

        Assert.Same(image, Assert.Single(changes.Inserted));
        var removed = Assert.Single(changes.Removed);
        Assert.Equal("old.png", removed.Source);
        Assert.Same(image, removed.Image);
        Assert.True(tracker.Update(doc).IsEmpty);
    }

    [Fact]
    public void Update_ReplaceImageWithSameSource_ReportsNothing()
    {
        var doc = DocWithText("abc");
        var image = ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 1), "a.png");
        var tracker = new DocumentImageTracker();
        tracker.Reset(doc);

        ImageEngine.ReplaceImage(doc, image, "a.png");

        Assert.True(tracker.Update(doc).IsEmpty);
    }

    [Fact]
    public void Update_UndoAndRedoOfReplace_ReportSourceSwaps()
    {
        var doc = DocWithText("abc");
        ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 1), "old.png");
        var undo = new UndoManager();
        var tracker = new DocumentImageTracker();
        tracker.Reset(doc);

        undo.Push(new ReplaceImageAction(doc, 0, "new.png"));
        Assert.Equal("new.png", Assert.Single(tracker.Update(doc).Inserted).Source);

        // Undo rebuilds the paragraph from its snapshot: a new instance with the old source.
        undo.Undo();
        var afterUndo = tracker.Update(doc);
        Assert.Equal("new.png", Assert.Single(afterUndo.Removed).Source);
        Assert.Equal("old.png", Assert.Single(afterUndo.Inserted).Source);

        undo.Redo();
        var afterRedo = tracker.Update(doc);
        Assert.Equal("old.png", Assert.Single(afterRedo.Removed).Source);
        Assert.Equal("new.png", Assert.Single(afterRedo.Inserted).Source);
        Assert.True(tracker.Update(doc).IsEmpty);
    }

    /// <summary>Replaces the first image in a paragraph, restoring the paragraph snapshot on undo.</summary>
    private sealed class ReplaceImageAction(EditorDocument doc, int blockIndex, string newSource) : IUndoableAction
    {
        private ParagraphSnapshot? _snapshot;

        public string Description => "Replace Image";

        public DocumentPosition Execute()
        {
            var para = (Paragraph)doc.Blocks[blockIndex];
            _snapshot = SnapshotHelper.Capture(para);
            ImageEngine.ReplaceImage(doc, para.Inlines.OfType<ImageInline>().First(), newSource);
            return new DocumentPosition(blockIndex, 0, 0);
        }

        public DocumentPosition Undo()
        {
            SnapshotHelper.Restore((Paragraph)doc.Blocks[blockIndex], _snapshot!);
            return new DocumentPosition(blockIndex, 0, 0);
        }

        public DocumentPosition Redo() => Execute();

        public bool MergeWith(IUndoableAction next) => false;
    }

    [Fact]
    public void CollectImages_FindsImagesInTablesAndHyperlinks()
    {
        var doc = new EditorDocument();
        var table = new TableBlock(1, 1);
        ((Paragraph)table.GetCell(0, 0)!.Blocks[0]).AddInline(new ImageInline("cell.png"));
        doc.AddBlock(table);
        var para = new Paragraph();
        var link = new HyperlinkInline("https://example.com");
        para.AddInline(link);
        link.AddChild(new ImageInline("link.png"));
        doc.AddBlock(para);

        var sources = DocumentImageTracker.CollectImages(doc).Select(i => i.Source);

        Assert.Equal(new[] { "cell.png", "link.png" }, sources);
    }

    [Fact]
    public void Reset_TakesBaselineWithoutReporting()
    {
        var doc = DocWithText("abc");
        var tracker = new DocumentImageTracker();
        ImageEngine.InsertImage(doc, new DocumentPosition(0, 0, 1), "a.png");

        tracker.Reset(doc);

        Assert.True(tracker.Update(doc).IsEmpty);
    }
}
