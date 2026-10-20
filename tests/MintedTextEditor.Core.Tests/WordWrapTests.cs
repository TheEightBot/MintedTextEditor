using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Layout;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Tests;

public class WordWrapTests
{
    private readonly MockDrawingContext _ctx = new();

    // ── WordWrap = true (default) ────────────────────────────────

    [Fact]
    public void WordWrap_Enabled_WrapsLongLine()
    {
        var engine = new TextLayoutEngine { WordWrap = true };
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        // Append many words that collectively exceed 200px
        para.AppendRun("one two three four five six seven eight nine ten");

        var layout = engine.Layout(doc, 200f, _ctx);

        // With wrapping at 200px the content should produce more than one line
        Assert.True(layout.Blocks[0].Lines.Count > 1,
            "Expected multiple lines when word-wrap is enabled with narrow width.");
    }

    [Theory]
    [InlineData(60f)]   // room for one emoji and part of the next
    [InlineData(30f)]   // narrower than a single emoji: still never split it
    public void WordWrap_LongUnbrokenEmojiRun_BreaksOnlyBetweenGraphemes(float width)
    {
        // 👩‍💻 is 5 UTF-16 code units (40px in the mock); a break inside it would split the
        // ZWJ sequence or a surrogate pair.
        var engine = new TextLayoutEngine { WordWrap = true };
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun(string.Concat(Enumerable.Repeat("👩\u200D💻", 4)));

        var layout = engine.Layout(doc, width, _ctx);

        var runs = layout.Blocks[0].Lines.SelectMany(l => l.Runs).ToList();
        Assert.True(runs.Count > 1);
        Assert.All(runs, r => Assert.Equal(0, r.Text.Length % 5));
    }

    [Fact]
    public void WordWrap_Enabled_ShortLine_FitsOnOneLine()
    {
        var engine = new TextLayoutEngine { WordWrap = true };
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun("Hi");

        var layout = engine.Layout(doc, 500f, _ctx);

        Assert.Single(layout.Blocks[0].Lines);
    }

    // ── WordWrap = false ─────────────────────────────────────────

    [Fact]
    public void WordWrap_Disabled_KeepsLongLineOnOneLine()
    {
        var engine = new TextLayoutEngine { WordWrap = false };
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("one two three four five six seven eight nine ten");

        var layout = engine.Layout(doc, 200f, _ctx);

        Assert.True(layout.Blocks[0].Lines.Count == 1,
            "Expected a single line when word-wrap is disabled, regardless of width.");
    }

    [Fact]
    public void WordWrap_Disabled_MultipleRuns_AllOnOneLine()
    {
        var engine = new TextLayoutEngine { WordWrap = false };
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Alpha ");
        para.AppendRun("Beta ");
        para.AppendRun("Gamma Delta Epsilon Zeta Eta Theta Iota Kappa");

        var layout = engine.Layout(doc, 150f, _ctx);

        Assert.Single(layout.Blocks[0].Lines);
    }

    // ── Toggle ───────────────────────────────────────────────────

    [Fact]
    public void WordWrap_DefaultIsTrue()
    {
        var engine = new TextLayoutEngine();
        Assert.True(engine.WordWrap);
    }

    [Fact]
    public void WordWrap_Toggle_ChangesLayoutLineCount()
    {
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun(
            "The quick brown fox jumps over the lazy dog again and again");

        var engine = new TextLayoutEngine();

        engine.WordWrap = true;
        var wrappedLayout = engine.Layout(doc, 200f, _ctx);

        engine.WordWrap = false;
        var unwrappedLayout = engine.Layout(doc, 200f, _ctx);

        Assert.True(wrappedLayout.Blocks[0].Lines.Count >
                    unwrappedLayout.Blocks[0].Lines.Count,
            "Wrapped layout should have more lines than unwrapped.");
    }
}
