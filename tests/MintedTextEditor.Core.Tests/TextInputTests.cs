using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Input;
using MintedTextEditor.Core.Layout;

namespace MintedTextEditor.Core.Tests;

public class TextInputTests
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

    private static EditorKeyEventArgs Key(EditorKey key, InputModifiers mods = InputModifiers.None) =>
        new(key, modifiers: mods);

    // ── HandleTextInput ───────────────────────────────────────────────

    [Fact]
    public void HandleTextInput_InsertsCharacterAtCaret()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 5));

        ctrl.HandleTextInput("!", doc);

        Assert.Equal("Hello!", doc.GetText());
        Assert.Equal(new DocumentPosition(0, 0, 6), caret.Position);
    }

    [Fact]
    public void HandleTextInput_AtMiddleOfRun_InsertsInline()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hllo"));
        caret.MoveTo(new DocumentPosition(0, 0, 1));

        ctrl.HandleTextInput("e", doc);

        Assert.Equal("Hello", doc.GetText());
    }

    [Fact]
    public void HandleTextInput_EmptyString_DoesNothing()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 3));

        ctrl.HandleTextInput("", doc);

        Assert.Equal("Hello", doc.GetText());
        Assert.Equal(new DocumentPosition(0, 0, 3), caret.Position);
    }

    [Fact]
    public void HandleTextInput_WithSelection_ReplacesSelection()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello World"));
        caret.MoveTo(new DocumentPosition(0, 0, 6));
        ctrl.Selection.Set(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 11));
        caret.MoveTo(new DocumentPosition(0, 0, 11));

        ctrl.HandleTextInput("Earth", doc);

        Assert.Equal("Hello Earth", doc.GetText());
        Assert.True(ctrl.Selection.IsEmpty);
    }

    [Fact]
    public void HandleTextInput_AppliesPendingFontStyle_WhenCaretIsCollapsed()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("A"));
        caret.MoveTo(new DocumentPosition(0, 0, 1));
        ctrl.Selection.CollapseTo(caret.Position);
        var fontEngine = new FontFormattingEngine();
        ctrl.FontFormattingEngine = fontEngine;

        fontEngine.ApplyFontFamily(doc, ctrl.Selection.Range, "Courier New");
        ctrl.HandleTextInput("B", doc);

        var para = (Paragraph)doc.Blocks[0];
        var inserted = Assert.IsType<TextRun>(para.Inlines[^1]);
        Assert.Equal("B", inserted.Text);
        Assert.Equal("Courier New", inserted.Style.FontFamily);
    }

    // ── Backspace ─────────────────────────────────────────────────────

    [Fact]
    public void Backspace_DeletesCharacterBeforeCaret()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 5));

        ctrl.HandleKeyDown(Key(EditorKey.Backspace), doc, layout, _context);

        Assert.Equal("Hell", doc.GetText());
        Assert.Equal(new DocumentPosition(0, 0, 4), caret.Position);
    }

    [Fact]
    public void Backspace_AtDocumentStart_DoesNothing()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hi"));
        caret.MoveTo(new DocumentPosition(0, 0, 0));

        ctrl.HandleKeyDown(Key(EditorKey.Backspace), doc, layout, _context);

        Assert.Equal("Hi", doc.GetText());
        Assert.Equal(1, doc.Blocks.Count);
    }

    [Fact]
    public void Backspace_AtBlockStart_MergesWithPreviousBlock()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"), Para("World"));
        caret.MoveTo(new DocumentPosition(1, 0, 0));

        ctrl.HandleKeyDown(Key(EditorKey.Backspace), doc, layout, _context);

        Assert.Equal(1, doc.Blocks.Count);
        Assert.Equal("HelloWorld", doc.GetText());
    }

    [Fact]
    public void Backspace_WithSelection_DeletesSelection()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello World"));
        ctrl.Selection.Set(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 6));
        caret.MoveTo(new DocumentPosition(0, 0, 6));

        ctrl.HandleKeyDown(Key(EditorKey.Backspace), doc, layout, _context);

        Assert.Equal("World", doc.GetText());
        Assert.True(ctrl.Selection.IsEmpty);
    }

    // ── Delete ────────────────────────────────────────────────────────

    [Fact]
    public void Delete_DeletesCharacterAfterCaret()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 0));

        ctrl.HandleKeyDown(Key(EditorKey.Delete), doc, layout, _context);

        Assert.Equal("ello", doc.GetText());
        Assert.Equal(new DocumentPosition(0, 0, 0), caret.Position);
    }

    [Fact]
    public void Delete_AtDocumentEnd_DoesNothing()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hi"));
        caret.MoveTo(new DocumentPosition(0, 0, 2));

        ctrl.HandleKeyDown(Key(EditorKey.Delete), doc, layout, _context);

        Assert.Equal("Hi", doc.GetText());
        Assert.Equal(1, doc.Blocks.Count);
    }

    [Fact]
    public void Delete_AtBlockEnd_MergesWithNextBlock()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"), Para("World"));
        caret.MoveTo(new DocumentPosition(0, 0, 5));

        ctrl.HandleKeyDown(Key(EditorKey.Delete), doc, layout, _context);

        Assert.Equal(1, doc.Blocks.Count);
        Assert.Equal("HelloWorld", doc.GetText());
    }

    [Fact]
    public void Delete_WithSelection_DeletesSelection()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello World"));
        ctrl.Selection.Set(new DocumentPosition(0, 0, 5), new DocumentPosition(0, 0, 11));
        caret.MoveTo(new DocumentPosition(0, 0, 11));

        ctrl.HandleKeyDown(Key(EditorKey.Delete), doc, layout, _context);

        Assert.Equal("Hello", doc.GetText());
        Assert.True(ctrl.Selection.IsEmpty);
    }

    // ── Enter ─────────────────────────────────────────────────────────

    [Fact]
    public void Enter_SplitsBlockAtCaretPosition()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("HelloWorld"));
        caret.MoveTo(new DocumentPosition(0, 0, 5));

        ctrl.HandleKeyDown(Key(EditorKey.Enter), doc, layout, _context);

        Assert.Equal(2, doc.Blocks.Count);
        Assert.Equal("Hello", doc.Blocks[0].GetText());
        Assert.Equal("World", doc.Blocks[1].GetText());
        Assert.Equal(1, caret.Position.BlockIndex);
    }

    [Fact]
    public void Enter_AtBlockEnd_CreatesEmptyBlock()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 5));

        ctrl.HandleKeyDown(Key(EditorKey.Enter), doc, layout, _context);

        Assert.Equal(2, doc.Blocks.Count);
        Assert.Equal("Hello", doc.Blocks[0].GetText());
        Assert.Equal("", doc.Blocks[1].GetText());
        Assert.Equal(new DocumentPosition(1, 0, 0), caret.Position);
    }

    [Fact]
    public void Enter_WithSelection_DeletesThenSplitsBlock()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello World"));
        ctrl.Selection.Set(new DocumentPosition(0, 0, 5), new DocumentPosition(0, 0, 6));
        caret.MoveTo(new DocumentPosition(0, 0, 6));

        ctrl.HandleKeyDown(Key(EditorKey.Enter), doc, layout, _context);

        Assert.Equal(2, doc.Blocks.Count);
        Assert.Equal("Hello", doc.Blocks[0].GetText());
        Assert.Equal("World", doc.Blocks[1].GetText());
    }

    // ── Tab ───────────────────────────────────────────────────────────

    [Fact]
    public void Tab_InsertsTabCharacter()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 5));

        ctrl.HandleKeyDown(Key(EditorKey.Tab), doc, layout, _context);

        Assert.Equal("Hello\t", doc.GetText());
    }

    [Fact]
    public void Tab_InTable_MovesToNextCell()
    {
        var table = new TableBlock(1, 2);
        var (doc, layout, caret, ctrl) = Setup(table);
        caret.MoveTo(new DocumentPosition(0, 0, 0, 0, 0));

        bool handled = ctrl.HandleKeyDown(Key(EditorKey.Tab), doc, layout, _context);

        Assert.True(handled);
        Assert.Equal(new DocumentPosition(0, 0, 0, 0, 1), caret.Position);
    }

    [Fact]
    public void ShiftTab_InTable_MovesToPreviousCell()
    {
        var table = new TableBlock(1, 2);
        var (doc, layout, caret, ctrl) = Setup(table);
        caret.MoveTo(new DocumentPosition(0, 0, 0, 0, 1));

        bool handled = ctrl.HandleKeyDown(Key(EditorKey.Tab, InputModifiers.Shift), doc, layout, _context);

        Assert.True(handled);
        Assert.Equal(new DocumentPosition(0, 0, 0, 0, 0), caret.Position);
    }

    [Fact]
    public void Tab_AtLastTableCell_InsertsParagraphAfterAndMovesCaretOut()
    {
        var table = new TableBlock(1, 1);
        ((Paragraph)table.GetCell(0, 0)!.Blocks[0]).AppendRun("A");
        var (doc, layout, caret, ctrl) = Setup(table);
        caret.MoveTo(new DocumentPosition(0, 0, 1, 0, 0));

        bool handled = ctrl.HandleKeyDown(Key(EditorKey.Tab), doc, layout, _context);

        Assert.True(handled);
        Assert.Equal(2, doc.Blocks.Count);
        Assert.IsType<Paragraph>(doc.Blocks[1]);
        Assert.Equal(new DocumentPosition(1, 0, 0), caret.Position);
    }

    [Fact]
    public void CtrlAltDown_InTable_InsertsRowBelow()
    {
        var table = new TableBlock(1, 1);
        var (doc, layout, caret, ctrl) = Setup(table);
        caret.MoveTo(new DocumentPosition(0, 0, 0, 0, 0));

        bool handled = ctrl.HandleKeyDown(Key(EditorKey.Down, InputModifiers.Control | InputModifiers.Alt), doc, layout, _context);

        Assert.True(handled);
        Assert.Equal(2, table.RowCount);
        Assert.Equal(1, caret.Position.CellRow);
        Assert.Equal(0, caret.Position.CellCol);
    }

    [Fact]
    public void CtrlShiftDelete_InTable_DeletesTable()
    {
        var table = new TableBlock(1, 1);
        var (doc, layout, caret, ctrl) = Setup(table, Para("Tail"));
        caret.MoveTo(new DocumentPosition(0, 0, 0, 0, 0));

        bool handled = ctrl.HandleKeyDown(Key(EditorKey.Delete, InputModifiers.Control | InputModifiers.Shift), doc, layout, _context);

        Assert.True(handled);
        Assert.Single(doc.Blocks);
        Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal(new DocumentPosition(0, 0, 0), caret.Position);
    }

    // ── Key routing ───────────────────────────────────────────────────

    [Fact]
    public void HandleKeyDown_Backspace_ReturnsTrue()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hi"));
        caret.MoveTo(new DocumentPosition(0, 0, 2));

        bool handled = ctrl.HandleKeyDown(Key(EditorKey.Backspace), doc, layout, _context);

        Assert.True(handled);
    }

    [Fact]
    public void HandleKeyDown_Delete_ReturnsTrue()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hi"));
        caret.MoveTo(new DocumentPosition(0, 0, 0));

        bool handled = ctrl.HandleKeyDown(Key(EditorKey.Delete), doc, layout, _context);

        Assert.True(handled);
    }

    [Fact]
    public void HandleKeyDown_Enter_ReturnsTrue()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hi"));
        caret.MoveTo(new DocumentPosition(0, 0, 2));

        bool handled = ctrl.HandleKeyDown(Key(EditorKey.Enter), doc, layout, _context);

        Assert.True(handled);
    }

    [Fact]
    public void HandleKeyDown_CustomKeyBinding_OverridesDefaultHandling()
    {
        var (doc, layout, caret, ctrl) = Setup(Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 5));

        ctrl.KeyBindings.Add(new EditorKeyBinding(
            EditorKey.Tab,
            InputModifiers.None,
            static (_, d, _, _) =>
            {
                DocumentEditor.InsertText(d, new DocumentPosition(0, 0, d.GetText().Length), "[TAB]");
                return true;
            }));

        bool handled = ctrl.HandleKeyDown(Key(EditorKey.Tab), doc, layout, _context);

        Assert.True(handled);
        Assert.Equal("Hello[TAB]", doc.GetText());
    }
}
