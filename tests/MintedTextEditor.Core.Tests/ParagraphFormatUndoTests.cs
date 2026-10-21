using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Tests;

/// <summary>
/// Paragraph commands record their change on the <see cref="UndoManager"/>; undo restores
/// the previous <see cref="ParagraphStyle"/> and redo reapplies the change.
/// </summary>
public class ParagraphFormatUndoTests
{
    private static EditorDocument MakeDoc(params string[] paragraphs)
    {
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun(paragraphs[0]);
        for (int i = 1; i < paragraphs.Length; i++)
            doc.AddBlock(new Paragraph(paragraphs[i]));
        return doc;
    }

    private static Paragraph Para(EditorDocument doc, int index) => (Paragraph)doc.Blocks[index];

    private static EditorContext MakeCtx(EditorDocument doc, int startBlock = 0, int endBlock = 0)
    {
        var sel = new Selection();
        sel.Set(new DocumentPosition(startBlock, 0, 0),
                new DocumentPosition(endBlock, 0, Para(doc, endBlock).GetText().Length));
        return new EditorContext(doc, sel, new UndoManager(), new FormattingEngine(), new FontFormattingEngine());
    }

    private static string StyleKey(ParagraphStyle s) =>
        $"{s.Alignment}|{s.IndentLevel}|{s.LineSpacing}|{s.SpaceBefore}|{s.SpaceAfter}|{s.ListType}|{s.HeadingLevel}|{s.Direction}|{s.IsBlockQuote}";

    public static TheoryData<IEditorCommand, Action<ParagraphStyle>, Func<ParagraphStyle, bool>> Commands => new()
    {
        { new AlignLeftCommand(),            s => s.Alignment = TextAlignment.Right, s => s.Alignment == TextAlignment.Left },
        { new AlignCenterCommand(),          _ => { },                               s => s.Alignment == TextAlignment.Center },
        { new AlignRightCommand(),           _ => { },                               s => s.Alignment == TextAlignment.Right },
        { new AlignJustifyCommand(),         _ => { },                               s => s.Alignment == TextAlignment.Justify },
        { new ToggleBulletListCommand(),     _ => { },                               s => s.ListType == ListType.Bullet },
        { new ToggleBulletListCommand(),     s => s.ListType = ListType.Bullet,      s => s.ListType == ListType.None },
        { new ToggleNumberListCommand(),     _ => { },                               s => s.ListType == ListType.Number },
        { new IncreaseIndentCommand(),       _ => { },                               s => s.IndentLevel == 1 },
        { new DecreaseIndentCommand(),       s => s.IndentLevel = 2,                 s => s.IndentLevel == 1 },
        { new SetHeadingLevelCommand(2),     _ => { },                               s => s.HeadingLevel == 2 },
        { new SetHeadingLevelCommand(0),     s => s.HeadingLevel = 3,                s => s.HeadingLevel == 0 },
        { new SetParagraphFormatCommand("Heading1"), s => s.IsBlockQuote = true,     s => s.HeadingLevel == 1 && !s.IsBlockQuote },
        { new SetParagraphFormatCommand("Quote"),    s => s.HeadingLevel = 4,        s => s.IsBlockQuote && s.HeadingLevel == 0 },
        { new SetParagraphFormatCommand("Normal"),   s => { s.HeadingLevel = 2; s.IsBlockQuote = true; }, s => s.HeadingLevel == 0 && !s.IsBlockQuote },
    };

    [Theory]
    [MemberData(nameof(Commands))]
    public void Command_IsRecorded_UndoRestores_RedoReapplies(
        IEditorCommand command, Action<ParagraphStyle> arrange, Func<ParagraphStyle, bool> applied)
    {
        var doc = MakeDoc("Hello");
        arrange(Para(doc, 0).Style);
        var original = StyleKey(Para(doc, 0).Style);
        var ctx = MakeCtx(doc);

        command.Execute(ctx);
        Assert.True(applied(Para(doc, 0).Style));
        Assert.True(ctx.UndoManager.CanUndo);

        ctx.UndoManager.Undo();
        Assert.Equal(original, StyleKey(Para(doc, 0).Style));

        ctx.UndoManager.Redo();
        Assert.True(applied(Para(doc, 0).Style));
    }

    [Fact]
    public void MultiParagraphSelection_UndoneInOneStep()
    {
        var doc = MakeDoc("One", "Two", "Three");
        Para(doc, 1).Style.Alignment = TextAlignment.Right;
        Para(doc, 2).Style.HeadingLevel = 2;
        var originals = Enumerable.Range(0, 3).Select(i => StyleKey(Para(doc, i).Style)).ToList();
        var ctx = MakeCtx(doc, 0, 2);

        new AlignCenterCommand().Execute(ctx);
        Assert.All(doc.Blocks.Cast<Paragraph>(), p => Assert.Equal(TextAlignment.Center, p.Style.Alignment));

        ctx.UndoManager.Undo();
        Assert.False(ctx.UndoManager.CanUndo);
        Assert.Equal(originals, Enumerable.Range(0, 3).Select(i => StyleKey(Para(doc, i).Style)).ToList());

        ctx.UndoManager.Redo();
        Assert.All(doc.Blocks.Cast<Paragraph>(), p => Assert.Equal(TextAlignment.Center, p.Style.Alignment));
        Assert.Equal(2, Para(doc, 2).Style.HeadingLevel);
    }

    [Fact]
    public void ToggleList_MixedSelection_UndoRestoresEachParagraph()
    {
        var doc = MakeDoc("One", "Two");
        Para(doc, 0).Style.ListType = ListType.Bullet;
        var ctx = MakeCtx(doc, 0, 1);

        new ToggleBulletListCommand().Execute(ctx);
        Assert.Equal(ListType.Bullet, Para(doc, 1).Style.ListType);

        ctx.UndoManager.Undo();
        Assert.Equal(ListType.Bullet, Para(doc, 0).Style.ListType);
        Assert.Equal(ListType.None, Para(doc, 1).Style.ListType);
    }

    [Fact]
    public void SuccessiveCommands_AreSeparateUndoSteps()
    {
        var doc = MakeDoc("Hello");
        var ctx = MakeCtx(doc);

        new IncreaseIndentCommand().Execute(ctx);
        new IncreaseIndentCommand().Execute(ctx);
        new AlignRightCommand().Execute(ctx);

        ctx.UndoManager.Undo();
        Assert.Equal(TextAlignment.Left, Para(doc, 0).Style.Alignment);
        Assert.Equal(2, Para(doc, 0).Style.IndentLevel);

        ctx.UndoManager.Undo();
        Assert.Equal(1, Para(doc, 0).Style.IndentLevel);

        ctx.UndoManager.Undo();
        Assert.Equal(0, Para(doc, 0).Style.IndentLevel);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [Fact]
    public void Undo_DoesNotAliasStylesBetweenUndoAndRedo()
    {
        var doc = MakeDoc("Hello");
        var ctx = MakeCtx(doc);

        new IncreaseIndentCommand().Execute(ctx);
        ctx.UndoManager.Undo();
        Para(doc, 0).Style.IndentLevel = 5; // mutate the restored style in place
        ctx.UndoManager.Redo();
        Assert.Equal(1, Para(doc, 0).Style.IndentLevel);

        ctx.UndoManager.Undo();
        Assert.Equal(0, Para(doc, 0).Style.IndentLevel);
    }

    [Fact]
    public void ApplyParagraphStyleAction_WithEngine_LineSpacingIsUndoable()
    {
        var doc = MakeDoc("One", "Two");
        var range = new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(1, 0, 3));
        var undo = new UndoManager();

        undo.Push(new ApplyParagraphStyleAction(doc, range,
            (d, r) => ParagraphFormattingEngine.SetLineSpacing(d, r, 1.5f)));
        Assert.Equal(1.5f, Para(doc, 0).Style.LineSpacing);
        Assert.Equal(1.5f, Para(doc, 1).Style.LineSpacing);

        undo.Undo();
        Assert.Equal(1.0f, Para(doc, 0).Style.LineSpacing);
        Assert.Equal(1.0f, Para(doc, 1).Style.LineSpacing);

        undo.Redo();
        Assert.Equal(1.5f, Para(doc, 1).Style.LineSpacing);
    }

    [Fact]
    public void ApplyParagraphStyleAction_UndoRaisesStyleChanged()
    {
        var doc = MakeDoc("Hello");
        var ctx = MakeCtx(doc);
        new AlignCenterCommand().Execute(ctx);

        DocumentChangeType? seen = null;
        doc.Changed += (_, e) => seen = e.ChangeType;
        ctx.UndoManager.Undo();

        Assert.Equal(DocumentChangeType.StyleChanged, seen);
    }

    [Fact]
    public void SelectionStartingAtTopLevelTable_FormatsEveryFollowingParagraph()
    {
        // [table, A, B] with Select All: the start endpoint is the table block itself.
        var doc = MakeDoc("x", "A", "B");
        doc.Blocks.RemoveAt(0);
        doc.InsertBlock(0, new TableBlock(1, 1));
        var sel = new Selection();
        sel.Set(new DocumentPosition(0, 0, 0), new DocumentPosition(2, 0, 1));
        var ctx = new EditorContext(doc, sel, new UndoManager(), new FormattingEngine(), new FontFormattingEngine());

        new AlignCenterCommand().Execute(ctx);
        Assert.Equal(TextAlignment.Center, Para(doc, 1).Style.Alignment);
        Assert.Equal(TextAlignment.Center, Para(doc, 2).Style.Alignment);

        ctx.UndoManager.Undo();
        Assert.Equal(TextAlignment.Left, Para(doc, 1).Style.Alignment);
        Assert.Equal(TextAlignment.Left, Para(doc, 2).Style.Alignment);

        ctx.UndoManager.Redo();
        Assert.Equal(TextAlignment.Center, Para(doc, 1).Style.Alignment);
        Assert.Equal(TextAlignment.Center, Para(doc, 2).Style.Alignment);
    }

    [Fact]
    public void SelectionEndingAtTopLevelTable_DoesNotFormatFollowingParagraph()
    {
        // [A, table, B] with the selection ending on the table block.
        var doc = MakeDoc("A", "x", "B");
        doc.Blocks.RemoveAt(1);
        doc.InsertBlock(1, new TableBlock(1, 1));
        var sel = new Selection();
        sel.Set(new DocumentPosition(0, 0, 0), new DocumentPosition(1, 0, 0));
        var ctx = new EditorContext(doc, sel, new UndoManager(), new FormattingEngine(), new FontFormattingEngine());

        new AlignCenterCommand().Execute(ctx);
        Assert.Equal(TextAlignment.Center, Para(doc, 0).Style.Alignment);
        Assert.Equal(TextAlignment.Left, Para(doc, 2).Style.Alignment);

        ctx.UndoManager.Undo();
        Assert.Equal(TextAlignment.Left, Para(doc, 0).Style.Alignment);

        ctx.UndoManager.Redo();
        Assert.Equal(TextAlignment.Center, Para(doc, 0).Style.Alignment);
        Assert.Equal(TextAlignment.Left, Para(doc, 2).Style.Alignment);
    }

    [Fact]
    public void Engine_IsNotRecordedOnUndoManager()
    {
        var doc = MakeDoc("Hello");
        var ctx = MakeCtx(doc);

        ParagraphFormattingEngine.SetAlignment(doc, ctx.Selection.Range, TextAlignment.Center);

        Assert.Equal(TextAlignment.Center, Para(doc, 0).Style.Alignment);
        Assert.False(ctx.UndoManager.CanUndo);
    }
}
