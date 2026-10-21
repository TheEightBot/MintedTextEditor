using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Input;
using MintedTextEditor.Core.Toolbar;

namespace MintedTextEditor.Core.Tests;

/// <summary>
/// Formatting at a bare (collapsed) caret: typed text inherits the style of the text at
/// the caret unless a pending style was picked, and the toolbar reflects that style.
/// </summary>
public class CaretFormattingTests
{
    private static readonly TextStyle Bold = TextStyle.Default.WithBold(true);
    private static readonly TextStyle Italic = TextStyle.Default.WithItalic(true);

    private static Paragraph Para(params (string Text, TextStyle Style)[] runs)
    {
        var p = new Paragraph();
        foreach (var (text, style) in runs) p.AppendRun(text, style);
        return p;
    }

    private static (EditorDocument doc, Caret caret, EditorInputController ctrl, FormattingEngine fmt)
        Setup(params Block[] blocks)
    {
        var doc = new EditorDocument(blocks);
        var caret = new Caret();
        var fmt = new FormattingEngine();
        var ctrl = new EditorInputController(caret, new CaretRenderer())
        {
            FormattingEngine = fmt,
            FontFormattingEngine = new FontFormattingEngine(),
        };
        return (doc, caret, ctrl, fmt);
    }

    private static TextRun RunContaining(EditorDocument doc, string text, int block = 0)
        => ((Paragraph)doc.Blocks[block]).Inlines.OfType<TextRun>().First(r => r.Text.Contains(text));

    // ── Typed text inherits the caret style ──────────────────────────

    [Fact]
    public void Typing_AfterBoldText_StaysBold()
    {
        var (doc, caret, ctrl, _) = Setup(Para(("Hello ", TextStyle.Default), ("bold", Bold), (" tail", TextStyle.Default)));
        caret.MoveTo(new DocumentPosition(0, 1, 4)); // end of "bold"

        ctrl.HandleTextInput("er", doc);

        Assert.Equal("Hello bolder tail", doc.GetText());
        Assert.True(RunContaining(doc, "bolder").Style.IsBold);
        Assert.False(RunContaining(doc, "tail").Style.IsBold);
    }

    [Fact]
    public void Typing_AfterPlainText_BeforeBoldText_StaysPlain()
    {
        var (doc, caret, ctrl, _) = Setup(Para(("plain", TextStyle.Default), ("bold", Bold)));
        caret.MoveTo(new DocumentPosition(0, 0, 5)); // between "plain" and "bold"

        ctrl.HandleTextInput("X", doc);

        Assert.False(RunContaining(doc, "plainX").Style.IsBold);
    }

    [Fact]
    public void Typing_AtParagraphStart_UsesFollowingCharacterStyle()
    {
        var (doc, caret, ctrl, _) = Setup(Para(("italic", Italic), (" plain", TextStyle.Default)));
        caret.MoveTo(new DocumentPosition(0, 0, 0));

        ctrl.HandleTextInput("An ", doc);

        Assert.Equal("An italic plain", doc.GetText());
        Assert.True(RunContaining(doc, "An ").Style.IsItalic);
    }

    [Fact]
    public void Typing_InEmptyParagraph_UsesDefaultStyle()
    {
        var (doc, caret, ctrl, _) = Setup(new Paragraph());
        caret.MoveTo(new DocumentPosition(0, 0, 0));

        ctrl.HandleTextInput("x", doc);

        Assert.Equal(TextStyle.Default, RunContaining(doc, "x").Style);
    }

    [Fact]
    public void Typing_WithPendingStyle_PendingStyleWins()
    {
        var (doc, caret, ctrl, fmt) = Setup(Para(("bold", Bold)));
        var caretPos = new DocumentPosition(0, 0, 4);
        caret.MoveTo(caretPos);

        // Toggling bold at a caret inside bold text turns it off for the next typed text.
        fmt.ToggleBold(doc, new TextRange(caretPos, caretPos));
        ctrl.HandleTextInput("X", doc);

        Assert.False(RunContaining(doc, "X").Style.IsBold);
        Assert.True(RunContaining(doc, "bold").Style.IsBold);
    }

    [Fact]
    public void Typing_WithPendingItalic_KeepsInheritedBold()
    {
        var (doc, caret, ctrl, fmt) = Setup(Para(("bold", Bold)));
        var caretPos = new DocumentPosition(0, 0, 4);
        caret.MoveTo(caretPos);

        fmt.ToggleItalic(doc, new TextRange(caretPos, caretPos));
        ctrl.HandleTextInput("X", doc);

        var style = RunContaining(doc, "X").Style;
        Assert.True(style.IsItalic);
        Assert.True(style.IsBold);
    }

    // ── Hyperlinks are not extended ──────────────────────────────────

    [Fact]
    public void Typing_AfterHyperlinkEnd_DoesNotExtendLink()
    {
        var para = new Paragraph();
        para.AppendRun("See ");
        var link = new HyperlinkInline("https://example.com");
        para.AddInline(link);
        link.AddChild(new TextRun("site"));
        var (doc, caret, ctrl, _) = Setup(para);
        caret.MoveTo(new DocumentPosition(0, 1, 4)); // end of the link (last inline)

        ctrl.HandleTextInput("!", doc);

        Assert.Equal("See site!", doc.GetText());
        Assert.Equal("site", link.GetText());
        Assert.Equal("!", ((TextRun)para.Inlines[2]).Text);
        Assert.Equal(new DocumentPosition(0, 2, 1), caret.Position);
    }

    [Fact]
    public void Typing_AfterHyperlinkEnd_JoinsFollowingText()
    {
        var para = new Paragraph();
        var link = new HyperlinkInline("https://example.com");
        para.AddInline(link);
        link.AddChild(new TextRun("site"));
        para.AppendRun(" tail");
        var (doc, caret, ctrl, _) = Setup(para);
        caret.MoveTo(new DocumentPosition(0, 0, 4)); // end of the link

        ctrl.HandleTextInput("s", doc);

        Assert.Equal("sites tail", doc.GetText());
        Assert.Equal("site", link.GetText());
        Assert.Equal("s tail", ((TextRun)para.Inlines[1]).Text);
    }

    [Fact]
    public void Typing_BeforeHyperlinkAtParagraphStart_DoesNotExtendLink()
    {
        var para = new Paragraph();
        var link = new HyperlinkInline("https://example.com");
        para.AddInline(link);
        link.AddChild(new TextRun("site"));
        var (doc, caret, ctrl, _) = Setup(para);
        caret.MoveTo(new DocumentPosition(0, 0, 0));

        ctrl.HandleTextInput("A ", doc);

        Assert.Equal("A site", doc.GetText());
        Assert.Equal("site", link.GetText());
        Assert.Equal(new DocumentPosition(0, 0, 2), caret.Position);
    }

    [Fact]
    public void Typing_AfterHyperlinkEnd_UndoRemovesOnlyTypedText()
    {
        var para = new Paragraph();
        var link = new HyperlinkInline("https://example.com");
        para.AddInline(link);
        link.AddChild(new TextRun("site"));
        var (doc, caret, ctrl, _) = Setup(para);
        var undo = new UndoManager();
        ctrl.UndoManager = undo;
        caret.MoveTo(new DocumentPosition(0, 0, 4));

        ctrl.HandleTextInput("!", doc);
        undo.Undo();

        Assert.Equal("site", doc.GetText());
        Assert.Equal("site", link.GetText());
    }

    [Fact]
    public void Typing_InsideHyperlink_ExtendsLink()
    {
        var para = new Paragraph();
        var link = new HyperlinkInline("https://example.com");
        para.AddInline(link);
        link.AddChild(new TextRun("ste"));
        var (doc, caret, ctrl, _) = Setup(para);
        caret.MoveTo(new DocumentPosition(0, 0, 1));

        ctrl.HandleTextInput("i", doc);

        Assert.Equal("site", link.GetText());
    }

    // ── Toolbar active state at a caret ──────────────────────────────

    private static ToolbarButton Button(ToolbarDefinition toolbar, string label)
        => toolbar.AllItems.OfType<ToolbarButton>().First(b => b.Label == label);

    [Fact]
    public void Toolbar_CaretInsideBoldText_ShowsBoldActive()
    {
        var doc = new EditorDocument([Para(("plain ", TextStyle.Default), ("bold", Bold))]);
        var ctx = new EditorContext(doc, new Selection(), new UndoManager(),
            new FormattingEngine(), new FontFormattingEngine());
        ctx.Selection.CollapseTo(new DocumentPosition(0, 1, 2));

        var toolbar = ToolbarDefinition.CreateDefault();
        new ToolbarRenderer(toolbar).UpdateToggleStates(ctx);

        Assert.True(Button(toolbar, "Bold").IsActive);
        Assert.False(Button(toolbar, "Italic").IsActive);
    }

    [Fact]
    public void Toolbar_CaretInsideBoldText_WithPendingBoldOff_ShowsBoldInactive()
    {
        var doc = new EditorDocument([Para(("bold", Bold))]);
        var ctx = new EditorContext(doc, new Selection(), new UndoManager(),
            new FormattingEngine(), new FontFormattingEngine());
        ctx.Selection.CollapseTo(new DocumentPosition(0, 0, 2));
        new ToggleBoldCommand().Execute(ctx);

        var toolbar = ToolbarDefinition.CreateDefault();
        new ToolbarRenderer(toolbar).UpdateToggleStates(ctx);

        Assert.False(Button(toolbar, "Bold").IsActive);
    }
}
