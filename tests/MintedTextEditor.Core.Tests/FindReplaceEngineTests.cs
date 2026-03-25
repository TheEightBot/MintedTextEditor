using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;

namespace MintedTextEditor.Core.Tests;

public class FindReplaceEngineTests
{
    private static EditorDocument MakeDocument(params string[] paragraphs)
    {
        var doc = new EditorDocument();
        doc.Blocks.Clear();
        foreach (var text in paragraphs)
            doc.Blocks.Add(new Paragraph(text) { Parent = doc });
        return doc;
    }

    // ── Find ─────────────────────────────────────────────────────

    [Fact]
    public void Find_MatchExists_ReturnsSingleMatch()
    {
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun("Hello world");
        var engine = new FindReplaceEngine(doc);

        var matches = engine.FindAll("world");

        Assert.Single(matches);
        Assert.Equal("world", matches[0].Text);
    }

    [Fact]
    public void Find_NoMatch_ReturnsEmptyList()
    {
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun("Hello world");
        var engine = new FindReplaceEngine(doc);

        var matches = engine.FindAll("xyz");

        Assert.Empty(matches);
    }

    [Fact]
    public void Find_MultipleOccurrences_ReturnsAll()
    {
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun("the cat sat on the mat");
        var engine = new FindReplaceEngine(doc);

        var matches = engine.FindAll("the");

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void Find_CaseInsensitive_FindsDifferentCasings()
    {
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun("Hello HELLO hello");
        var engine = new FindReplaceEngine(doc);

        var matches = engine.FindAll("hello");

        Assert.Equal(3, matches.Count);
    }

    [Fact]
    public void Find_CaseSensitive_SkipsWrongCasings()
    {
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun("Hello HELLO hello");
        var engine = new FindReplaceEngine(doc);

        var options = new FindOptions { MatchCase = true };
        var matches = engine.FindAll("hello", options);

        Assert.Single(matches);
    }

    [Fact]
    public void Find_WholeWord_SkipsPartialMatches()
    {
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun("cat concatenate cats");
        var engine = new FindReplaceEngine(doc);

        var options = new FindOptions { WholeWord = true };
        var matches = engine.FindAll("cat", options);

        Assert.Single(matches);
    }

    [Fact]
    public void Find_AcrossMultipleParagraphs_FindsAllOccurrences()
    {
        var doc = MakeDocument("First match here", "Second match here");
        var engine = new FindReplaceEngine(doc);

        var matches = engine.FindAll("match");

        Assert.Equal(2, matches.Count);
    }

    // ── FindNext / FindPrevious ───────────────────────────────────

    [Fact]
    public void FindNext_AdvancesCurrentMatch()
    {
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun("aaa aaa aaa");
        var engine = new FindReplaceEngine(doc);

        var first  = engine.Find("aaa");
        var second = engine.FindNext();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first!.Range.Start.Offset, second!.Range.Start.Offset);
    }

    [Fact]
    public void FindPrevious_ReturnsPreviousMatch()
    {
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun("aaa bbb aaa");
        var engine = new FindReplaceEngine(doc);

        engine.Find("aaa");       // first
        engine.FindNext();        // second
        var prev = engine.FindPrevious(); // back to first

        Assert.NotNull(prev);
        Assert.Equal(0, prev!.Range.Start.Offset);
    }

    // ── Replace ──────────────────────────────────────────────────

    [Fact]
    public void Replace_ReplacesCurrentMatch()
    {
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun("Hello world");
        var engine = new FindReplaceEngine(doc);

        // Replace() requires CurrentMatch to be set first via Find().
        engine.Find("world");
        engine.Replace("world", "earth");

        Assert.Contains("earth", doc.GetText());
        Assert.DoesNotContain("world", doc.GetText());
    }

    [Fact]
    public void ReplaceAll_ReplacesEveryOccurrence()
    {
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun("cat sat mat");
        var engine = new FindReplaceEngine(doc);

        int count = engine.ReplaceAll("at", "og");

        Assert.Equal(3, count);
        Assert.Equal("cog sog mog", doc.GetText().Trim());
    }

    [Fact]
    public void ReplaceAll_NothingToReplace_ReturnsZero()
    {
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun("Hello");
        var engine = new FindReplaceEngine(doc);

        int count = engine.ReplaceAll("xyz", "abc");

        Assert.Equal(0, count);
    }
}
