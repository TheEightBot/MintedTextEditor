using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Input;
using MintedTextEditor.Core.Layout;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Tests;

/// <summary>
/// Character-formatting commands, keyboard shortcuts and hyperlink removal must each record
/// exactly one undo step that restores the previous runs with their exact styles.
/// </summary>
public class CharacterFormatUndoTests
{
    private static readonly TextStyle Bold = TextStyle.Default.WithBold(true);
    private static readonly TextStyle Italic = TextStyle.Default.WithItalic(true).WithFontSize(18);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Paragraph Runs(params (string Text, TextStyle Style)[] runs)
    {
        var p = new Paragraph();
        foreach (var (text, style) in runs)
            p.AppendRun(text, style);
        return p;
    }

    /// <summary>"Hello " (bold), "big " (plain), "world" (italic, 18pt).</summary>
    private static EditorDocument MixedDoc() =>
        new([Runs(("Hello ", Bold), ("big ", TextStyle.Default), ("world", Italic))]);

    /// <summary>Flattens the document into comparable (text, style) entries per block.</summary>
    private static List<List<object>> Dump(EditorDocument doc) =>
        doc.Blocks.Select(b => b switch
        {
            Paragraph p => p.Inlines.Select(i => i switch
            {
                TextRun r => (object)(r.Text, r.Style),
                _ => Describe(i),
            }).ToList(),
            _ => new List<object> { b.GetType().Name },
        }).ToList();

    /// <summary>Lossless text description of an inline, recursing into hyperlink children.</summary>
    private static string Describe(Inline inline) => inline switch
    {
        TextRun r => $"T[{r.Text}|{r.Style.GetHashCode()}|{r.Style.IsBold}]",
        ImageInline img => $"I[{img.Source}|{img.AltText}|{img.Width}x{img.Height}]",
        LineBreak => "BR",
        HyperlinkInline h => $"L[{h.Url}|{h.Title}|{string.Join(",", h.Children.Select(Describe))}]",
        _ => inline.GetType().Name,
    };

    private static EditorContext Ctx(EditorDocument doc, DocumentPosition anchor, DocumentPosition active, UndoManager undo)
    {
        var sel = new Selection();
        sel.Set(anchor, active);
        return new EditorContext(doc, sel, undo, new FormattingEngine(), new FontFormattingEngine());
    }

    private static int UndoDepth(UndoManager undo)
    {
        int n = 0;
        while (undo.CanUndo) { undo.Undo(); n++; }
        for (int i = 0; i < n; i++) undo.Redo();
        return n;
    }

    public static TheoryData<string> CommandNames => new()
    {
        "ToggleBold", "ToggleItalic", "ToggleUnderline", "ToggleStrikethrough",
        "ToggleSubscript", "ToggleSuperscript", "ClearFormatting",
        "ApplyFontFamily", "ApplyFontSize", "ApplyTextColor", "ApplyHighlightColor",
    };

    private static IEditorCommand Create(string name) => name switch
    {
        "ToggleBold"          => new ToggleBoldCommand(),
        "ToggleItalic"        => new ToggleItalicCommand(),
        "ToggleUnderline"     => new ToggleUnderlineCommand(),
        "ToggleStrikethrough" => new ToggleStrikethroughCommand(),
        "ToggleSubscript"     => new ToggleSubscriptCommand(),
        "ToggleSuperscript"   => new ToggleSuperscriptCommand(),
        "ClearFormatting"     => new ClearFormattingCommand(),
        "ApplyFontFamily"     => new ApplyFontFamilyCommand { FontFamily = "Georgia" },
        "ApplyFontSize"       => new ApplyFontSizeCommand { FontSize = 30 },
        "ApplyTextColor"      => new ApplyTextColorCommand { Color = EditorColor.Red },
        "ApplyHighlightColor" => new ApplyHighlightColorCommand { Color = EditorColor.Yellow },
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    // ── Commands: one step, exact undo, redo ─────────────────────────────────

    [Theory]
    [MemberData(nameof(CommandNames))]
    public void Command_OverMixedRuns_RecordsOneStep_UndoRestoresExactRuns_RedoReapplies(string name)
    {
        var doc = MixedDoc();
        var undo = new UndoManager();
        // "llo big wor": from inside the bold run to inside the italic run
        var ctx = Ctx(doc, new DocumentPosition(0, 0, 2), new DocumentPosition(0, 2, 3), undo);
        var before = Dump(doc);

        Create(name).Execute(ctx);
        var after = Dump(doc);

        Assert.NotEqual(before, after);
        Assert.Equal(1, UndoDepth(undo));
        Assert.Equal("llo big wor", DocumentEditor.GetSelectedText(doc, ctx.Selection.Range));

        var undoPos = undo.Undo();
        Assert.NotNull(undoPos);
        Assert.Equal(before, Dump(doc));
        Assert.False(undo.CanUndo);

        undo.Redo();
        Assert.Equal(after, Dump(doc));
    }

    [Fact]
    public void ToggleBold_OffOnFullyBoldText_UndoRestoresBold()
    {
        var doc = new EditorDocument([Runs(("Hello", Bold))]);
        var undo = new UndoManager();
        var ctx = Ctx(doc, new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 5), undo);

        new ToggleBoldCommand().Execute(ctx);
        Assert.False(((TextRun)((Paragraph)doc.Blocks[0]).Inlines[0]).Style.IsBold);

        undo.Undo();
        var runs = ((Paragraph)doc.Blocks[0]).Inlines.Cast<TextRun>().ToList();
        Assert.Single(runs);
        Assert.Equal(Bold, runs[0].Style);

        undo.Redo();
        Assert.All(((Paragraph)doc.Blocks[0]).Inlines.Cast<TextRun>(), r => Assert.False(r.Style.IsBold));
    }

    [Fact]
    public void RepeatedToggleAndUndo_DoesNotFragmentRuns()
    {
        var doc = new EditorDocument([new Paragraph("Hello world")]);
        var undo = new UndoManager();
        var ctx = Ctx(doc, new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 5), undo);

        new ToggleBoldCommand().Execute(ctx);
        new ToggleBoldCommand().Execute(ctx);   // selection still covers "Hello"
        Assert.Single(((Paragraph)doc.Blocks[0]).Inlines);
        Assert.Equal(2, UndoDepth(undo));

        undo.Undo();
        Assert.Equal(2, ((Paragraph)doc.Blocks[0]).Inlines.Count);
        Assert.True(((TextRun)((Paragraph)doc.Blocks[0]).Inlines[0]).Style.IsBold);
        undo.Undo();
        Assert.Single(((Paragraph)doc.Blocks[0]).Inlines);
        Assert.False(((TextRun)((Paragraph)doc.Blocks[0]).Inlines[0]).Style.IsBold);
    }

    // ── Multi-paragraph selection ────────────────────────────────────────────

    [Fact]
    public void Command_AcrossParagraphsAndTable_UndoRestoresEveryParagraph()
    {
        var doc = new EditorDocument(
        [
            Runs(("Alpha ", Bold), ("one", TextStyle.Default)),
            new TableBlock(1, 1),
            Runs(("Gamma", Italic)),
            new Paragraph("Delta"),
        ]);
        var undo = new UndoManager();
        var ctx = Ctx(doc, new DocumentPosition(0, 0, 2), new DocumentPosition(3, 0, 2), undo);
        var before = Dump(doc);

        new ApplyTextColorCommand { Color = EditorColor.Red }.Execute(ctx);
        var after = Dump(doc);
        Assert.NotEqual(before, after);
        Assert.Equal(EditorColor.Red, ((TextRun)((Paragraph)doc.Blocks[2]).Inlines[0]).Style.TextColor);
        Assert.Equal(1, UndoDepth(undo));

        undo.Undo();
        Assert.Equal(before, Dump(doc));

        undo.Redo();
        Assert.Equal(after, Dump(doc));
    }

    [Fact]
    public void Undo_LeavesParagraphStyleUntouched()
    {
        var doc = new EditorDocument([new Paragraph("Hello")]);
        var undo = new UndoManager();
        var ctx = Ctx(doc, new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 5), undo);

        new ToggleItalicCommand().Execute(ctx);
        // An unrelated paragraph change made after the formatting step
        ((Paragraph)doc.Blocks[0]).Style.Alignment = TextAlignment.Center;

        undo.Undo();
        Assert.Equal(TextAlignment.Center, ((Paragraph)doc.Blocks[0]).Style.Alignment);
        Assert.False(((TextRun)((Paragraph)doc.Blocks[0]).Inlines[0]).Style.IsItalic);
    }

    // ── Pending style (collapsed selection) ───────────────────────────────────

    [Theory]
    [MemberData(nameof(CommandNames))]
    public void Command_WithCollapsedSelection_SetsPendingStyle_WithoutUndoStep(string name)
    {
        var doc = MixedDoc();
        var undo = new UndoManager();
        var caret = new DocumentPosition(0, 1, 2);
        var ctx = Ctx(doc, caret, caret, undo);
        var before = Dump(doc);

        Create(name).Execute(ctx);

        Assert.False(undo.CanUndo);
        Assert.Equal(before, Dump(doc));
        Assert.NotNull(ctx.FormattingEngine.PendingStyle ?? ctx.FontFormattingEngine.PendingStyle);
        Assert.Equal(caret, ctx.Selection.Active);
    }

    // ── Keyboard shortcuts ────────────────────────────────────────────────────

    [Fact]
    public void CtrlB_WithSelection_RecordsOneUndoStep()
    {
        var doc = new EditorDocument([new Paragraph("Hello")]);
        var context = new MockDrawingContext();
        var layout = new TextLayoutEngine().Layout(doc, 800f, context);
        var undo = new UndoManager();
        var ctrl = new EditorInputController(new Caret(), new CaretRenderer())
        {
            FormattingEngine = new FormattingEngine(),
            UndoManager = undo,
        };

        ctrl.HandleKeyDown(new EditorKeyEventArgs(EditorKey.A, modifiers: InputModifiers.Control), doc, layout, context);
        ctrl.HandleKeyDown(new EditorKeyEventArgs(EditorKey.B, modifiers: InputModifiers.Control), doc, layout, context);
        Assert.True(((TextRun)((Paragraph)doc.Blocks[0]).Inlines[0]).Style.IsBold);
        Assert.Equal(1, UndoDepth(undo));

        ctrl.HandleKeyDown(new EditorKeyEventArgs(EditorKey.Z, modifiers: InputModifiers.Control), doc, layout, context);
        Assert.False(((TextRun)((Paragraph)doc.Blocks[0]).Inlines[0]).Style.IsBold);
    }

    [Fact]
    public void CtrlB_WithCollapsedSelection_DoesNotRecordUndoStep()
    {
        var doc = new EditorDocument([new Paragraph("Hello")]);
        var context = new MockDrawingContext();
        var layout = new TextLayoutEngine().Layout(doc, 800f, context);
        var undo = new UndoManager();
        var fmt = new FormattingEngine();
        var ctrl = new EditorInputController(new Caret(), new CaretRenderer())
        {
            FormattingEngine = fmt,
            UndoManager = undo,
        };

        ctrl.HandleKeyDown(new EditorKeyEventArgs(EditorKey.B, modifiers: InputModifiers.Control), doc, layout, context);

        Assert.False(undo.CanUndo);
        Assert.True(fmt.PendingStyle?.IsBold);
    }

    // ── RemoveHyperlink ───────────────────────────────────────────────────────

    private static (EditorDocument doc, int linkIndex) LinkDoc()
    {
        var para = Runs(("Click ", TextStyle.Default));
        var doc = new EditorDocument([para]);
        var link = new HyperlinkInline("https://example.com", "Example");
        para.AddInline(link);
        link.AddChild(new TextRun("bold", Bold));
        link.AddChild(new TextRun(" plain"));
        para.AppendRun(" after");
        return (doc, 1);
    }

    [Fact]
    public void RemoveHyperlink_KeepsLinkTextFormatting()
    {
        var (doc, idx) = LinkDoc();

        HyperlinkEngine.RemoveHyperlink(doc, new DocumentPosition(0, idx, 1));

        var inlines = ((Paragraph)doc.Blocks[0]).Inlines;
        Assert.DoesNotContain(inlines, i => i is HyperlinkInline);
        Assert.Equal("Click bold plain after", doc.GetText());
        var boldRun = Assert.Single(inlines.OfType<TextRun>(), r => r.Text == "bold");
        Assert.Equal(Bold, boldRun.Style);
        Assert.All(inlines, i => Assert.Same(doc.Blocks[0], i.Parent));
    }

    [Fact]
    public void RemoveHyperlinkCommand_RecordsOneStep_UndoRestoresLink_RedoRemovesAgain()
    {
        var (doc, idx) = LinkDoc();
        var undo = new UndoManager();
        var caret = new DocumentPosition(0, idx, 2);
        var ctx = Ctx(doc, caret, caret, undo);
        var before = Dump(doc);

        new RemoveHyperlinkCommand().Execute(ctx);
        var after = Dump(doc);

        Assert.DoesNotContain(((Paragraph)doc.Blocks[0]).Inlines, i => i is HyperlinkInline);
        Assert.Equal(1, UndoDepth(undo));
        // Caret stays on the same character ("Click bo|ld")
        Assert.Equal(8, DocumentEditor.GetAbsoluteCharOffset(doc, ctx.Selection.Active));

        var pos = undo.Undo();
        Assert.Equal(before, Dump(doc));
        var link = Assert.IsType<HyperlinkInline>(((Paragraph)doc.Blocks[0]).Inlines[idx]);
        Assert.Equal("https://example.com", link.Url);
        Assert.Equal("Example", link.Title);
        Assert.Equal(Bold, ((TextRun)link.Children[0]).Style);
        Assert.Equal(8, DocumentEditor.GetAbsoluteCharOffset(doc, pos!.Value));

        undo.Redo();
        Assert.Equal(after, Dump(doc));
    }

    [Fact]
    public void RemoveHyperlinkCommand_WithoutLinkAtCaret_DoesNotRecordUndoStep()
    {
        var doc = new EditorDocument([new Paragraph("No link")]);
        var undo = new UndoManager();
        var caret = new DocumentPosition(0, 0, 2);

        new RemoveHyperlinkCommand().Execute(Ctx(doc, caret, caret, undo));

        Assert.False(undo.CanUndo);
    }

    // ── Undo of earlier insertions after a snapshot restore ───────────────────
    // Snapshot restores must put the original inline objects back: earlier insert
    // actions find what they inserted by reference when they are undone.

    private static void SelectRange(EditorContext ctx, int fromAbs, int toAbs)
    {
        var context = new DocumentPosition(0, 0, 0);
        ctx.Selection.Set(
            DocumentEditor.AbsoluteCharOffsetToPosition(ctx.Document, context, fromAbs),
            DocumentEditor.AbsoluteCharOffsetToPosition(ctx.Document, context, toAbs));
    }

    [Fact]
    public void InsertLink_RemoveLink_UndoTwice_RemovesTheLink()
    {
        var doc = new EditorDocument([new Paragraph("Click here now")]);
        var undo = new UndoManager();
        var ctx = Ctx(doc, new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 10), undo);

        new InsertHyperlinkCommand { Url = "https://example.com" }.Execute(ctx);
        var linkIndex = ((Paragraph)doc.Blocks[0]).Inlines.FindIndex(i => i is HyperlinkInline);
        ctx.Selection.CollapseTo(new DocumentPosition(0, linkIndex, 1));
        new RemoveHyperlinkCommand().Execute(ctx);

        undo.Undo();   // link is back
        Assert.Contains(((Paragraph)doc.Blocks[0]).Inlines, i => i is HyperlinkInline);
        undo.Undo();   // insertion undone
        Assert.DoesNotContain(((Paragraph)doc.Blocks[0]).Inlines, i => i is HyperlinkInline);
        Assert.Equal("Click here now", doc.GetText());
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void InsertLink_FormatOverIt_UndoTwice_RemovesTheLink()
    {
        var doc = new EditorDocument([new Paragraph("Click here now")]);
        var undo = new UndoManager();
        var ctx = Ctx(doc, new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 10), undo);

        new InsertHyperlinkCommand { Url = "https://example.com" }.Execute(ctx);
        SelectRange(ctx, 0, 14);
        new ToggleBoldCommand().Execute(ctx);

        undo.Undo();   // bold undone; link still there
        Assert.Contains(((Paragraph)doc.Blocks[0]).Inlines, i => i is HyperlinkInline);
        undo.Undo();   // insertion undone
        Assert.DoesNotContain(((Paragraph)doc.Blocks[0]).Inlines, i => i is HyperlinkInline);
        Assert.Equal("Click here now", doc.GetText());
    }

    [Fact]
    public void InsertImage_FormatOverIt_UndoTwice_RemovesTheImage()
    {
        var doc = new EditorDocument([new Paragraph("Before after")]);
        var undo = new UndoManager();
        var caret = new DocumentPosition(0, 0, 6);
        var ctx = Ctx(doc, caret, caret, undo);

        new InsertImageCommand { Source = "pic.png", Width = 10, Height = 20 }.Execute(ctx);
        SelectRange(ctx, 0, 13);
        new ApplyTextColorCommand { Color = EditorColor.Red }.Execute(ctx);

        undo.Undo();   // colour undone; image still there
        Assert.Single(((Paragraph)doc.Blocks[0]).Inlines.OfType<ImageInline>());
        undo.Undo();   // insertion undone
        Assert.Empty(((Paragraph)doc.Blocks[0]).Inlines.OfType<ImageInline>());
        Assert.Equal("Before after", doc.GetText());
    }

    [Fact]
    public void InsertImage_RemoveLinkInSameParagraph_UndoTwice_RemovesTheImage()
    {
        var (doc, _) = LinkDoc();
        var undo = new UndoManager();
        var caret = new DocumentPosition(0, 0, 2);
        var ctx = Ctx(doc, caret, caret, undo);

        new InsertImageCommand { Source = "pic.png" }.Execute(ctx);
        var linkIndex = ((Paragraph)doc.Blocks[0]).Inlines.FindIndex(i => i is HyperlinkInline);
        ctx.Selection.CollapseTo(new DocumentPosition(0, linkIndex, 1));
        new RemoveHyperlinkCommand().Execute(ctx);

        undo.Undo();   // link back
        Assert.Contains(((Paragraph)doc.Blocks[0]).Inlines, i => i is HyperlinkInline);
        undo.Undo();   // image insertion undone
        Assert.Empty(((Paragraph)doc.Blocks[0]).Inlines.OfType<ImageInline>());
        Assert.Equal("Click bold plain after", doc.GetText());
    }

    // ── Lossless snapshots ────────────────────────────────────────────────────

    [Fact]
    public void Format_AcrossLineBreak_UndoRestoresLineBreak_RedoKeepsIt()
    {
        var para = new Paragraph("first");
        var doc = new EditorDocument([para]);
        var lineBreak = new LineBreak();
        para.AddInline(lineBreak);
        para.AppendRun("second");
        var undo = new UndoManager();
        var ctx = Ctx(doc, new DocumentPosition(0, 0, 2), new DocumentPosition(0, 2, 3), undo);
        var before = Dump(doc);

        new ToggleBoldCommand().Execute(ctx);
        var after = Dump(doc);
        Assert.Contains(para.Inlines, i => i is LineBreak);

        undo.Undo();
        Assert.Equal(before, Dump(doc));
        Assert.Same(lineBreak, para.Inlines[1]);

        undo.Redo();
        Assert.Equal(after, Dump(doc));
        Assert.Contains(para.Inlines, i => i is LineBreak);
    }

    [Fact]
    public void RemoveLink_WithNonTextChildren_UndoRestoresAllChildren_RedoKeepsThem()
    {
        var para = new Paragraph("See ");
        var doc = new EditorDocument([para]);
        var link = new HyperlinkInline("https://example.com", "Example");
        para.AddInline(link);
        link.AddChild(new TextRun("logo", Bold));
        link.AddChild(new ImageInline("logo.png", "Logo", 16, 16));
        link.AddChild(new LineBreak());
        link.AddChild(new TextRun("caption", Italic));
        para.AppendRun(" end");
        var undo = new UndoManager();
        var caret = new DocumentPosition(0, 1, 1);
        var ctx = Ctx(doc, caret, caret, undo);
        var before = Dump(doc);

        new RemoveHyperlinkCommand().Execute(ctx);
        var after = Dump(doc);
        Assert.Contains(para.Inlines, i => i is ImageInline);
        Assert.Contains(para.Inlines, i => i is LineBreak);
        Assert.Equal("See logo￼\ncaption end", doc.GetText());

        undo.Undo();
        Assert.Equal(before, Dump(doc));
        Assert.Same(link, para.Inlines[1]);
        Assert.All(link.Children, c => Assert.Same(para, c.Parent));

        undo.Redo();
        Assert.Equal(after, Dump(doc));
    }

    [Fact]
    public void Format_OverLinkWithNonTextChildren_UndoKeepsChildren()
    {
        var para = new Paragraph("A ");
        var doc = new EditorDocument([para]);
        var link = new HyperlinkInline("https://example.com");
        para.AddInline(link);
        link.AddChild(new TextRun("x", Bold));
        link.AddChild(new LineBreak());
        link.AddChild(new ImageInline("i.png"));
        para.AppendRun(" B");
        var undo = new UndoManager();
        var ctx = Ctx(doc, new DocumentPosition(0, 0, 0), new DocumentPosition(0, 2, 2), undo);
        var before = Dump(doc);

        new ToggleItalicCommand().Execute(ctx);
        undo.Undo();

        Assert.Equal(before, Dump(doc));
        Assert.Same(link, para.Inlines[1]);
    }
}
