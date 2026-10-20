using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Input;

namespace MintedTextEditor.Core.Tests;

public class ImeTextContextTests
{
    private static Paragraph Para(params string[] runs)
    {
        var p = new Paragraph();
        foreach (var r in runs) p.AppendRun(r);
        return p;
    }

    private static (EditorDocument doc, Caret caret, EditorInputController ctrl) Setup(params Block[] blocks)
    {
        var doc = new EditorDocument(blocks);
        var caret = new Caret();
        var ctrl = new EditorInputController(caret, new CaretRenderer());
        return (doc, caret, ctrl);
    }

    [Fact]
    public void Context_CaretMidParagraph_ReportsParagraphTextAndOffset()
    {
        var (doc, caret, ctrl) = Setup(Para("First"), Para("Hello ", "world"));
        caret.MoveTo(new DocumentPosition(1, 1, 2));

        var ctx = ctrl.GetImeTextContext(doc);

        Assert.Equal("Hello world", ctx.ParagraphText);
        Assert.Equal(8, ctx.SelectionStart);
        Assert.Equal(8, ctx.SelectionEnd);
        Assert.Equal("Hello wo", ctx.TextBeforeCursor(100));
        Assert.Equal("lo wo", ctx.TextBeforeCursor(5));
        Assert.Equal("rl", ctx.TextAfterCursor(2));
    }

    [Fact]
    public void Context_SelectionAcrossRuns_ReportsRange()
    {
        var (doc, caret, ctrl) = Setup(Para("Hello ", "world"));
        ctrl.Selection.Set(new DocumentPosition(0, 0, 3), new DocumentPosition(0, 1, 2));
        caret.MoveTo(new DocumentPosition(0, 1, 2));

        var ctx = ctrl.GetImeTextContext(doc);

        Assert.Equal(3, ctx.SelectionStart);
        Assert.Equal(8, ctx.SelectionEnd);
        Assert.Equal("lo wo", ctx.SelectedText);
    }

    [Fact]
    public void Context_SelectionLeavingParagraph_IsClamped()
    {
        var (doc, caret, ctrl) = Setup(Para("abc"), Para("defg"));
        ctrl.Selection.Set(new DocumentPosition(0, 0, 1), new DocumentPosition(1, 0, 2));
        caret.MoveTo(new DocumentPosition(1, 0, 2));

        var ctx = ctrl.GetImeTextContext(doc);

        Assert.Equal("defg", ctx.ParagraphText);
        Assert.Equal(0, ctx.SelectionStart);
        Assert.Equal(2, ctx.SelectionEnd);
    }

    [Fact]
    public void Context_InTableCell_UsesCellText()
    {
        var table = new TableBlock(1, 2);
        var (doc, caret, ctrl) = Setup(table);
        caret.MoveTo(new DocumentPosition(0, 0, 0, 0, 1));
        ctrl.HandleTextInput("cell", doc);

        var ctx = ctrl.GetImeTextContext(doc);

        Assert.Equal("cell", ctx.ParagraphText);
        Assert.Equal(4, ctx.SelectionStart);
    }

    [Fact]
    public void DeleteImeTextBefore_DeletesExactUnits_AcrossRuns()
    {
        var (doc, caret, ctrl) = Setup(Para("Hello ", "world"));
        caret.MoveTo(new DocumentPosition(0, 1, 2));

        ctrl.DeleteImeTextBefore(4, doc);

        Assert.Equal("Hellrld", doc.GetText());
        Assert.Equal(4, ctrl.GetImeTextContext(doc).SelectionStart);
    }

    [Fact]
    public void DeleteImeTextBefore_ClampsAtParagraphStart_NoMerge()
    {
        var (doc, caret, ctrl) = Setup(Para("ab"), Para("cd"));
        caret.MoveTo(new DocumentPosition(1, 0, 1));

        ctrl.DeleteImeTextBefore(5, doc);

        Assert.Equal(2, doc.Blocks.Count);
        Assert.Equal("d", doc.Blocks[1].GetText());
    }

    [Fact]
    public void DeleteImeTextBefore_WithSelection_KeepsSelectedText()
    {
        var (doc, caret, ctrl) = Setup(Para("abcdef"));
        ctrl.Selection.Set(new DocumentPosition(0, 0, 2), new DocumentPosition(0, 0, 4));
        caret.MoveTo(new DocumentPosition(0, 0, 4));

        ctrl.DeleteImeTextBefore(1, doc);
        ctrl.DeleteImeTextAfter(1, doc);

        Assert.Equal("acdf", doc.GetText());
        var ctx = ctrl.GetImeTextContext(doc);
        Assert.Equal("cd", ctx.SelectedText);
    }

    [Fact]
    public void DeleteImeTextBefore_IsExact_EvenInsideSurrogatePair()
    {
        // Composition diffs work in UTF-16 units; the tracker guarantees pair-safe counts.
        var (doc, caret, ctrl) = Setup(Para("a😀"));
        caret.MoveTo(new DocumentPosition(0, 0, 3));

        ctrl.DeleteImeTextBefore(2, doc);

        Assert.Equal("a", doc.GetText());
    }

    [Fact]
    public void SetImeSelection_MovesCaretWithinParagraph()
    {
        var (doc, caret, ctrl) = Setup(Para("one"), Para("hello"));
        caret.MoveTo(new DocumentPosition(1, 0, 5));

        ctrl.SetImeSelection(2, 2, doc);

        Assert.Equal(new DocumentPosition(1, 0, 2), caret.Position);
        Assert.True(ctrl.Selection.IsEmpty);
    }

    [Fact]
    public void TrackerOverController_ReComposeExistingWord_DoesNotDuplicate()
    {
        // End-to-end R2: Samsung adopts "hello" as the composing region, then edits it.
        var (doc, caret, ctrl) = Setup(Para("say hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 9));
        var sink = new ControllerSink(ctrl, doc);
        var tracker = new ImeCompositionTracker(sink);

        tracker.SetComposingRegion(4, 9, ctrl.GetImeTextContext(doc));
        tracker.SetComposingText("hello");
        tracker.SetComposingText("help");
        tracker.CommitText("help ");

        Assert.Equal("say help ", doc.GetText());
    }

    private sealed class ControllerSink(EditorInputController ctrl, EditorDocument doc) : IImeEditSink
    {
        public void InsertText(string text) => ctrl.HandleTextInput(text, doc);
        public void DeleteBefore(int count) => ctrl.DeleteImeTextBefore(count, doc);
        public void DeleteAfter(int count) => ctrl.DeleteImeTextAfter(count, doc);
        public void SetSelection(int start, int end) => ctrl.SetImeSelection(start, end, doc);
        public void Enter() => ctrl.HandleTextInput("\n", doc);
    }
}
