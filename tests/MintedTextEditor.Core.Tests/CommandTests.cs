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
