using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Input;
using MintedTextEditor.Core.Layout;

namespace MintedTextEditor.Core.Tests;

public class UndoRedoTests
{
    private readonly MockDrawingContext _context = new();
    private readonly TextLayoutEngine _engine = new();

    private static Paragraph Para(string text) => new Paragraph(text);

    private (EditorDocument doc, DocumentLayout layout, Caret caret, EditorInputController ctrl, UndoManager undo)
        Setup(params Block[] blocks)
    {
        var doc = blocks.Length > 0 ? new EditorDocument(blocks) : new EditorDocument();
        var layout = _engine.Layout(doc, 800f, _context);
        var caret = new Caret();
        var caretRenderer = new CaretRenderer();
        var undo = new UndoManager();
        var ctrl = new EditorInputController(caret, caretRenderer) { UndoManager = undo };
        return (doc, layout, caret, ctrl, undo);
    }

    // ── UndoManager core ──────────────────────────────────────────────────────

    [Fact]
    public void UndoManager_InitialState_CanNotUndoOrRedo()
    {
        var undo = new UndoManager();
        Assert.False(undo.CanUndo);
        Assert.False(undo.CanRedo);
    }

    [Fact]
    public void UndoManager_Push_CanUndo()
    {
        var doc = new EditorDocument();
        var undo = new UndoManager();
        var action = new InsertTextAction(doc, new DocumentPosition(0, 0, 0), "Hello");
        undo.Push(action);
        Assert.True(undo.CanUndo);
    }

    [Fact]
    public void UndoManager_Push_ClearsRedoStack()
    {
        var doc = new EditorDocument();
        var undo = new UndoManager();

        // Push, undo (now redo available), push again
        undo.Push(new InsertTextAction(doc, new DocumentPosition(0, 0, 0), "Hello"));
        undo.Undo();
        Assert.True(undo.CanRedo);

        undo.Push(new InsertTextAction(doc, new DocumentPosition(0, 0, 0), "World"));
        Assert.False(undo.CanRedo);
    }

    [Fact]
    public void UndoManager_MaxDepth_EvictsOldestEntry()
    {
        // Use 4 separate paragraphs so inserts don't merge (different blocks)
        var doc = new EditorDocument([Para("A"), Para("B"), Para("C"), Para("D")]);
        var undo = new UndoManager { MaxDepth = 3 };

        // Push 4 actions into separate blocks so none merge; the first should be evicted
        for (int i = 0; i < 4; i++)
            undo.Push(new InsertTextAction(doc, new DocumentPosition(i, 0, 0), "x"));

        // Can undo only 3 times
        Assert.True(undo.CanUndo);
        undo.Undo();
        Assert.True(undo.CanUndo);
        undo.Undo();
        Assert.True(undo.CanUndo);
        undo.Undo();
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void UndoManager_UndoStackChanged_FiresOnPush()
    {
        var doc = new EditorDocument();
        var undo = new UndoManager();
        int events = 0;
        undo.UndoStackChanged += (_, _) => events++;

        undo.Push(new InsertTextAction(doc, new DocumentPosition(0, 0, 0), "Hi"));
        Assert.Equal(1, events);
    }

    [Fact]
    public void UndoManager_UndoStackChanged_FiresOnUndo()
    {
        var doc = new EditorDocument();
        var undo = new UndoManager();
        undo.Push(new InsertTextAction(doc, new DocumentPosition(0, 0, 0), "Hi"));

        int events = 0;
        undo.UndoStackChanged += (_, _) => events++;
        undo.Undo();
        Assert.Equal(1, events);
    }

    [Fact]
    public void UndoManager_Undo_WhenEmpty_ReturnsNull()
    {
        var undo = new UndoManager();
        Assert.Null(undo.Undo());
    }

    [Fact]
    public void UndoManager_Redo_WhenEmpty_ReturnsNull()
    {
        var undo = new UndoManager();
        Assert.Null(undo.Redo());
    }

    [Fact]
    public void UndoManager_Clear_EmptiesBothStacks()
    {
        var doc = new EditorDocument();
        var undo = new UndoManager();
        undo.Push(new InsertTextAction(doc, new DocumentPosition(0, 0, 0), "Hi"));
        undo.Undo();
        Assert.True(undo.CanRedo);

        undo.Clear();
        Assert.False(undo.CanUndo);
        Assert.False(undo.CanRedo);
    }

    // ── InsertTextAction ──────────────────────────────────────────────────────

    [Fact]
    public void InsertTextAction_Execute_InsertsText()
    {
        var doc = new EditorDocument();
        var undo = new UndoManager();
        undo.Push(new InsertTextAction(doc, new DocumentPosition(0, 0, 0), "Hello"));
        Assert.Equal("Hello", doc.GetText());
    }

    [Fact]
    public void InsertTextAction_Undo_RestoresOriginalContent()
    {
        var doc = new EditorDocument();
        var undo = new UndoManager();
        undo.Push(new InsertTextAction(doc, new DocumentPosition(0, 0, 0), "Hello"));
        Assert.Equal("Hello", doc.GetText());

        undo.Undo();
        Assert.Equal("", doc.GetText());
    }

    [Fact]
    public void InsertTextAction_Undo_ReturnsOriginalPosition()
    {
        var doc = new EditorDocument();
        var undo = new UndoManager();
        var insertPos = new DocumentPosition(0, 0, 0);
        undo.Push(new InsertTextAction(doc, insertPos, "Hello"));

        var restoredPos = undo.Undo();
        Assert.Equal(insertPos, restoredPos);
    }

    [Fact]
    public void InsertTextAction_UndoThenRedo_RoundTrip()
    {
        var doc = new EditorDocument();
        var undo = new UndoManager();
        undo.Push(new InsertTextAction(doc, new DocumentPosition(0, 0, 0), "Hello"));

        undo.Undo();
        Assert.Equal("", doc.GetText());

        undo.Redo();
        Assert.Equal("Hello", doc.GetText());
    }

    [Fact]
    public void InsertTextAction_MergeWith_GroupsConsecutiveInserts()
    {
        var doc = new EditorDocument();
        var undo = new UndoManager();

        // Simulate typing three characters one by one
        var pos0 = new DocumentPosition(0, 0, 0);
        undo.Push(new InsertTextAction(doc, pos0, "H"));
        var pos1 = new DocumentPosition(0, 0, 1);
        undo.Push(new InsertTextAction(doc, pos1, "i"));
        var pos2 = new DocumentPosition(0, 0, 2);
        undo.Push(new InsertTextAction(doc, pos2, "!"));

        Assert.Equal("Hi!", doc.GetText());

        // All three chars should merge into one undo step
        undo.Undo();
        Assert.Equal("", doc.GetText());
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void InsertTextAction_NoMerge_WhenPositionGap()
    {
        var doc = new EditorDocument(new[] { Para("Hello World") });
        var undo = new UndoManager();

        // Insert at two non-adjacent positions — should NOT merge
        undo.Push(new InsertTextAction(doc, new DocumentPosition(0, 0, 0), "X"));
        undo.Push(new InsertTextAction(doc, new DocumentPosition(0, 0, 7), "Y"));

        // Two separate undo steps
        undo.Undo();
        Assert.True(undo.CanUndo);
        undo.Undo();
        Assert.False(undo.CanUndo);
    }

    // ── DeleteRangeAction ─────────────────────────────────────────────────────

    [Fact]
    public void DeleteRangeAction_Execute_RemovesText()
    {
        var doc = new EditorDocument(new[] { Para("Hello World") });
        var undo = new UndoManager();
        var range = new TextRange(new DocumentPosition(0, 0, 5), new DocumentPosition(0, 0, 11));

        undo.Push(new DeleteRangeAction(doc, range));
        Assert.Equal("Hello", doc.GetText());
    }

    [Fact]
    public void DeleteRangeAction_Undo_RestoresDeletedText()
    {
        var doc = new EditorDocument(new[] { Para("Hello World") });
        var undo = new UndoManager();
        var range = new TextRange(new DocumentPosition(0, 0, 5), new DocumentPosition(0, 0, 11));

        undo.Push(new DeleteRangeAction(doc, range));
        Assert.Equal("Hello", doc.GetText());

        undo.Undo();
        Assert.Equal("Hello World", doc.GetText());
    }

    [Fact]
    public void DeleteRangeAction_UndoThenRedo_RoundTrip()
    {
        var doc = new EditorDocument(new[] { Para("Hello World") });
        var undo = new UndoManager();
        var range = new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 5));

        undo.Push(new DeleteRangeAction(doc, range));
        undo.Undo();
        Assert.Equal("Hello World", doc.GetText());

        undo.Redo();
        Assert.Equal(" World", doc.GetText());
    }

    // ── SplitBlockAction ──────────────────────────────────────────────────────

    [Fact]
    public void SplitBlockAction_Execute_CreatesTwoBlocks()
    {
        var doc = new EditorDocument(new[] { Para("HelloWorld") });
        var undo = new UndoManager();

        undo.Push(new SplitBlockAction(doc, new DocumentPosition(0, 0, 5)));
        Assert.Equal(2, doc.Blocks.Count);
        Assert.Equal("Hello", doc.Blocks[0].GetText());
        Assert.Equal("World", doc.Blocks[1].GetText());
    }

    [Fact]
    public void SplitBlockAction_Undo_MergesBlocksBack()
    {
        var doc = new EditorDocument(new[] { Para("HelloWorld") });
        var undo = new UndoManager();

        undo.Push(new SplitBlockAction(doc, new DocumentPosition(0, 0, 5)));
        Assert.Equal(2, doc.Blocks.Count);

        undo.Undo();
        Assert.Equal(1, doc.Blocks.Count);
        Assert.Equal("HelloWorld", doc.GetText());
    }

    [Fact]
    public void SplitBlockAction_UndoThenRedo_RoundTrip()
    {
        var doc = new EditorDocument(new[] { Para("HelloWorld") });
        var undo = new UndoManager();

        undo.Push(new SplitBlockAction(doc, new DocumentPosition(0, 0, 5)));
        undo.Undo();
        Assert.Equal(1, doc.Blocks.Count);

        undo.Redo();
        Assert.Equal(2, doc.Blocks.Count);
        Assert.Equal("Hello", doc.Blocks[0].GetText());
        Assert.Equal("World", doc.Blocks[1].GetText());
    }

    // ── MergeBlocksAction ─────────────────────────────────────────────────────

    [Fact]
    public void MergeBlocksAction_Execute_CombinesBlocks()
    {
        var doc = new EditorDocument(new[] { Para("Hello"), Para("World") });
        var undo = new UndoManager();

        undo.Push(new MergeBlocksAction(doc, 0));
        Assert.Equal(1, doc.Blocks.Count);
        Assert.Equal("HelloWorld", doc.GetText());
    }

    [Fact]
    public void MergeBlocksAction_Undo_RestoresTwoBlocks()
    {
        var doc = new EditorDocument(new[] { Para("Hello"), Para("World") });
        var undo = new UndoManager();

        undo.Push(new MergeBlocksAction(doc, 0));
        undo.Undo();

        Assert.Equal(2, doc.Blocks.Count);
        Assert.Equal("Hello", doc.Blocks[0].GetText());
        Assert.Equal("World", doc.Blocks[1].GetText());
    }

    [Fact]
    public void MergeBlocksAction_UndoThenRedo_RoundTrip()
    {
        var doc = new EditorDocument(new[] { Para("Hello"), Para("World") });
        var undo = new UndoManager();

        undo.Push(new MergeBlocksAction(doc, 0));
        undo.Undo();
        Assert.Equal(2, doc.Blocks.Count);

        undo.Redo();
        Assert.Equal(1, doc.Blocks.Count);
        Assert.Equal("HelloWorld", doc.GetText());
    }

    // ── ApplyStyleAction ──────────────────────────────────────────────────────

    [Fact]
    public void ApplyStyleAction_Execute_AppliesStyle()
    {
        var doc = new EditorDocument(new[] { Para("Hello") });
        var undo = new UndoManager();
        var range = new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 5));

        undo.Push(new ApplyStyleAction(doc, range, s => s.WithBold(true)));

        var para = (Paragraph)doc.Blocks[0];
        var run = (TextRun)para.Inlines[0];
        Assert.True(run.Style.IsBold);
    }

    [Fact]
    public void ApplyStyleAction_Undo_RestoresOriginalStyle()
    {
        var doc = new EditorDocument(new[] { Para("Hello") });
        var undo = new UndoManager();
        var range = new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 5));

        undo.Push(new ApplyStyleAction(doc, range, s => s.WithBold(true)));

        var para = (Paragraph)doc.Blocks[0];
        var run = (TextRun)para.Inlines[0];
        Assert.True(run.Style.IsBold);

        undo.Undo();
        para = (Paragraph)doc.Blocks[0];
        run = (TextRun)para.Inlines[0];
        Assert.False(run.Style.IsBold);
    }

    [Fact]
    public void ApplyStyleAction_UndoThenRedo_RoundTrip()
    {
        var doc = new EditorDocument(new[] { Para("Hello") });
        var undo = new UndoManager();
        var range = new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 5));

        undo.Push(new ApplyStyleAction(doc, range, s => s.WithBold(true)));
        undo.Undo();

        var para = (Paragraph)doc.Blocks[0];
        Assert.False(((TextRun)para.Inlines[0]).Style.IsBold);

        undo.Redo();
        para = (Paragraph)doc.Blocks[0];
        Assert.True(((TextRun)para.Inlines[0]).Style.IsBold);
    }

    // ── CompositeAction ───────────────────────────────────────────────────────

    [Fact]
    public void CompositeAction_Execute_RunsAllActions()
    {
        var doc = new EditorDocument(new[] { Para("Hello World") });
        var undo = new UndoManager();

        var range = new TextRange(new DocumentPosition(0, 0, 5), new DocumentPosition(0, 0, 11));
        var deleteAction = new DeleteRangeAction(doc, range);
        var insertAction = new InsertTextAction(doc, new DocumentPosition(0, 0, 5), " Earth");
        var composite = new CompositeAction(new IUndoableAction[] { deleteAction, insertAction });

        undo.Push(composite);
        Assert.Equal("Hello Earth", doc.GetText());
    }

    [Fact]
    public void CompositeAction_Undo_ReversesAllActions()
    {
        var doc = new EditorDocument(new[] { Para("Hello World") });
        var undo = new UndoManager();

        var range = new TextRange(new DocumentPosition(0, 0, 5), new DocumentPosition(0, 0, 11));
        var deleteAction = new DeleteRangeAction(doc, range);
        var insertAction = new InsertTextAction(doc, new DocumentPosition(0, 0, 5), " Earth");
        var composite = new CompositeAction(new IUndoableAction[] { deleteAction, insertAction });

        undo.Push(composite);
        undo.Undo();
        Assert.Equal("Hello World", doc.GetText());
    }

    // ── Keyboard shortcut integration ────────────────────────────────────────

    [Fact]
    public void HandleKeyDown_CtrlZ_TriggersUndo()
    {
        var (doc, layout, caret, ctrl, undo) = Setup(Para("Hello World"));

        // Place some text to give the undo manager something to undo
        undo.Push(new InsertTextAction(doc, new DocumentPosition(0, 0, 0), "Test"));
        Assert.True(undo.CanUndo);

        var pressed = ctrl.HandleKeyDown(
            new EditorKeyEventArgs(EditorKey.Z, '\0', InputModifiers.Control),
            doc, layout, _context);

        Assert.True(pressed);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void HandleKeyDown_CtrlZ_ReturnsFalse_WhenNothingToUndo()
    {
        var (doc, layout, caret, ctrl, undo) = Setup(Para("Hello"));

        var pressed = ctrl.HandleKeyDown(
            new EditorKeyEventArgs(EditorKey.Z, '\0', InputModifiers.Control),
            doc, layout, _context);

        Assert.False(pressed);
    }

    [Fact]
    public void HandleKeyDown_CtrlY_TriggersRedo()
    {
        var (doc, layout, caret, ctrl, undo) = Setup(Para("Hello World"));

        undo.Push(new InsertTextAction(doc, new DocumentPosition(0, 0, 0), "Test"));
        undo.Undo();
        Assert.True(undo.CanRedo);

        var pressed = ctrl.HandleKeyDown(
            new EditorKeyEventArgs(EditorKey.Y, '\0', InputModifiers.Control),
            doc, layout, _context);

        Assert.True(pressed);
        Assert.False(undo.CanRedo);
    }

    [Fact]
    public void HandleKeyDown_CtrlShiftZ_TriggersRedo()
    {
        var (doc, layout, caret, ctrl, undo) = Setup(Para("Hello World"));

        undo.Push(new InsertTextAction(doc, new DocumentPosition(0, 0, 0), "Test"));
        undo.Undo();
        Assert.True(undo.CanRedo);

        var pressed = ctrl.HandleKeyDown(
            new EditorKeyEventArgs(EditorKey.Z, '\0', InputModifiers.Control | InputModifiers.Shift),
            doc, layout, _context);

        Assert.True(pressed);
        Assert.False(undo.CanRedo);
    }

    [Fact]
    public void HandleKeyDown_CtrlY_ReturnsFalse_WhenNothingToRedo()
    {
        var (doc, layout, caret, ctrl, undo) = Setup(Para("Hello"));

        var pressed = ctrl.HandleKeyDown(
            new EditorKeyEventArgs(EditorKey.Y, '\0', InputModifiers.Control),
            doc, layout, _context);

        Assert.False(pressed);
    }

    [Fact]
    public void HandleKeyDown_Undo_UpdatesCaret()
    {
        var (doc, layout, caret, ctrl, undo) = Setup(Para("Hello"));

        var startPos = new DocumentPosition(0, 0, 0);
        undo.Push(new InsertTextAction(doc, startPos, "Test"));
        // Caret is at position after "Test" was inserted
        caret.MoveTo(new DocumentPosition(0, 0, 4));

        ctrl.HandleKeyDown(
            new EditorKeyEventArgs(EditorKey.Z, '\0', InputModifiers.Control),
            doc, layout, _context);

        // After undo caret should return to the original insert position
        Assert.Equal(startPos, caret.Position);
    }
}
