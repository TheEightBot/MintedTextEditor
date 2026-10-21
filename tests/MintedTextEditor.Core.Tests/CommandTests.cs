using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Tests;

public class CommandTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static EditorDocument MakeDoc(string text = "Hello world")
    {
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun(text);
        return doc;
    }

    private static TextRun FirstRun(EditorDocument doc, int block = 0)
        => (TextRun)((Paragraph)doc.Blocks[block]).Inlines[0];

    private static TextRange FullRange(EditorDocument doc, int block = 0)
    {
        var run = FirstRun(doc, block);
        return new TextRange(
            new DocumentPosition(block, 0, 0),
            new DocumentPosition(block, 0, run.Text.Length));
    }

    private static EditorContext MakeCtx(
        EditorDocument? doc = null,
        Selection? sel = null,
        UndoManager? undo = null)
    {
        doc  ??= MakeDoc();
        sel  ??= new Selection();
        undo ??= new UndoManager();
        return new EditorContext(doc, sel, undo, new FormattingEngine(), new FontFormattingEngine());
    }

    private static EditorContext MakeCtxWithFullSelection(EditorDocument doc)
    {
        var sel = new Selection();
        var run = FirstRun(doc);
        sel.Set(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, run.Text.Length));
        return new EditorContext(doc, sel, new UndoManager(), new FormattingEngine(), new FontFormattingEngine());
    }

    // ── Registry ─────────────────────────────────────────────────────────────

    [Fact]
    public void Registry_RegisterAndGet_ReturnsCommand()
    {
        var reg = new EditorCommandRegistry();
        var cmd = new ToggleBoldCommand();
        reg.Register(cmd);
        Assert.Same(cmd, reg.Get("ToggleBold"));
    }

    [Fact]
    public void Registry_Get_CaseInsensitive()
    {
        var reg = new EditorCommandRegistry();
        reg.Register(new ToggleBoldCommand());
        Assert.NotNull(reg.Get("togglebold"));
        Assert.NotNull(reg.Get("TOGGLEBOLD"));
    }

    [Fact]
    public void Registry_Unregister_RemovesCommand()
    {
        var reg = new EditorCommandRegistry();
        reg.Register(new ToggleBoldCommand());
        var removed = reg.Unregister("ToggleBold");
        Assert.True(removed);
        Assert.Null(reg.Get("ToggleBold"));
    }

    [Fact]
    public void Registry_Execute_ReturnsFalseForUnknownCommand()
    {
        var reg = new EditorCommandRegistry();
        var ctx = MakeCtx();
        Assert.False(reg.Execute("DoesNotExist", ctx));
    }

    [Fact]
    public void Registry_Execute_ReturnsFalseWhenCanExecuteIsFalse()
    {
        var reg = new EditorCommandRegistry();
        // InsertHyperlinkCommand.CanExecute is false when Url is empty (default)
        reg.Register(new InsertHyperlinkCommand());
        var ctx = MakeCtx();
        Assert.False(reg.Execute("InsertHyperlink", ctx));
    }

    [Fact]
    public void Registry_CreateDefault_ContainsAllBuiltInCommands()
    {
        var reg = EditorCommandRegistry.CreateDefault();
        Assert.NotNull(reg.Get("ToggleBold"));
        Assert.NotNull(reg.Get("ToggleItalic"));
        Assert.NotNull(reg.Get("ToggleUnderline"));
        Assert.NotNull(reg.Get("ToggleStrikethrough"));
        Assert.NotNull(reg.Get("ToggleSubscript"));
        Assert.NotNull(reg.Get("ToggleSuperscript"));
        Assert.NotNull(reg.Get("ClearFormatting"));
        Assert.NotNull(reg.Get("AlignLeft"));
        Assert.NotNull(reg.Get("AlignCenter"));
        Assert.NotNull(reg.Get("AlignRight"));
        Assert.NotNull(reg.Get("AlignJustify"));
        Assert.NotNull(reg.Get("ToggleBulletList"));
        Assert.NotNull(reg.Get("ToggleNumberList"));
        Assert.NotNull(reg.Get("IncreaseIndent"));
        Assert.NotNull(reg.Get("DecreaseIndent"));
        Assert.NotNull(reg.Get("Undo"));
        Assert.NotNull(reg.Get("Redo"));
        Assert.NotNull(reg.Get("Copy"));
        Assert.NotNull(reg.Get("Cut"));
        Assert.NotNull(reg.Get("SelectAll"));
        Assert.NotNull(reg.Get("InsertHyperlink"));
        Assert.NotNull(reg.Get("RemoveHyperlink"));
        Assert.NotNull(reg.Get("OpenHyperlink"));
        Assert.NotNull(reg.Get("InsertImage"));
        Assert.NotNull(reg.Get("RemoveImage"));
        Assert.NotNull(reg.Get("InsertTable"));
        Assert.NotNull(reg.Get("ApplyFontFamily"));
        Assert.NotNull(reg.Get("ApplyFontSize"));
        Assert.NotNull(reg.Get("ApplyTextColor"));
        Assert.NotNull(reg.Get("ApplyHighlightColor"));
    }

    // ── FormattingCommands ────────────────────────────────────────────────────

    [Fact]
    public void ToggleBoldCommand_Execute_AppliesBold()
    {
        var doc = MakeDoc("Hello");
        var ctx = MakeCtxWithFullSelection(doc);
        new ToggleBoldCommand().Execute(ctx);
        Assert.True(FirstRun(doc).Style.IsBold);
    }

    [Fact]
    public void ToggleItalicCommand_Execute_AppliesItalic()
    {
        var doc = MakeDoc("Hello");
        var ctx = MakeCtxWithFullSelection(doc);
        new ToggleItalicCommand().Execute(ctx);
        Assert.True(FirstRun(doc).Style.IsItalic);
    }

    [Fact]
    public void ToggleUnderlineCommand_Execute_AppliesUnderline()
    {
        var doc = MakeDoc("Hello");
        var ctx = MakeCtxWithFullSelection(doc);
        new ToggleUnderlineCommand().Execute(ctx);
        Assert.True(FirstRun(doc).Style.IsUnderline);
    }

    [Fact]
    public void ToggleStrikethroughCommand_Execute_AppliesStrikethrough()
    {
        var doc = MakeDoc("Hello");
        var ctx = MakeCtxWithFullSelection(doc);
        new ToggleStrikethroughCommand().Execute(ctx);
        Assert.True(FirstRun(doc).Style.IsStrikethrough);
    }

    [Fact]
    public void ClearFormattingCommand_Execute_RemovesBold()
    {
        var doc = MakeDoc("Hello");
        var fmt = new FormattingEngine();
        var range = FullRange(doc);
        fmt.ToggleBold(doc, range);
        Assert.True(FirstRun(doc).Style.IsBold);

        var sel = new Selection();
        sel.Set(range.Start, range.End);
        var ctx = new EditorContext(doc, sel, new UndoManager(), fmt, new FontFormattingEngine());
        new ClearFormattingCommand().Execute(ctx);

        Assert.False(FirstRun(doc).Style.IsBold);
    }

    [Fact]
    public void FormattingCommands_CanExecute_AlwaysTrue()
    {
        var ctx = MakeCtx();
        Assert.True(new ToggleBoldCommand().CanExecute(ctx));
        Assert.True(new ToggleItalicCommand().CanExecute(ctx));
        Assert.True(new ClearFormattingCommand().CanExecute(ctx));
    }

    // ── ParagraphCommands ─────────────────────────────────────────────────────

    [Fact]
    public void AlignCenterCommand_Execute_SetsCenterAlignment()
    {
        var doc = MakeDoc("Hello");
        var ctx = MakeCtxWithFullSelection(doc);
        new AlignCenterCommand().Execute(ctx);
        var para = (Paragraph)doc.Blocks[0];
        Assert.Equal(TextAlignment.Center, para.Style.Alignment);
    }

    [Fact]
    public void AlignRightCommand_Execute_SetsRightAlignment()
    {
        var doc = MakeDoc("Hello");
        var ctx = MakeCtxWithFullSelection(doc);
        new AlignRightCommand().Execute(ctx);
        var para = (Paragraph)doc.Blocks[0];
        Assert.Equal(TextAlignment.Right, para.Style.Alignment);
    }

    [Fact]
    public void ToggleBulletListCommand_Execute_SetsBulletList()
    {
        var doc = MakeDoc("Item");
        var ctx = MakeCtxWithFullSelection(doc);
        new ToggleBulletListCommand().Execute(ctx);
        var para = (Paragraph)doc.Blocks[0];
        Assert.Equal(ListType.Bullet, para.Style.ListType);
    }

    [Fact]
    public void IncreaseIndentCommand_Execute_IncrementsIndentLevel()
    {
        var doc = MakeDoc("Hello");
        var ctx = MakeCtxWithFullSelection(doc);
        var para = (Paragraph)doc.Blocks[0];
        int before = para.Style.IndentLevel;
        new IncreaseIndentCommand().Execute(ctx);
        Assert.Equal(before + 1, para.Style.IndentLevel);
    }

    // ── EditCommands ──────────────────────────────────────────────────────────

    [Fact]
    public void UndoCommand_CanExecute_FalseWhenStackEmpty()
    {
        var ctx = MakeCtx();
        Assert.False(new UndoCommand().CanExecute(ctx));
    }

    [Fact]
    public void RedoCommand_CanExecute_FalseWhenStackEmpty()
    {
        var ctx = MakeCtx();
        Assert.False(new RedoCommand().CanExecute(ctx));
    }

    [Fact]
    public void CopyCommand_CanExecute_FalseWithEmptySelection()
    {
        var ctx = MakeCtx();
        Assert.False(new CopyCommand().CanExecute(ctx));
    }

    [Fact]
    public void CopyCommand_CanExecute_FalseWithoutClipboard()
    {
        var doc = MakeDoc("Hello");
        var ctx = MakeCtxWithFullSelection(doc);
        Assert.False(new CopyCommand().CanExecute(ctx));
    }

    [Fact]
    public void SelectAllCommand_Execute_SelectsEntireDocument()
    {
        var doc = MakeDoc("Hello world");
        var ctx = MakeCtx(doc: doc);
        new SelectAllCommand().Execute(ctx);
        Assert.False(ctx.Selection.IsEmpty);
        Assert.Equal(new DocumentPosition(0, 0, 0), ctx.Selection.Anchor);
    }

    // ── FontCommands ──────────────────────────────────────────────────────────

    [Fact]
    public void ApplyFontFamilyCommand_Execute_ChangesFontFamily()
    {
        var doc = MakeDoc("Hello");
        var ctx = MakeCtxWithFullSelection(doc);
        var cmd = new ApplyFontFamilyCommand { FontFamily = "Arial" };
        cmd.Execute(ctx);
        Assert.Equal("Arial", FirstRun(doc).Style.FontFamily);
    }

    [Fact]
    public void ApplyFontFamilyCommand_CanExecute_FalseWhenEmpty()
    {
        var cmd = new ApplyFontFamilyCommand { FontFamily = "" };
        Assert.False(cmd.CanExecute(MakeCtx()));
    }

    [Fact]
    public void ApplyFontSizeCommand_Execute_ChangesFontSize()
    {
        var doc = MakeDoc("Hello");
        var ctx = MakeCtxWithFullSelection(doc);
        var cmd = new ApplyFontSizeCommand { FontSize = 24f };
        cmd.Execute(ctx);
        Assert.Equal(24f, FirstRun(doc).Style.FontSize);
    }

    [Fact]
    public void ApplyFontSizeCommand_CanExecute_FalseForNonPositive()
    {
        Assert.False(new ApplyFontSizeCommand { FontSize = 0 }.CanExecute(MakeCtx()));
        Assert.False(new ApplyFontSizeCommand { FontSize = -1 }.CanExecute(MakeCtx()));
    }

    // ── HyperlinkCommands ─────────────────────────────────────────────────────

    [Fact]
    public void InsertHyperlinkCommand_CanExecute_FalseWhenUrlEmpty()
    {
        var cmd = new InsertHyperlinkCommand { Url = "" };
        Assert.False(cmd.CanExecute(MakeCtx()));
    }

    [Fact]
    public void InsertHyperlinkCommand_Execute_InsertsHyperlink()
    {
        var doc = MakeDoc("Click here");
        var sel = new Selection();
        sel.Set(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 10));
        var ctx = new EditorContext(doc, sel, new UndoManager(), new FormattingEngine(), new FontFormattingEngine());
        var cmd = new InsertHyperlinkCommand { Url = "https://example.com" };
        cmd.Execute(ctx);
        var para = (Paragraph)doc.Blocks[0];
        Assert.Contains(para.Inlines, i => i is HyperlinkInline);
    }

    private static EditorContext MakeLinkCtx(EditorDocument doc, int selStart, int selEnd, UndoManager undo)
    {
        var sel = new Selection();
        sel.Set(new DocumentPosition(0, 0, selStart), new DocumentPosition(0, 0, selEnd));
        return MakeCtx(doc, sel, undo);
    }

    private static HyperlinkInline SingleLink(EditorDocument doc)
        => Assert.Single(((Paragraph)doc.Blocks[0]).Inlines.OfType<HyperlinkInline>());

    private static void AssertCaretAtEndOfLink(EditorContext ctx, HyperlinkInline link)
    {
        var para = (Paragraph)ctx.Document.Blocks[0];
        Assert.True(ctx.Selection.IsEmpty);
        Assert.Equal(new DocumentPosition(0, para.Inlines.IndexOf(link), link.Length), ctx.Selection.Active);
    }

    [Fact]
    public void InsertHyperlinkCommand_DisplayText_NoSelection_InsertsLinkWithDisplayTextAtCaret()
    {
        var doc  = MakeDoc("Hello world");
        var undo = new UndoManager();
        var ctx  = MakeLinkCtx(doc, 6, 6, undo);

        new InsertHyperlinkCommand { Url = "https://example.com", DisplayText = "Example" }.Execute(ctx);

        var link = SingleLink(doc);
        Assert.Equal("https://example.com", link.Url);
        Assert.Equal("Example", link.GetText());
        Assert.Equal("Hello Exampleworld", doc.Blocks[0].GetText());
        AssertCaretAtEndOfLink(ctx, link);

        Assert.True(undo.CanUndo);
        undo.Undo();
        Assert.Equal("Hello world", doc.Blocks[0].GetText());
        Assert.Empty(((Paragraph)doc.Blocks[0]).Inlines.OfType<HyperlinkInline>());
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void InsertHyperlinkCommand_DisplayText_WithSelection_ReplacesSelectedText()
    {
        var doc  = MakeDoc("Click here now");
        var undo = new UndoManager();
        var ctx  = MakeLinkCtx(doc, 6, 10, undo);

        new InsertHyperlinkCommand { Url = "https://example.com", DisplayText = "this link" }.Execute(ctx);

        var link = SingleLink(doc);
        Assert.Equal("this link", link.GetText());
        Assert.Equal("Click this link now", doc.Blocks[0].GetText());
        AssertCaretAtEndOfLink(ctx, link);

        undo.Undo();
        Assert.Equal("Click here now", doc.Blocks[0].GetText());
        Assert.Empty(((Paragraph)doc.Blocks[0]).Inlines.OfType<HyperlinkInline>());
        Assert.False(undo.CanUndo);

        undo.Redo();
        Assert.Equal("Click this link now", doc.Blocks[0].GetText());
        Assert.Equal("this link", SingleLink(doc).GetText());
    }

    [Fact]
    public void InsertHyperlinkCommand_NoDisplayText_WithSelection_LinksSelectedText()
    {
        var doc  = MakeDoc("Click here now");
        var undo = new UndoManager();
        var ctx  = MakeLinkCtx(doc, 6, 10, undo);

        new InsertHyperlinkCommand { Url = "https://example.com" }.Execute(ctx);

        var link = SingleLink(doc);
        Assert.Equal("here", link.GetText());
        Assert.Equal("Click here now", doc.Blocks[0].GetText());
        AssertCaretAtEndOfLink(ctx, link);

        undo.Undo();
        Assert.Equal("Click here now", doc.Blocks[0].GetText());
        Assert.Empty(((Paragraph)doc.Blocks[0]).Inlines.OfType<HyperlinkInline>());
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void InsertHyperlinkCommand_DisplayText_Undo_RestoresFormattingOfReplacedText()
    {
        var doc  = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Click ");
        para.AppendRun("here", TextStyle.Default.WithBold(true));
        para.AppendRun(" now");
        var undo = new UndoManager();
        var sel  = new Selection();
        sel.Set(new DocumentPosition(0, 1, 0), new DocumentPosition(0, 1, 4));
        var ctx  = MakeCtx(doc, sel, undo);

        new InsertHyperlinkCommand { Url = "https://example.com", DisplayText = "docs" }.Execute(ctx);
        Assert.Equal("Click docs now", para.GetText());

        undo.Undo();
        Assert.Equal("Click here now", para.GetText());
        var restored = Assert.Single(para.Inlines.OfType<TextRun>(), r => r.Text == "here");
        Assert.True(restored.Style.IsBold);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void InsertHyperlinkCommand_NoDisplayText_NoSelection_InsertsUrlAsText(string displayText)
    {
        var doc  = MakeDoc("Hello world");
        var undo = new UndoManager();
        var ctx  = MakeLinkCtx(doc, 6, 6, undo);

        new InsertHyperlinkCommand { Url = "https://example.com", DisplayText = displayText }.Execute(ctx);

        var link = SingleLink(doc);
        Assert.Equal("https://example.com", link.GetText());
        Assert.Equal("Hello https://example.comworld", doc.Blocks[0].GetText());
        AssertCaretAtEndOfLink(ctx, link);

        undo.Undo();
        Assert.Equal("Hello world", doc.Blocks[0].GetText());
        Assert.False(undo.CanUndo);
    }

    private static EditorContext MakeCaretCtx(EditorDocument doc, DocumentPosition caret, UndoManager undo)
    {
        var sel = new Selection();
        sel.Set(caret, caret);
        return MakeCtx(doc, sel, undo);
    }

    [Fact]
    public void InsertHyperlinkCommand_AfterImage_InsertsLinkAfterTheImage()
    {
        var doc  = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Hi");
        var image = new ImageInline("a.png");
        para.AddInline(image);
        para.AppendRun("there");
        var undo = new UndoManager();

        // Caret at the start of the text after the image.
        new InsertHyperlinkCommand { Url = "https://example.com", DisplayText = "L" }
            .Execute(MakeCaretCtx(doc, new DocumentPosition(0, 2, 0), undo));

        Assert.Same(image, para.Inlines[1]);
        Assert.IsType<HyperlinkInline>(para.Inlines[2]);
        Assert.Equal("Hi￼Lthere", para.GetText());

        // Caret at the end of an image that ends the paragraph.
        var doc2  = new EditorDocument();
        var para2 = (Paragraph)doc2.Blocks[0];
        para2.AppendRun("Hi");
        para2.AddInline(new ImageInline("a.png"));
        new InsertHyperlinkCommand { Url = "https://example.com", DisplayText = "L" }
            .Execute(MakeCaretCtx(doc2, new DocumentPosition(0, 1, 1), new UndoManager()));

        Assert.IsType<ImageInline>(para2.Inlines[1]);
        Assert.IsType<HyperlinkInline>(para2.Inlines[2]);
    }

    [Fact]
    public void InsertHyperlinkCommand_AfterExistingLink_InsertsNewLinkAfterIt()
    {
        var doc   = new EditorDocument();
        var para  = (Paragraph)doc.Blocks[0];
        var first = new HyperlinkInline("https://one.example");
        para.AddInline(first);
        first.AddChild(new TextRun("one"));
        para.AppendRun(" tail");
        var undo = new UndoManager();

        new InsertHyperlinkCommand { Url = "https://two.example", DisplayText = "two" }
            .Execute(MakeCaretCtx(doc, new DocumentPosition(0, 1, 0), undo));

        Assert.Same(first, para.Inlines[0]);
        Assert.Equal("https://two.example", Assert.IsType<HyperlinkInline>(para.Inlines[1]).Url);
        Assert.Equal("onetwo tail", para.GetText());

        undo.Undo();
        Assert.Equal("one tail", para.GetText());
        Assert.Same(first, para.Inlines[0]);
    }

    [Fact]
    public void InsertHyperlinkAction_Undo_KeepsImageIdentity_SoEarlierImageUndoAndRedoWork()
    {
        var doc  = MakeDoc("Hello world");
        var para = (Paragraph)doc.Blocks[0];
        var undo = new UndoManager();

        undo.Push(new InsertImageAction(doc, new DocumentPosition(0, 0, 5), "a.png"));
        var image = Assert.Single(para.Inlines.OfType<ImageInline>());

        // A link elsewhere in the same paragraph (at its end).
        var end = new DocumentPosition(0, para.Inlines.Count - 1, para.Inlines[^1].Length);
        undo.Push(new InsertHyperlinkAction(doc, new TextRange(end, end), "https://example.com", "L"));

        undo.Undo(); // link
        Assert.Same(image, Assert.Single(para.Inlines.OfType<ImageInline>()));
        Assert.Same(para, image.Parent);

        undo.Undo(); // image
        Assert.Empty(para.Inlines.OfType<ImageInline>());
        Assert.Equal("Hello world", para.GetText());

        undo.Redo(); // image
        Assert.Single(para.Inlines.OfType<ImageInline>());
        undo.Redo(); // link
        Assert.Single(para.Inlines.OfType<ImageInline>());
        Assert.Single(para.Inlines.OfType<HyperlinkInline>());
    }

    [Fact]
    public void InsertHyperlinkAction_Undo_PreservesLineBreaksAndLinkChildren()
    {
        var doc  = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("a");
        var lineBreak = new LineBreak();
        para.AddInline(lineBreak);
        var existing = new HyperlinkInline("https://one.example", "title");
        para.AddInline(existing);
        var linkText  = new TextRun("x", TextStyle.Default.WithBold(true));
        var linkImage = new ImageInline("in-link.png", "alt", 10, 20);
        existing.AddChild(linkText);
        existing.AddChild(linkImage);
        para.AppendRun("bc");
        var undo = new UndoManager();

        // Replace "b" (outside the existing link) with a new link, then undo.
        var range = new TextRange(new DocumentPosition(0, 3, 0), new DocumentPosition(0, 3, 1));
        undo.Push(new InsertHyperlinkAction(doc, range, "https://two.example", "new"));
        undo.Undo();

        Assert.Equal(4, para.Inlines.Count);
        Assert.Equal("a", Assert.IsType<TextRun>(para.Inlines[0]).Text);
        Assert.Same(lineBreak, para.Inlines[1]);
        Assert.Same(existing, para.Inlines[2]);
        Assert.Equal("title", existing.Title);
        Assert.Equal(2, existing.Children.Count);
        Assert.Same(linkText, existing.Children[0]);
        Assert.True(linkText.Style.IsBold);
        Assert.Same(linkImage, existing.Children[1]);
        Assert.Equal(20, linkImage.Height);
        Assert.Equal("bc", Assert.IsType<TextRun>(para.Inlines[3]).Text);
        Assert.All(para.Inlines, i => Assert.Same(para, i.Parent));
    }

    [Fact]
    public void InsertHyperlinkAction_UndoAcrossParagraphs_InvalidatesLaterCachedLayouts()
    {
        var doc = new EditorDocument(new Block[] { new Paragraph("One"), new Paragraph("Two"), new Paragraph("Three"), new Paragraph("Four") });
        var cache = new MintedTextEditor.Core.Layout.LayoutCache();
        doc.AddChangeListener(cache);
        var mock = new MockDrawingContext();
        var undo = new UndoManager();

        var range = new TextRange(new DocumentPosition(0, 0, 1), new DocumentPosition(1, 0, 1));
        undo.Push(new InsertHyperlinkAction(doc, range, "https://example.com", "L"));
        Assert.Equal(3, doc.Blocks.Count);
        var merged = cache.GetLayout(doc, 500f, mock); // caches "Three" at 1 and "Four" at 2

        DocumentChangeType? seen = null;
        doc.Changed += (_, e) => seen = e.ChangeType;
        undo.Undo();

        Assert.Equal(DocumentChangeType.BlockSplit, seen);
        Assert.Equal(new[] { "One", "Two", "Three", "Four" }, doc.Blocks.Select(b => b.GetText()));
        var restored = cache.GetLayout(doc, 500f, mock);
        Assert.NotSame(merged.Blocks[2], restored.Blocks[2]); // "Four"'s layout no longer stands in for "Three"
    }

    [Fact]
    public void DeleteRangeAction_UndoAcrossParagraphs_ReportsStructuralChange()
    {
        var doc = new EditorDocument(new Block[] { new Paragraph("One"), new Paragraph("Two"), new Paragraph("Three") });
        var undo = new UndoManager();
        undo.Push(new DeleteRangeAction(doc,
            new TextRange(new DocumentPosition(0, 0, 1), new DocumentPosition(1, 0, 1))));

        DocumentChangeType? seen = null;
        doc.Changed += (_, e) => seen = e.ChangeType;
        undo.Undo();

        Assert.Equal(DocumentChangeType.BlockSplit, seen);
        Assert.Equal(new[] { "One", "Two", "Three" }, doc.Blocks.Select(b => b.GetText()));
    }

    // ── TableCommands ─────────────────────────────────────────────────────────

    [Fact]
    public void InsertTableCommand_Execute_InsertsTableBlock()
    {
        var doc = MakeDoc("Hello");
        var ctx = MakeCtx(doc: doc);
        var cmd = new InsertTableCommand { Rows = 2, Columns = 3 };
        cmd.Execute(ctx);
        Assert.Contains(doc.Blocks, b => b is TableBlock);
    }

    [Fact]
    public void InsertTableCommand_Execute_CorrectRowsAndColumns()
    {
        var doc = MakeDoc("Hello");
        var ctx = MakeCtx(doc: doc);
        var cmd = new InsertTableCommand { Rows = 3, Columns = 4 };
        cmd.Execute(ctx);
        var table = (TableBlock)doc.Blocks.First(b => b is TableBlock);
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal(4, table.Rows[0].Cells.Count);
    }

    [Fact]
    public void InsertTableCommand_CanExecute_FalseForZeroRows()
    {
        Assert.False(new InsertTableCommand { Rows = 0, Columns = 2 }.CanExecute(MakeCtx()));
    }

    // ── ImageCommands ─────────────────────────────────────────────────────────

    [Fact]
    public void InsertImageCommand_CanExecute_FalseWhenSourceEmpty()
    {
        Assert.False(new InsertImageCommand { Source = "" }.CanExecute(MakeCtx()));
    }

    [Fact]
    public void InsertImageCommand_Execute_InsertsImageInline()
    {
        var doc = MakeDoc("Hello");
        var ctx = MakeCtx(doc: doc);
        var cmd = new InsertImageCommand { Source = "image.png", AltText = "alt" };
        cmd.Execute(ctx);
        var para = (Paragraph)doc.Blocks[0];
        Assert.Contains(para.Inlines, i => i is ImageInline);
    }

    [Fact]
    public void RemoveImageCommand_CanExecute_FalseWhenImageIsNull()
    {
        Assert.False(new RemoveImageCommand { Image = null }.CanExecute(MakeCtx()));
    }
}
