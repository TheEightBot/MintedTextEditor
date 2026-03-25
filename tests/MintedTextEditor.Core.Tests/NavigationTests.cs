using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Input;
using MintedTextEditor.Core.Layout;

namespace MintedTextEditor.Core.Tests;

public class NavigationTests
{
    private readonly MockDrawingContext _context = new();
    private readonly TextLayoutEngine _engine = new();

    private static Paragraph Para(params string[] runs)
    {
        var p = new Paragraph();
        foreach (var r in runs) p.AppendRun(r);
        return p;
    }

    private (EditorDocument doc, DocumentLayout layout, Caret caret, EditorInputController ctrl)
        Setup(params Block[] blocks)
    {
        var doc = new EditorDocument(blocks);
        var layout = _engine.Layout(doc, 800f, _context);
        var caret = new Caret();
        var caretRenderer = new CaretRenderer();
        var ctrl = new EditorInputController(caret, caretRenderer);
        return (doc, layout, caret, ctrl);
    }

    // ── MoveLeft ──────────────────────────────────────────────────────

    [Fact]
    public void MoveLeft_WithinInline_DecrementsOffset()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 3));

        ctrl.MoveLeft(doc, layout, _context);

        Assert.Equal(new DocumentPosition(0, 0, 2), caret.Position);
    }

    [Fact]
    public void MoveLeft_AtInlineStart_MovesToPreviousInlineEnd()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello", " World"));
        caret.MoveTo(new DocumentPosition(0, 1, 0)); // start of " World"

        ctrl.MoveLeft(doc, layout, _context);

        Assert.Equal(0, caret.Position.InlineIndex);
        Assert.Equal(5, caret.Position.Offset); // end of "Hello"
    }

    [Fact]
    public void MoveLeft_AtBlockStart_MovesToPreviousBlockEnd()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"), Para("World"));
        caret.MoveTo(new DocumentPosition(1, 0, 0));

        ctrl.MoveLeft(doc, layout, _context);

        Assert.Equal(0, caret.Position.BlockIndex);
        Assert.Equal(5, caret.Position.Offset); // end of "Hello"
    }

    [Fact]
    public void MoveLeft_AtDocumentStart_DoesNotMove()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 0));

        ctrl.MoveLeft(doc, layout, _context);

        Assert.Equal(new DocumentPosition(0, 0, 0), caret.Position);
    }

    // ── MoveRight ────────────────────────────────────────────────────

    [Fact]
    public void MoveRight_WithinInline_IncrementsOffset()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 1));

        ctrl.MoveRight(doc, layout, _context);

        Assert.Equal(new DocumentPosition(0, 0, 2), caret.Position);
    }

    [Fact]
    public void MoveRight_AtBlockEnd_MovesToNextBlockStart()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"), Para("World"));
        caret.MoveTo(new DocumentPosition(0, 0, 5)); // end of "Hello"

        ctrl.MoveRight(doc, layout, _context);

        Assert.Equal(1, caret.Position.BlockIndex);
        Assert.Equal(0, caret.Position.Offset);
    }

    [Fact]
    public void MoveRight_AtDocumentEnd_DoesNotMove()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 5)); // end of "Hello"

        ctrl.MoveRight(doc, layout, _context);

        Assert.Equal(new DocumentPosition(0, 0, 5), caret.Position);
    }

    // ── MoveDocStart / MoveDocEnd ─────────────────────────────────────

    [Fact]
    public void MoveDocStart_AlwaysGoesToZero()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"), Para("World"));
        caret.MoveTo(new DocumentPosition(1, 0, 3));

        ctrl.MoveDocStart();

        Assert.Equal(new DocumentPosition(0, 0, 0), caret.Position);
    }

    [Fact]
    public void MoveDocEnd_GoesToEndOfLastBlock()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"), Para("World"));

        ctrl.MoveDocEnd(doc);

        Assert.Equal(1, caret.Position.BlockIndex);
        Assert.Equal(5, caret.Position.Offset); // "World" = 5 chars
    }

    // ── Word Navigation ──────────────────────────────────────────────

    [Fact]
    public void MoveWordRight_SkipsWordThenWhitespace()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("hello world"));
        caret.MoveTo(new DocumentPosition(0, 0, 0));

        ctrl.MoveWordRight(doc, layout, _context);

        // "hello" (5 chars skipped) + " " (1 non-word char skipped) = offset 6
        Assert.Equal(6, caret.Position.Offset);
    }

    [Fact]
    public void MoveWordLeft_SkipsBackToWordStart()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("hello world"));
        caret.MoveTo(new DocumentPosition(0, 0, 11)); // end of "world"

        ctrl.MoveWordLeft(doc, layout, _context);

        Assert.Equal(6, caret.Position.Offset); // start of "world"
    }

    [Fact]
    public void MoveWordRight_AtBlockEnd_MovesToNextBlockStart()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"), Para("World"));
        caret.MoveTo(new DocumentPosition(0, 0, 5)); // end of block 0

        ctrl.MoveWordRight(doc, layout, _context);

        Assert.Equal(1, caret.Position.BlockIndex);
        Assert.Equal(0, caret.Position.Offset);
    }

    // ── MoveHome / MoveEnd ────────────────────────────────────────────

    [Fact]
    public void MoveHome_MovesToLineStart()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 3));

        ctrl.MoveHome(doc, layout, _context);

        Assert.Equal(0, caret.Position.Offset);
    }

    [Fact]
    public void MoveEnd_MovesToLineEnd()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 0));

        ctrl.MoveEnd(doc, layout, _context);

        Assert.Equal(5, caret.Position.Offset);
    }

    // ── MoveUp / MoveDown boundary ────────────────────────────────────

    [Fact]
    public void MoveUp_OnSingleLineDoc_DoesNotChangeBlockIndex()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 2));

        ctrl.MoveUp(doc, layout, _context);

        Assert.Equal(0, caret.Position.BlockIndex);
    }

    [Fact]
    public void MoveDown_OnSingleLineDoc_DoesNotChangeBlockIndex()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 2));

        ctrl.MoveDown(doc, layout, _context);

        Assert.Equal(0, caret.Position.BlockIndex);
    }

    // ── Internal helpers ──────────────────────────────────────────────

    [Fact]
    public void GetAbsoluteOffset_SingleInline_ReturnsSameOffset()
    {
        var doc = new EditorDocument(new Block[] { Para("Hello") });

        int offset = EditorInputController.GetAbsoluteOffset(doc, new DocumentPosition(0, 0, 3));

        Assert.Equal(3, offset);
    }

    [Fact]
    public void GetAbsoluteOffset_SecondInline_AccumulatesPriorLengths()
    {
        var doc = new EditorDocument(new Block[] { Para("Hello", " World") });

        // inline 1, offset 2 → length("Hello") + 2 = 5 + 2 = 7
        int offset = EditorInputController.GetAbsoluteOffset(doc, new DocumentPosition(0, 1, 2));

        Assert.Equal(7, offset);
    }

    [Fact]
    public void AbsoluteOffsetToPosition_MapsToCorrectInlineAndOffset()
    {
        var doc = new EditorDocument(new Block[] { Para("Hello", " World") });

        // Absolute offset 7 = 5 ("Hello") + 2 into " World" → inline 1, offset 2
        var pos = EditorInputController.AbsoluteOffsetToPosition(doc, 0, 7);

        Assert.Equal(0, pos.BlockIndex);
        Assert.Equal(1, pos.InlineIndex);
        Assert.Equal(2, pos.Offset);
    }

    // ── HandleKeyDown ─────────────────────────────────────────────────

    [Fact]
    public void HandleKeyDown_LeftArrow_MovesCaretLeft()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 3));

        var e = new EditorKeyEventArgs(EditorKey.Left);
        bool handled = ctrl.HandleKeyDown(e, doc, layout, _context);

        Assert.True(handled);
        Assert.Equal(2, caret.Position.Offset);
    }

    [Fact]
    public void HandleKeyDown_KeyUp_ReturnsFalse()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));

        var e = new EditorKeyEventArgs(EditorKey.Left, isKeyDown: false);
        bool handled = ctrl.HandleKeyDown(e, doc, layout, _context);

        Assert.False(handled);
    }

    [Fact]
    public void HandleKeyDown_UnknownKey_ReturnsFalse()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));

        var e = new EditorKeyEventArgs(EditorKey.None);
        bool handled = ctrl.HandleKeyDown(e, doc, layout, _context);

        Assert.False(handled);
    }

    [Fact]
    public void HandleKeyDown_CtrlHome_MovesToDocumentStart()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"), Para("World"));
        caret.MoveTo(new DocumentPosition(1, 0, 3));

        var e = new EditorKeyEventArgs(EditorKey.Home, modifiers: InputModifiers.Control);
        ctrl.HandleKeyDown(e, doc, layout, _context);

        Assert.Equal(new DocumentPosition(0, 0, 0), caret.Position);
    }

    [Fact]
    public void HandleKeyDown_CtrlEnd_MovesToDocumentEnd()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"), Para("World"));
        caret.MoveTo(new DocumentPosition(0, 0, 0));

        var e = new EditorKeyEventArgs(EditorKey.End, modifiers: InputModifiers.Control);
        ctrl.HandleKeyDown(e, doc, layout, _context);

        Assert.Equal(1, caret.Position.BlockIndex);
        Assert.Equal(5, caret.Position.Offset);
    }

    // ── HandlePointerDown ─────────────────────────────────────────────

    [Fact]
    public void HandlePointerDown_SetsCaretToHitPosition()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));

        // 16px / 8px per char (MockDrawingContext.CharWidth) = offset 2
        var e = new EditorPointerEventArgs(16f, 5f, InputAction.Pressed);
        ctrl.HandlePointerDown(e, doc, layout, _context);

        Assert.Equal(2, caret.Position.Offset);
    }

    [Fact]
    public void HandlePointerDown_NonPressedAction_DoesNotMoveCaret()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 3));

        var e = new EditorPointerEventArgs(16f, 5f, InputAction.Released);
        ctrl.HandlePointerDown(e, doc, layout, _context);

        Assert.Equal(3, caret.Position.Offset); // unchanged
    }
}
