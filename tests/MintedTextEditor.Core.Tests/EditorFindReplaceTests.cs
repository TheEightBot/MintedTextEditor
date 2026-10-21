using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;

namespace MintedTextEditor.Core.Tests;

public class EditorFindReplaceTests
{
    private static EditorDocument MakeDocument(params string[] paragraphs)
    {
        var doc = new EditorDocument();
        doc.Blocks.Clear();
        foreach (var text in paragraphs)
            doc.Blocks.Add(new Paragraph(text) { Parent = doc });
        return doc;
    }

    private static (EditorDocument doc, Selection sel, UndoManager undo, EditorFindReplace finder) Setup(params string[] paragraphs)
    {
        var doc = MakeDocument(paragraphs);
        var sel = new Selection();
        var undo = new UndoManager();
        return (doc, sel, undo, new EditorFindReplace(doc, sel, undo));
    }

    private static string SelectedText(EditorDocument doc, Selection sel) =>
        DocumentEditor.GetSelectedText(doc, sel.Range);

    // ── Find ─────────────────────────────────────────────────────

    [Fact]
    public void FindNext_SelectsFirstMatchAfterCaret()
    {
        var (doc, sel, _, finder) = Setup("one two one two");

        var match = finder.FindNext("two");

        Assert.NotNull(match);
        Assert.Equal("two", SelectedText(doc, sel));
        Assert.Equal(4, sel.Range.Start.Offset);
        Assert.Equal(2, finder.MatchCount);
        Assert.Equal(0, finder.CurrentMatchIndex);
    }

    [Fact]
    public void FindNext_IgnoresCaseByDefault()
    {
        var (doc, sel, _, finder) = Setup("Hello HELLO");

        finder.FindNext("hello");

        Assert.Equal("Hello", SelectedText(doc, sel));
        Assert.Equal(2, finder.MatchCount);
    }

    [Fact]
    public void FindNext_MatchCase_SkipsOtherCasings()
    {
        var (doc, sel, _, finder) = Setup("Hello HELLO hello");

        var match = finder.FindNext("hello", new FindOptions { MatchCase = true });

        Assert.NotNull(match);
        Assert.Equal(12, sel.Range.Start.Offset);
        Assert.Equal(1, finder.MatchCount);
    }

    [Fact]
    public void FindNext_AdvancesPastSelectedMatch_AcrossParagraphs()
    {
        var (_, sel, _, finder) = Setup("cat", "a cat");

        finder.FindNext("cat");
        finder.FindNext("cat");

        Assert.Equal(1, sel.Range.Start.BlockIndex);
        Assert.Equal(1, finder.CurrentMatchIndex);
    }

    [Fact]
    public void FindNext_WrapsToFirstMatch()
    {
        var (_, sel, _, finder) = Setup("ab ab");

        finder.FindNext("ab");
        finder.FindNext("ab");
        var wrapped = finder.FindNext("ab");

        Assert.NotNull(wrapped);
        Assert.Equal(0, sel.Range.Start.Offset);
        Assert.Equal(0, finder.CurrentMatchIndex);
    }

    [Fact]
    public void FindNext_WithoutWrap_ReturnsNullAndKeepsSelection()
    {
        var (_, sel, _, finder) = Setup("ab ab");
        var options = new FindOptions { WrapAround = false };

        finder.FindNext("ab", options);
        finder.FindNext("ab", options);
        var none = finder.FindNext("ab", options);

        Assert.Null(none);
        Assert.Equal(3, sel.Range.Start.Offset);
        Assert.Equal(1, finder.CurrentMatchIndex);
    }

    [Fact]
    public void FindPrevious_WithoutWrap_KeepsIndexOfSelectedMatch()
    {
        var (_, sel, _, finder) = Setup("ab ab");
        var options = new FindOptions { WrapAround = false };

        finder.FindNext("ab", options);
        var none = finder.FindPrevious("ab", options);

        Assert.Null(none);
        Assert.Equal(0, sel.Range.Start.Offset);
        Assert.Equal(0, finder.CurrentMatchIndex);
        Assert.Equal(2, finder.MatchCount);
    }

    [Fact]
    public void FindPrevious_FromStart_WrapsToLastMatch()
    {
        var (_, sel, _, finder) = Setup("ab cd ab");

        var match = finder.FindPrevious("ab");

        Assert.NotNull(match);
        Assert.Equal(6, sel.Range.Start.Offset);
        Assert.Equal(1, finder.CurrentMatchIndex);
    }

    [Fact]
    public void FindPrevious_MovesBeforeSelectedMatch()
    {
        var (_, sel, _, finder) = Setup("ab cd ab");

        finder.FindNext("ab");
        finder.FindNext("ab");
        finder.FindPrevious("ab");

        Assert.Equal(0, sel.Range.Start.Offset);
    }

    [Fact]
    public void FindNext_NoMatch_ReturnsNull()
    {
        var (_, sel, _, finder) = Setup("hello");

        Assert.Null(finder.FindNext("xyz"));
        Assert.True(sel.IsEmpty);
        Assert.Equal(0, finder.MatchCount);
    }

    [Fact]
    public void FindNext_SkipsTableBlocks()
    {
        var doc = MakeDocument("before");
        var table = new TableBlock(1, 1);
        table.Rows[0].Cells[0].Blocks.Clear();
        table.Rows[0].Cells[0].Blocks.Add(new Paragraph("before"));
        doc.Blocks.Add(table);
        var finder = new EditorFindReplace(doc, new Selection());

        finder.FindNext("before");

        Assert.Equal(1, finder.MatchCount);
    }

    // ── Replace ──────────────────────────────────────────────────

    [Fact]
    public void Replace_ReplacesSelectedMatchAndSelectsNext()
    {
        var (doc, sel, _, finder) = Setup("cat cat");

        finder.FindNext("cat");
        bool replaced = finder.Replace("cat", "dog");

        Assert.True(replaced);
        Assert.Equal("dog cat", doc.GetText());
        Assert.Equal("cat", SelectedText(doc, sel));
        Assert.Equal(4, sel.Range.Start.Offset);
    }

    [Fact]
    public void Replace_WithoutSelectedMatch_ReplacesNextMatch()
    {
        var (doc, _, _, finder) = Setup("a cat");

        Assert.True(finder.Replace("cat", "dog"));
        Assert.Equal("a dog", doc.GetText());
    }

    [Fact]
    public void Replace_IsSingleUndoStep()
    {
        var (doc, _, undo, finder) = Setup("hello world");

        finder.Replace("world", "there");
        Assert.Equal("hello there", doc.GetText());

        undo.Undo();

        Assert.Equal("hello world", doc.GetText());
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void Replace_KeepsMatchFormatting()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("plain ");
        var bold = TextStyle.Default.WithBold(true);
        para.AppendRun("word", bold);
        var undo = new UndoManager();
        var finder = new EditorFindReplace(doc, new Selection(), undo);

        finder.Replace("word", "term");

        Assert.Equal("plain term", doc.GetText());
        var run = Assert.Single(para.Inlines.OfType<TextRun>(), r => r.Text == "term");
        Assert.True(run.Style.IsBold);

        undo.Undo();
        Assert.Equal("plain word", doc.GetText());
        Assert.True(Assert.Single(para.Inlines.OfType<TextRun>(), r => r.Text == "word").Style.IsBold);

        undo.Redo();
        Assert.Equal("plain term", doc.GetText());
        Assert.True(Assert.Single(para.Inlines.OfType<TextRun>(), r => r.Text == "term").Style.IsBold);
    }

    [Fact]
    public void ReplaceAll_KeepsEachMatchFormatting_AndUndoRedoRestore()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        var bold = TextStyle.Default.WithBold(true);
        para.AppendRun("word ");
        para.AppendRun("word", bold);
        para.AppendRun(" and word");
        var undo = new UndoManager();
        var finder = new EditorFindReplace(doc, new Selection(), undo);

        Assert.Equal(3, finder.ReplaceAll("word", "term"));

        Assert.Equal("term term and term", doc.GetText());
        Assert.True(Assert.Single(para.Inlines.OfType<TextRun>(), r => r.Style.IsBold).Text == "term");

        undo.Undo();
        Assert.Equal("word word and word", doc.GetText());
        Assert.True(Assert.Single(para.Inlines.OfType<TextRun>(), r => r.Style.IsBold).Text == "word");

        undo.Redo();
        Assert.Equal("term term and term", doc.GetText());
        Assert.True(Assert.Single(para.Inlines.OfType<TextRun>(), r => r.Style.IsBold).Text == "term");
    }

    [Fact]
    public void Replace_InsideHyperlink_KeepsReplacementInLink()
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("see ");
        var link = new HyperlinkInline("https://example.com");
        para.AddInline(link);
        link.AddChild(new TextRun("docs here"));
        var finder = new EditorFindReplace(doc, new Selection(), new UndoManager());

        Assert.True(finder.Replace("docs", "notes"));

        Assert.Equal("see notes here", doc.GetText());
        Assert.Equal("notes here", Assert.Single(para.Inlines.OfType<HyperlinkInline>()).GetText());
    }

    [Fact]
    public void Replace_NoMatch_ReturnsFalse()
    {
        var (doc, _, undo, finder) = Setup("hello");

        Assert.False(finder.Replace("xyz", "abc"));
        Assert.Equal("hello", doc.GetText());
        Assert.False(undo.CanUndo);
    }

    // ── ReplaceAll ───────────────────────────────────────────────

    [Fact]
    public void ReplaceAll_ReplacesEveryMatchAndReturnsCount()
    {
        var (doc, _, _, finder) = Setup("cat sat", "mat cat");

        int count = finder.ReplaceAll("at", "og");

        Assert.Equal(4, count);
        Assert.Equal("cog sog\nmog cog", doc.GetText());
        Assert.Equal(0, finder.MatchCount);
    }

    [Fact]
    public void ReplaceAll_RespectsMatchCase()
    {
        var (doc, _, _, finder) = Setup("Cat cat");

        int count = finder.ReplaceAll("cat", "dog", new FindOptions { MatchCase = true });

        Assert.Equal(1, count);
        Assert.Equal("Cat dog", doc.GetText());
    }

    [Fact]
    public void ReplaceAll_WithEmptyReplacement_DeletesMatches()
    {
        var (doc, _, _, finder) = Setup("a-b-c");

        Assert.Equal(2, finder.ReplaceAll("-", ""));
        Assert.Equal("abc", doc.GetText());
    }

    [Fact]
    public void ReplaceAll_UndoRestoresTextInOneStep_AndRedoReapplies()
    {
        var (doc, _, undo, finder) = Setup("one fish two fish", "red fish");

        finder.ReplaceAll("fish", "cat");
        Assert.Equal("one cat two cat\nred cat", doc.GetText());

        undo.Undo();
        Assert.Equal("one fish two fish\nred fish", doc.GetText());
        Assert.False(undo.CanUndo);

        undo.Redo();
        Assert.Equal("one cat two cat\nred cat", doc.GetText());
    }

    [Fact]
    public void ReplaceAll_ManyMatchesInOneParagraph_IsOneStepWithOneSnapshotPerParagraph()
    {
        string original = string.Concat(Enumerable.Repeat("ab", 2_000));
        var (doc, _, undo, finder) = Setup(original, "xa");

        Assert.Equal(2_001, finder.ReplaceAll("a", "c"));
        string replaced = string.Concat(Enumerable.Repeat("cb", 2_000)) + "\nxc";
        Assert.Equal(replaced, doc.GetText());

        Assert.True(undo.CanUndo);
        undo.Undo();
        Assert.False(undo.CanUndo);
        Assert.Equal(original + "\nxa", doc.GetText());

        undo.Redo();
        Assert.Equal(replaced, doc.GetText());
    }

    [Fact]
    public void ReplaceMatchesAction_HoldsTwoSnapshotsPerParagraph_NotOnePerMatch()
    {
        var doc = MakeDocument(string.Concat(Enumerable.Repeat("ab", 500)), "xa");
        var matches = new FindReplaceEngine(doc).FindAll("a");
        Assert.Equal(501, matches.Count);

        var action = new ReplaceMatchesAction(doc, matches, "c");
        action.Execute();
        action.Undo();
        action.Redo();

        // One "before" and one "after" snapshot for each of the two paragraphs.
        Assert.Equal(4, action.SnapshotCount);
    }

    [Fact]
    public void ReplaceAll_ReplacementContainingQuery_DoesNotLoop()
    {
        var (doc, _, _, finder) = Setup("a a");

        Assert.Equal(2, finder.ReplaceAll("a", "aa"));
        Assert.Equal("aa aa", doc.GetText());
    }

    [Fact]
    public void ReplaceAll_NoMatch_ReturnsZeroAndPushesNothing()
    {
        var (_, _, undo, finder) = Setup("hello");

        Assert.Equal(0, finder.ReplaceAll("xyz", "abc"));
        Assert.False(undo.CanUndo);
    }
}
