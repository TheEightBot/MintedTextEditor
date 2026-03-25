using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Input;
using MintedTextEditor.Core.Layout;

namespace MintedTextEditor.Core.Tests;

internal sealed class MockClipboardProvider : IClipboardProvider
{
    public string? Contents { get; private set; }
    public MockClipboardProvider(string? initial = null) => Contents = initial;
    public Task SetTextAsync(string text) { Contents = text; return Task.CompletedTask; }
    public Task<string?> GetTextAsync() => Task.FromResult(Contents);
}

public class ClipboardTests
{
    private readonly MockDrawingContext _context = new();
    private readonly TextLayoutEngine _engine = new();

    private static Paragraph Para(params string[] runs)
    {
        var p = new Paragraph();
        foreach (var r in runs) p.AppendRun(r);
        return p;
    }

    private (EditorDocument doc, DocumentLayout layout, Caret caret, EditorInputController ctrl, MockClipboardProvider clipboard)
        Setup(string? clipboardContent = null, params Block[] blocks)
    {
        var doc = new EditorDocument(blocks);
        var layout = _engine.Layout(doc, 800f, _context);
        var caret = new Caret();
        var caretRenderer = new CaretRenderer();
        var clipboard = new MockClipboardProvider(clipboardContent);
        var ctrl = new EditorInputController(caret, caretRenderer) { ClipboardProvider = clipboard };
        return (doc, layout, caret, ctrl, clipboard);
    }

    [Fact]
    public async Task Copy_WritesSelectedTextToClipboard()
    {
        var doc = new EditorDocument(new[] { Para("Hello World") });
        var clipboard = new MockClipboardProvider();
        var range = new TextRange(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 11));
        await ClipboardOperations.CopyAsync(doc, range, clipboard);
        Assert.Equal("World", clipboard.Contents);
    }

    [Fact]
    public async Task Copy_DoesNotAlterDocument()
    {
        var doc = new EditorDocument(new[] { Para("Hello World") });
        var clipboard = new MockClipboardProvider();
        var range = new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 5));
        await ClipboardOperations.CopyAsync(doc, range, clipboard);
        Assert.Equal("Hello World", doc.GetText());
    }

    [Fact]
    public async Task Cut_WritesSelectedTextToClipboard_AndRemovesFromDocument()
    {
        var doc = new EditorDocument(new[] { Para("Hello World") });
        var clipboard = new MockClipboardProvider();
        var range = new TextRange(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 11));
        var newPos = await ClipboardOperations.CutAsync(doc, range, clipboard);
        Assert.Equal("World", clipboard.Contents);
        Assert.Equal("Hello ", doc.GetText());
        Assert.Equal(new DocumentPosition(0, 0, 6), newPos);
    }

    [Fact]
    public async Task Paste_InsertsClipboardTextAtPosition()
    {
        var doc = new EditorDocument(new[] { Para("Hello") });
        var clipboard = new MockClipboardProvider(" World");
        var pos = new DocumentPosition(0, 0, 5);
        var emptyRange = new TextRange(pos, pos);
        var newPos = await ClipboardOperations.PasteAsync(doc, pos, emptyRange, clipboard);
        Assert.Equal("Hello World", doc.GetText());
        Assert.NotNull(newPos);
    }

    [Fact]
    public async Task Paste_ReplacesSelectionWithClipboardText()
    {
        var doc = new EditorDocument(new[] { Para("Hello Earth") });
        var clipboard = new MockClipboardProvider("World");
        var selectPos = new DocumentPosition(0, 0, 6);
        var endPos = new DocumentPosition(0, 0, 11);
        var range = new TextRange(selectPos, endPos);
        await ClipboardOperations.PasteAsync(doc, selectPos, range, clipboard);
        Assert.Equal("Hello World", doc.GetText());
    }

    [Fact]
    public async Task Paste_WhenClipboardEmpty_ReturnsNull()
    {
        var doc = new EditorDocument(new[] { Para("Hello") });
        var clipboard = new MockClipboardProvider(null);
        var pos = new DocumentPosition(0, 0, 5);
        var emptyRange = new TextRange(pos, pos);
        var result = await ClipboardOperations.PasteAsync(doc, pos, emptyRange, clipboard);
        Assert.Null(result);
        Assert.Equal("Hello", doc.GetText());
    }

    [Fact]
    public async Task HandleCopyAsync_CopiesSelectionToClipboard()
    {
        var (doc, _, caret, ctrl, clipboard) = Setup(null, Para("Hello World"));
        caret.MoveTo(new DocumentPosition(0, 0, 6));
        ctrl.Selection.Set(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 11));
        await ctrl.HandleCopyAsync(doc);
        Assert.Equal("World", clipboard.Contents);
        Assert.Equal("Hello World", doc.GetText());
    }

    [Fact]
    public async Task HandleCopyAsync_EmptySelection_DoesNothing()
    {
        var (doc, _, _, ctrl, clipboard) = Setup(null, Para("Hello"));
        await ctrl.HandleCopyAsync(doc);
        Assert.Null(clipboard.Contents);
    }

    [Fact]
    public async Task HandleCutAsync_CutsSelectionToClipboard()
    {
        var (doc, _, caret, ctrl, clipboard) = Setup(null, Para("Hello World"));
        caret.MoveTo(new DocumentPosition(0, 0, 11));
        ctrl.Selection.Set(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 11));
        await ctrl.HandleCutAsync(doc);
        Assert.Equal("World", clipboard.Contents);
        Assert.Equal("Hello ", doc.GetText());
        Assert.True(ctrl.Selection.IsEmpty);
    }

    [Fact]
    public async Task HandlePasteAsync_InsertsAtCaret()
    {
        var (doc, _, caret, ctrl, _) = Setup(" World", Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 5));
        await ctrl.HandlePasteAsync(doc);
        Assert.Equal("Hello World", doc.GetText());
        Assert.Equal(new DocumentPosition(0, 0, 11), caret.Position);
    }

    [Fact]
    public async Task HandlePasteAsync_ReplacesActiveSelection()
    {
        var (doc, _, caret, ctrl, _) = Setup("Earth", Para("Hello World"));
        caret.MoveTo(new DocumentPosition(0, 0, 11));
        ctrl.Selection.Set(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 11));
        await ctrl.HandlePasteAsync(doc);
        Assert.Equal("Hello Earth", doc.GetText());
        Assert.True(ctrl.Selection.IsEmpty);
    }

    [Fact]
    public async Task HandlePasteAsync_WhenClipboardEmpty_DoesNothing()
    {
        var (doc, _, caret, ctrl, _) = Setup(null, Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 5));
        await ctrl.HandlePasteAsync(doc);
        Assert.Equal("Hello", doc.GetText());
    }

    [Fact]
    public void HandleKeyDown_CtrlC_WithSelectionAndProvider_ReturnsTrue()
    {
        var (doc, layout, caret, ctrl, _) = Setup(null, Para("Hello"));
        caret.MoveTo(new DocumentPosition(0, 0, 5));
        ctrl.Selection.Set(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 5));
        bool handled = ctrl.HandleKeyDown(
            new EditorKeyEventArgs(EditorKey.C, modifiers: InputModifiers.Control),
            doc, layout, _context);
        Assert.True(handled);
    }

    [Fact]
    public void HandleKeyDown_CtrlC_WithoutProvider_NotHandled()
    {
        var doc = new EditorDocument(new[] { Para("Hello") });
        var layout = _engine.Layout(doc, 800f, _context);
        var caret = new Caret();
        var ctrl = new EditorInputController(caret, new CaretRenderer());
        bool handled = ctrl.HandleKeyDown(
            new EditorKeyEventArgs(EditorKey.C, modifiers: InputModifiers.Control),
            doc, layout, _context);
        Assert.False(handled);
    }
}
