using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Input;
using MintedTextEditor.Core.Layout;
using MintedTextEditor.Core.Toolbar;

namespace MintedTextEditor.Core.Tests;

/// <summary>
/// A pending style (a format picked at a collapsed caret) applies only at the caret where it was
/// chosen, and plain-text paste uses the same insertion style as typing.
/// </summary>
public class PendingStyleTests
{
    private static readonly TextStyle Bold = TextStyle.Default.WithBold(true);

    private static Paragraph Para(params (string Text, TextStyle Style)[] runs)
    {
        var p = new Paragraph();
        foreach (var (text, style) in runs) p.AppendRun(text, style);
        return p;
    }

    private sealed record Editor(
        EditorDocument Doc, DocumentLayout Layout, MockDrawingContext Context, Caret Caret,
        EditorInputController Ctrl, FormattingEngine Fmt, FontFormattingEngine FontFmt);

    private static Editor Setup(string? clipboard, params Block[] blocks)
    {
        var doc = new EditorDocument(blocks);
        var context = new MockDrawingContext();
        var layout = new TextLayoutEngine().Layout(doc, 800f, context);
        var caret = new Caret();
        var fmt = new FormattingEngine();
        var fontFmt = new FontFormattingEngine();
        var ctrl = new EditorInputController(caret, new CaretRenderer())
        {
            FormattingEngine = fmt,
            FontFormattingEngine = fontFmt,
            ClipboardProvider = new MockClipboardProvider(clipboard),
        };
        return new Editor(doc, layout, context, caret, ctrl, fmt, fontFmt);
    }

    private static void PlaceCaret(Editor e, DocumentPosition pos)
    {
        e.Ctrl.Selection.CollapseTo(pos);
        e.Caret.MoveTo(pos);
    }

    private static void Key(Editor e, EditorKey key, InputModifiers modifiers = InputModifiers.None)
        => e.Ctrl.HandleKeyDown(new EditorKeyEventArgs(key, modifiers: modifiers), e.Doc, e.Layout, e.Context);

    private static TextRun RunContaining(EditorDocument doc, string text)
        => ((Paragraph)doc.Blocks[0]).Inlines.OfType<TextRun>().First(r => r.Text.Contains(text));

    // ── Pending style is cleared when the caret moves ────────────────

    [Theory]
    [InlineData(EditorKey.Left)]
    [InlineData(EditorKey.Right)]
    [InlineData(EditorKey.Home)]
    [InlineData(EditorKey.End)]
    public void PendingBold_CaretMovedByKey_TypingIsNotBold(EditorKey key)
    {
        var e = Setup(null, Para(("Hello World", TextStyle.Default)));
        PlaceCaret(e, new DocumentPosition(0, 0, 5));
        Key(e, EditorKey.B, InputModifiers.Control);

        Key(e, key);
        e.Ctrl.HandleTextInput("X", e.Doc);

        Assert.All(((Paragraph)e.Doc.Blocks[0]).Inlines.OfType<TextRun>(), r => Assert.False(r.Style.IsBold));
    }

    [Fact]
    public void PendingBold_CaretMovedAndBack_TypingIsNotBold()
    {
        var e = Setup(null, Para(("Hello World", TextStyle.Default)));
        PlaceCaret(e, new DocumentPosition(0, 0, 5));
        Key(e, EditorKey.B, InputModifiers.Control);

        Key(e, EditorKey.Right);
        Key(e, EditorKey.Left);
        e.Ctrl.HandleTextInput("X", e.Doc);

        Assert.False(RunContaining(e.Doc, "X").Style.IsBold);
    }

    [Fact]
    public void PendingBold_CaretMovedByClick_TypingIsNotBold()
    {
        var e = Setup(null, Para(("Hello World", TextStyle.Default)));
        PlaceCaret(e, new DocumentPosition(0, 0, 5));
        Key(e, EditorKey.B, InputModifiers.Control);

        e.Ctrl.HandlePointerDown(new EditorPointerEventArgs(0f, 5f, InputAction.Pressed), e.Doc, e.Layout, e.Context);
        e.Ctrl.HandlePointerDown(new EditorPointerEventArgs(0f, 5f, InputAction.Released), e.Doc, e.Layout, e.Context);
        Assert.NotEqual(new DocumentPosition(0, 0, 5), e.Caret.Position);
        e.Ctrl.HandleTextInput("X", e.Doc);

        Assert.False(RunContaining(e.Doc, "X").Style.IsBold);
    }

    [Fact]
    public void PendingBold_SelectionMovedFromCode_TypingIsNotBold()
    {
        var e = Setup(null, Para(("Hello World", TextStyle.Default)));
        PlaceCaret(e, new DocumentPosition(0, 0, 5));
        Key(e, EditorKey.B, InputModifiers.Control);

        PlaceCaret(e, new DocumentPosition(0, 0, 11));
        e.Ctrl.HandleTextInput("X", e.Doc);

        Assert.Equal("Hello WorldX", e.Doc.GetText());
        Assert.False(RunContaining(e.Doc, "X").Style.IsBold);
    }

    [Fact]
    public void PendingFontSize_SelectionMovedFromCode_TypingKeepsCaretSize()
    {
        var e = Setup(null, Para(("Hello World", TextStyle.Default)));
        var at = new DocumentPosition(0, 0, 5);
        PlaceCaret(e, at);
        e.FontFmt.ApplyFontSize(e.Doc, new TextRange(at, at), 30f);

        PlaceCaret(e, new DocumentPosition(0, 0, 11));
        e.Ctrl.HandleTextInput("X", e.Doc);

        Assert.Equal(TextStyle.Default.FontSize, RunContaining(e.Doc, "X").Style.FontSize);
    }

    [Fact]
    public void PendingBold_SameCaret_TypingIsBold()
    {
        var e = Setup(null, Para(("Hello World", TextStyle.Default)));
        PlaceCaret(e, new DocumentPosition(0, 0, 5));
        Key(e, EditorKey.B, InputModifiers.Control);

        e.Ctrl.HandleTextInput("X", e.Doc);

        Assert.True(RunContaining(e.Doc, "X").Style.IsBold);
    }

    [Fact]
    public void PendingBold_SameLogicalSpotAtRunBoundary_TypingIsBold()
    {
        // (0,0,5) and (0,1,0) are the same spot: the end of "Hello" and the start of " World".
        var e = Setup(null, Para(("Hello", TextStyle.Default), (" World", TextStyle.Default.WithItalic(true))));
        PlaceCaret(e, new DocumentPosition(0, 0, 5));
        Key(e, EditorKey.B, InputModifiers.Control);

        PlaceCaret(e, new DocumentPosition(0, 1, 0));
        e.Ctrl.HandleTextInput("X", e.Doc);

        Assert.True(RunContaining(e.Doc, "X").Style.IsBold);
    }

    [Fact]
    public void PendingBold_ThenItalicAtAnotherCaret_TypingIsItalicOnly()
    {
        var e = Setup(null, Para(("Hello World", TextStyle.Default)));
        PlaceCaret(e, new DocumentPosition(0, 0, 5));
        Key(e, EditorKey.B, InputModifiers.Control);

        PlaceCaret(e, new DocumentPosition(0, 0, 11));
        Key(e, EditorKey.I, InputModifiers.Control);
        e.Ctrl.HandleTextInput("X", e.Doc);

        var style = RunContaining(e.Doc, "X").Style;
        Assert.True(style.IsItalic);
        Assert.False(style.IsBold);
    }

    [Fact]
    public void Toolbar_PendingBold_AfterCaretMoved_ShowsBoldInactive()
    {
        var doc = new EditorDocument([Para(("Hello World", TextStyle.Default))]);
        var ctx = new EditorContext(doc, new Selection(), new UndoManager(),
            new FormattingEngine(), new FontFormattingEngine());
        ctx.Selection.CollapseTo(new DocumentPosition(0, 0, 5));
        new ToggleBoldCommand().Execute(ctx);

        ctx.Selection.CollapseTo(new DocumentPosition(0, 0, 2));
        var toolbar = ToolbarDefinition.CreateDefault();
        new ToolbarRenderer(toolbar).UpdateToggleStates(ctx);

        Assert.False(toolbar.AllItems.OfType<ToolbarButton>().First(b => b.Label == "Bold").IsActive);
        Assert.Null(ctx.FormattingEngine.PendingStyle);
    }

    [Fact]
    public void DiscardPendingStyleIfMoved_NonEmptySelection_Discards()
    {
        var doc = new EditorDocument([Para(("Hello World", TextStyle.Default))]);
        var fmt = new FormattingEngine();
        var at = new DocumentPosition(0, 0, 5);
        fmt.ToggleBold(doc, new TextRange(at, at));

        fmt.DiscardPendingStyleIfMoved(doc, new TextRange(at, new DocumentPosition(0, 0, 8)));

        Assert.Null(fmt.PendingStyle);
        Assert.Null(fmt.PendingStylePosition);
    }

    // ── Plain-text paste uses the typing insertion style ─────────────

    [Fact]
    public async Task Paste_AfterBoldText_InheritsBold()
    {
        var e = Setup("er", Para(("Hello ", TextStyle.Default), ("bold", Bold), (" tail", TextStyle.Default)));
        PlaceCaret(e, new DocumentPosition(0, 1, 4));

        await e.Ctrl.HandlePasteAsync(e.Doc);

        Assert.Equal("Hello bolder tail", e.Doc.GetText());
        Assert.True(RunContaining(e.Doc, "bolder").Style.IsBold);
        Assert.False(RunContaining(e.Doc, "tail").Style.IsBold);
    }

    [Fact]
    public async Task Paste_WithPendingBold_UsesPendingStyle()
    {
        var e = Setup("X", Para(("Hello World", TextStyle.Default)));
        PlaceCaret(e, new DocumentPosition(0, 0, 5));
        Key(e, EditorKey.B, InputModifiers.Control);

        await e.Ctrl.HandlePasteAsync(e.Doc);

        Assert.True(RunContaining(e.Doc, "X").Style.IsBold);
        Assert.Null(e.Fmt.PendingStyle);
    }

    [Fact]
    public async Task Paste_WithPendingFontSize_UsesPendingSize()
    {
        var e = Setup("X", Para(("Hello World", TextStyle.Default)));
        var at = new DocumentPosition(0, 0, 5);
        PlaceCaret(e, at);
        e.FontFmt.ApplyFontSize(e.Doc, new TextRange(at, at), 30f);

        await e.Ctrl.HandlePasteAsync(e.Doc);

        Assert.Equal(30f, RunContaining(e.Doc, "X").Style.FontSize);
    }

    [Fact]
    public async Task Paste_OverBoldSelection_InheritsStyleBeforeSelection()
    {
        var e = Setup("Earth", Para(("Hello ", Bold), ("World", TextStyle.Default)));
        e.Ctrl.Selection.Set(new DocumentPosition(0, 1, 0), new DocumentPosition(0, 1, 5));
        e.Caret.MoveTo(new DocumentPosition(0, 1, 5));

        await e.Ctrl.HandlePasteAsync(e.Doc);

        Assert.Equal("Hello Earth", e.Doc.GetText());
        Assert.True(RunContaining(e.Doc, "Earth").Style.IsBold);
    }

    [Fact]
    public async Task ClipboardOperationsPaste_AfterBoldText_InheritsBold()
    {
        var doc = new EditorDocument([Para(("bold", Bold), (" tail", TextStyle.Default))]);
        var at = new DocumentPosition(0, 0, 4);

        await ClipboardOperations.PasteAsync(doc, at, new TextRange(at, at), new MockClipboardProvider("er"));

        Assert.Equal("bolder tail", doc.GetText());
        Assert.True(RunContaining(doc, "bolder").Style.IsBold);
    }
}
