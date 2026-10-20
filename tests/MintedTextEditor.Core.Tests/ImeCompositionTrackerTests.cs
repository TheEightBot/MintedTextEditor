using MintedTextEditor.Core.Input;

namespace MintedTextEditor.Core.Tests;

public class ImeCompositionTrackerTests
{
    /// <summary>Records sink calls and applies them to a plain string so tests can assert on the result.</summary>
    private sealed class FakeSink : IImeEditSink
    {
        public List<string> Ops { get; } = new();
        public string Text = string.Empty;
        public int SelStart;
        public int SelEnd;

        public FakeSink(string text = "", int selStart = -1, int selEnd = -1)
        {
            Text = text;
            SelStart = selStart < 0 ? text.Length : selStart;
            SelEnd = selEnd < 0 ? SelStart : selEnd;
        }

        public ImeTextContext Context => new(Text, SelStart, SelEnd);

        public void InsertText(string text)
        {
            Ops.Add($"insert:{text}");
            Text = Text[..SelStart] + text + Text[SelEnd..];
            SelStart = SelEnd = SelStart + text.Length;
        }

        public void DeleteBefore(int count)
        {
            Ops.Add($"before:{count}");
            count = Math.Min(count, SelStart);
            Text = Text[..(SelStart - count)] + Text[SelStart..];
            SelStart -= count;
            SelEnd -= count;
        }

        public void DeleteAfter(int count)
        {
            Ops.Add($"after:{count}");
            count = Math.Min(count, Text.Length - SelEnd);
            Text = Text[..SelEnd] + Text[(SelEnd + count)..];
        }

        public void SetSelection(int start, int end)
        {
            Ops.Add($"select:{start},{end}");
            SelStart = start;
            SelEnd = end;
        }

        public void Enter()
        {
            Ops.Add("enter");
            InsertText("\n");
            Ops.RemoveAt(Ops.Count - 1);
        }
    }

    [Fact]
    public void SamsungStyleComposing_InsertsOnlyNewCharacters()
    {
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);

        t.SetComposingText("h");
        t.SetComposingText("he");
        t.SetComposingText("hel");
        t.CommitText("hel ");

        Assert.Equal(new[] { "insert:h", "insert:e", "insert:l", "insert: " }, sink.Ops);
        Assert.Equal("hel ", sink.Text);
        Assert.Equal(string.Empty, t.ComposingText);
    }

    [Fact]
    public void Autocorrect_ReplacesOnlyTheChangedSuffix()
    {
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);

        t.SetComposingText("teh");
        sink.Ops.Clear();
        t.CommitText("the ");

        Assert.Equal(new[] { "before:2", "insert:he " }, sink.Ops);
        Assert.Equal("the ", sink.Text);
    }

    [Fact]
    public void ShrinkingComposition_DeletesDifference()
    {
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);

        t.SetComposingText("help");
        sink.Ops.Clear();
        t.SetComposingText("he");

        Assert.Equal(new[] { "before:2" }, sink.Ops);
        Assert.Equal("he", sink.Text);
    }

    [Fact]
    public void SetComposingRegion_AdoptsExistingWord_NoDuplication()
    {
        // R2: caret after an existing word; Samsung re-composes it before editing.
        var sink = new FakeSink("say hello");
        var t = new ImeCompositionTracker(sink);

        t.SetComposingRegion(4, 9, sink.Context);
        t.SetComposingText("hello");
        Assert.Empty(sink.Ops);

        t.SetComposingText("help");
        Assert.Equal(new[] { "before:2", "insert:p" }, sink.Ops);
        Assert.Equal("say help", sink.Text);
    }

    [Fact]
    public void SetComposingRegion_AwayFromCaret_MovesCaretToRegionEnd()
    {
        var sink = new FakeSink("say hello world", selStart: 15);
        var t = new ImeCompositionTracker(sink);

        t.SetComposingRegion(4, 9, sink.Context);

        Assert.Equal(new[] { "select:9,9" }, sink.Ops);
        Assert.Equal("hello", t.ComposingText);
    }

    [Fact]
    public void SetComposingRegion_OutOfRange_IsClamped()
    {
        var sink = new FakeSink("abc");
        var t = new ImeCompositionTracker(sink);

        t.SetComposingRegion(9, 1, sink.Context);

        Assert.Equal("bc", t.ComposingText);
    }

    [Fact]
    public void CommitNewline_IsPassedThroughAsText()
    {
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);

        t.CommitText("\n");

        Assert.Equal(new[] { "insert:\n" }, sink.Ops);
    }

    [Fact]
    public void DeleteSurrounding_WithSelection_KeepsSelection()
    {
        // R6: Android semantics delete around the selection, never the selection itself.
        var sink = new FakeSink("abcdef", selStart: 2, selEnd: 4);
        var t = new ImeCompositionTracker(sink);

        t.DeleteSurrounding(1, 1);

        Assert.Equal(new[] { "before:1", "after:1" }, sink.Ops);
        Assert.Equal("acd", sink.Text[..3]);
        Assert.Equal("acdf", sink.Text);
    }

    [Fact]
    public void DeleteSurrounding_InsideComposition_TrimsComposingMirror()
    {
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);
        t.SetComposingText("word");

        t.DeleteSurrounding(1, 0);
        sink.Ops.Clear();
        t.SetComposingText("wors");

        Assert.Equal(new[] { "insert:s" }, sink.Ops);
        Assert.Equal("wors", sink.Text);
    }

    [Fact]
    public void DeleteSurroundingInCodePoints_DeletesWholeSurrogatePair()
    {
        // R4: Samsung deletes an emoji as one code point.
        var sink = new FakeSink("a😀");
        var t = new ImeCompositionTracker(sink);

        t.DeleteSurroundingInCodePoints(1, 0, sink.Context);

        Assert.Equal(new[] { "before:2" }, sink.Ops);
        Assert.Equal("a", sink.Text);
    }

    [Fact]
    public void DeleteSurroundingInCodePoints_After_CountsCodePoints()
    {
        var sink = new FakeSink("😀b😀c", selStart: 0);
        var t = new ImeCompositionTracker(sink);

        t.DeleteSurroundingInCodePoints(0, 3, sink.Context);

        Assert.Equal(new[] { "after:5" }, sink.Ops);
        Assert.Equal("c", sink.Text);
    }

    [Fact]
    public void FinishComposing_ThenCompose_IsPureInsert()
    {
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);
        t.SetComposingText("ab");
        t.FinishComposing();
        sink.Ops.Clear();

        t.SetComposingText("x");

        Assert.Equal(new[] { "insert:x" }, sink.Ops);
        Assert.Equal("abx", sink.Text);
    }

    [Fact]
    public void Enter_FinishesCompositionAndSplits()
    {
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);
        t.SetComposingText("hi");
        sink.Ops.Clear();

        t.Enter();
        t.SetComposingText("x");

        Assert.Equal(new[] { "enter", "insert:x" }, sink.Ops);
    }

    [Fact]
    public void ComposingDiff_DoesNotSplitSurrogatePair()
    {
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);
        t.SetComposingText("a\U0001F600");   // a😀
        sink.Ops.Clear();

        t.SetComposingText("a\U0001F601");   // a😁 — shares the high surrogate

        Assert.Equal(new[] { "before:2", "insert:\U0001F601" }, sink.Ops);
    }

    [Fact]
    public void Reset_ForgetsComposition()
    {
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);
        t.SetComposingText("ab");
        t.Reset();
        sink.Ops.Clear();

        t.SetComposingText("ab");

        Assert.Equal(new[] { "insert:ab" }, sink.Ops);
    }
}
