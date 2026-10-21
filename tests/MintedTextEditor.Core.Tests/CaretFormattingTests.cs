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

    // ── Replacing part of a link keeps it in the link ─────────────────

    private static (Paragraph Para, HyperlinkInline Link) LinkedSite()
    {
        var para = new Paragraph();
        para.AppendRun("See ");
        var link = new HyperlinkInline("https://example.com");
        para.AddInline(link);
        link.AddChild(new TextRun("site"));
        return (para, link);
    }

    [Fact]
    public void TypingOverSelectionAtLinkEnd_KeepsReplacementInLink()
    {
        var (para, link) = LinkedSite();
        var (doc, caret, ctrl, _) = Setup(para);
        ctrl.Selection.Set(new DocumentPosition(0, 1, 1), new DocumentPosition(0, 1, 4)); // "ite"
        caret.MoveTo(new DocumentPosition(0, 1, 4));

        // Typed one character at a time, as a keyboard delivers it.
        foreach (var c in "pot")
            ctrl.HandleTextInput(c.ToString(), doc);

        Assert.Equal("See spot", doc.GetText());
        Assert.Equal("spot", link.GetText());
        Assert.Equal(2, para.Inlines.Count);
    }

    [Fact]
    public void TypingOverSelectionAtLinkStart_KeepsReplacementInLink()
    {
        var (para, link) = LinkedSite();
        var (doc, caret, ctrl, _) = Setup(para);
        ctrl.Selection.Set(new DocumentPosition(0, 1, 0), new DocumentPosition(0, 1, 2)); // "si"
        caret.MoveTo(new DocumentPosition(0, 1, 2));

        ctrl.HandleTextInput("qui", doc);

        Assert.Equal("See quite", doc.GetText());
        Assert.Equal("quite", link.GetText());
    }

    [Theory]
    [InlineData(1, 4, "pot", "spot")] // suffix
    [InlineData(0, 2, "qui", "quite")] // prefix
    public void TypingOverSelectionInLink_ThenUndoRedo_KeepsReplacementInLink(int start, int end, string typed, string expectedLink)
    {
        var (para, _) = LinkedSite();
        var (doc, caret, ctrl, _) = Setup(para);
        var undo = new UndoManager();
        ctrl.UndoManager = undo;
        ctrl.Selection.Set(new DocumentPosition(0, 1, start), new DocumentPosition(0, 1, end));
        caret.MoveTo(new DocumentPosition(0, 1, end));
        foreach (var c in typed)
            ctrl.HandleTextInput(c.ToString(), doc);

        while (undo.CanUndo) undo.Undo();
        Assert.Equal("See site", doc.GetText());
        Assert.Equal("site", ((HyperlinkInline)para.Inlines[1]).GetText());
        while (undo.CanRedo) undo.Redo();

        Assert.Equal("See " + expectedLink, doc.GetText());
        Assert.Equal(expectedLink, ((HyperlinkInline)para.Inlines[1]).GetText());
    }

    [Fact]
    public void TypingAfterLink_AfterMovingCaretAwayFromReplacement_DoesNotExtendLink()
    {
        var (para, link) = LinkedSite();
        var (doc, caret, ctrl, _) = Setup(para);
        ctrl.Selection.Set(new DocumentPosition(0, 1, 1), new DocumentPosition(0, 1, 4));
        caret.MoveTo(new DocumentPosition(0, 1, 4));
        ctrl.HandleTextInput("pot", doc);

        ctrl.Selection.CollapseTo(new DocumentPosition(0, 0, 0));
        caret.MoveTo(new DocumentPosition(0, 0, 0));
        ctrl.HandleTextInput("I ", doc);
        ctrl.Selection.CollapseTo(new DocumentPosition(0, 1, 4));
        caret.MoveTo(new DocumentPosition(0, 1, 4));
        ctrl.HandleTextInput("!", doc);

        Assert.Equal("I See spot!", doc.GetText());
        Assert.Equal("spot", link.GetText());
    }

    [Theory]
    [InlineData("ite", "pot", "spot")] // suffix
    [InlineData("si", "qui", "quite")] // prefix
    public void FindReplace_Replace_PartOfLink_KeepsReplacementInLink(string query, string replacement, string expectedLink)
    {
        var (para, _) = LinkedSite();
        var doc = new EditorDocument([para]);
        var engine = new FindReplaceEngine(doc);
        Assert.NotNull(engine.Find(query, new FindOptions { MatchCase = true }));

        engine.Replace(query, replacement, new FindOptions { MatchCase = true });

        Assert.Equal("See " + expectedLink, doc.GetText());
        var links = para.Inlines.OfType<HyperlinkInline>().ToList();
        Assert.Single(links);
        Assert.Equal(expectedLink, links[0].GetText());
    }

    [Theory]
    [InlineData("ite", "pot", "spot")] // suffix
    [InlineData("si", "qui", "quite")] // prefix
    public void FindReplace_ReplaceAll_PartOfLink_KeepsReplacementInLink(string query, string replacement, string expectedLink)
    {
        var (para, _) = LinkedSite();
        var doc = new EditorDocument([para]);
        var engine = new FindReplaceEngine(doc);

        Assert.Equal(1, engine.ReplaceAll(query, replacement, new FindOptions { MatchCase = true }));

        Assert.Equal("See " + expectedLink, doc.GetText());
        var links = para.Inlines.OfType<HyperlinkInline>().ToList();
        Assert.Single(links);
        Assert.Equal(expectedLink, links[0].GetText());
    }

    // ── Enter keeps the caret style ──────────────────────────────────

    [Fact]
    public void Enter_AfterBoldText_NextParagraphTypingStaysBold()
    {
        var (doc, caret, ctrl, _) = Setup(Para(("plain ", TextStyle.Default), ("bold", Bold)));
        caret.MoveTo(new DocumentPosition(0, 1, 4)); // end of "bold"

        ctrl.HandleTextInput("\n", doc); // same path as the Enter key
        ctrl.HandleTextInput("more", doc);

        Assert.Equal(2, doc.Blocks.Count);
        Assert.True(RunContaining(doc, "more", block: 1).Style.IsBold);
    }

    [Fact]
    public void Enter_AfterPlainText_NewParagraphHasNoInlines()
    {
        var (doc, caret, ctrl, _) = Setup(Para(("plain", TextStyle.Default)));
        caret.MoveTo(new DocumentPosition(0, 0, 5));

        ctrl.HandleTextInput("\n", doc); // same path as the Enter key

        Assert.Empty(((Paragraph)doc.Blocks[1]).Inlines);
    }

    [Fact]
    public void Enter_WithPendingBold_NextParagraphTypingIsBold()
    {
        var (doc, caret, ctrl, fmt) = Setup(Para(("plain", TextStyle.Default)));
        var end = new DocumentPosition(0, 0, 5);
        caret.MoveTo(end);
        ctrl.Selection.CollapseTo(end);
        fmt.ToggleBold(doc, new TextRange(end, end));

        ctrl.HandleTextInput("\n", doc); // same path as the Enter key
        ctrl.HandleTextInput("more", doc);

        Assert.True(RunContaining(doc, "more", block: 1).Style.IsBold);
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
