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

    private static void Compose(ImeCompositionTracker t, FakeSink sink, string text, int cursor = 1)
        => t.SetComposingText(text, cursor, sink.Context);

    private static void Commit(ImeCompositionTracker t, FakeSink sink, string text, int cursor = 1)
        => t.CommitText(text, cursor, sink.Context);

    [Fact]
    public void SamsungStyleComposing_InsertsOnlyNewCharacters()
    {
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);

        Compose(t, sink, "h");
        Compose(t, sink, "he");
        Compose(t, sink, "hel");
        Commit(t, sink, "hel ");

        Assert.Equal(new[] { "insert:h", "insert:e", "insert:l", "insert: " }, sink.Ops);
        Assert.Equal("hel ", sink.Text);
        Assert.False(t.HasComposition);
    }

    [Fact]
    public void Autocorrect_ReplacesOnlyTheChangedSuffix()
    {
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);

        Compose(t, sink, "teh");
        sink.Ops.Clear();
        Commit(t, sink, "the ");

        Assert.Equal(new[] { "before:2", "insert:he " }, sink.Ops);
        Assert.Equal("the ", sink.Text);
    }

    [Fact]
    public void ShrinkingComposition_DeletesDifference()
    {
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);

        Compose(t, sink, "help");
        sink.Ops.Clear();
        Compose(t, sink, "he");

        Assert.Equal(new[] { "before:2" }, sink.Ops);
        Assert.Equal("he", sink.Text);
        Assert.Equal((0, 2), (t.ComposingStart, t.ComposingEnd));
    }

    [Fact]
    public void SetComposingRegion_AdoptsExistingWord_NoDuplication()
    {
        // R2: caret after an existing word; Samsung re-composes it before editing.
        var sink = new FakeSink("say hello");
        var t = new ImeCompositionTracker(sink);

        t.SetComposingRegion(4, 9, sink.Context);
        Compose(t, sink, "hello");
        Assert.Empty(sink.Ops);

        Compose(t, sink, "help");
        Assert.Equal(new[] { "before:2", "insert:p" }, sink.Ops);
        Assert.Equal("say help", sink.Text);
    }

    [Fact]
    public void SetComposingRegion_LeavesSelectionUnchanged()
    {
        var sink = new FakeSink("hello", selStart: 2);
        var t = new ImeCompositionTracker(sink);

        t.SetComposingRegion(0, 5, sink.Context);

        Assert.Empty(sink.Ops);
        Assert.Equal(2, sink.SelStart);
        Assert.Equal((0, 5), (t.ComposingStart, t.ComposingEnd));
    }

    [Fact]
    public void ComposeAfterAdoptingRegion_WithCaretInsideWord_ReplacesRegion()
    {
        // Mid-word tap: caret at 2 inside the adopted word; the IME re-composes the word.
        var sink = new FakeSink("say hello", selStart: 6);
        var t = new ImeCompositionTracker(sink);
        t.SetComposingRegion(4, 9, sink.Context);

        Compose(t, sink, "heXllo");

        Assert.Equal("say heXllo", sink.Text);
        Assert.Equal(10, sink.SelStart);
        Assert.Equal((4, 10), (t.ComposingStart, t.ComposingEnd));
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
    public void SetSelection_KeepsComposition_Unchanged()
    {
        // Comment: setComposingText("ab"), setSelection(2,2), setComposingText("abc") → "abc".
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);
        Compose(t, sink, "ab");
        t.SetSelection(2, 2);
        Compose(t, sink, "abc");

        Assert.Equal("abc", sink.Text);
    }

    [Fact]
    public void SetSelection_InsideComposition_NextComposeReplacesWholeRegion()
    {
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);
        Compose(t, sink, "abcd");
        t.SetSelection(1, 1);
        Compose(t, sink, "abXd");

        Assert.Equal("abXd", sink.Text);
        Assert.Equal(4, sink.SelStart);
    }

    [Fact]
    public void ComposingTextWithNewline_EndsComposition()
    {
        // Comment: "a\n" then "ab\n" must not leave a multi-paragraph mirror behind.
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);

        Compose(t, sink, "a\n");

        Assert.False(t.HasComposition);
        Assert.Equal(-1, t.ComposingStart);
    }

    [Fact]
    public void CommitNewline_IsPassedThroughAsText()
    {
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);

        Commit(t, sink, "\n");

        Assert.Equal(new[] { "insert:\n" }, sink.Ops);
    }

    [Fact]
    public void EmptyCommit_DeletesComposingText()
    {
        var sink = new FakeSink("x");
        var t = new ImeCompositionTracker(sink);
        Compose(t, sink, "ab");

        Commit(t, sink, "");

        Assert.Equal("x", sink.Text);
    }

    [Fact]
    public void CommitText_NewCursorPositionZero_PlacesCursorBeforeText()
    {
        var sink = new FakeSink("ab");
        var t = new ImeCompositionTracker(sink);

        Commit(t, sink, "xyz", cursor: 0);

        Assert.Equal("abxyz", sink.Text);
        Assert.Equal(2, sink.SelStart);
    }

    [Fact]
    public void DeleteSurrounding_WithSelection_KeepsSelection()
    {
        // R6: Android semantics delete around the selection, never the selection itself.
        var sink = new FakeSink("abcdef", selStart: 2, selEnd: 4);
        var t = new ImeCompositionTracker(sink);

        bool crosses = t.DeleteSurrounding(1, 1, sink.Context);

        Assert.False(crosses);
        Assert.Equal(new[] { "before:1", "after:1" }, sink.Ops);
        Assert.Equal("acdf", sink.Text);
    }

    [Fact]
    public void DeleteSurrounding_InsideComposition_TrimsComposingRange()
    {
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);
        Compose(t, sink, "word");

        t.DeleteSurrounding(1, 0, sink.Context);
        sink.Ops.Clear();
        Compose(t, sink, "wors");

        Assert.Equal(new[] { "insert:s" }, sink.Ops);
        Assert.Equal("wors", sink.Text);
    }

    [Fact]
    public void DeleteSurrounding_BeforeComposition_ShiftsRange()
    {
        var sink = new FakeSink("ab cd", selStart: 3);
        var t = new ImeCompositionTracker(sink);
        t.SetComposingRegion(3, 5, sink.Context);

        t.DeleteSurrounding(2, 0, sink.Context);   // deletes "b "

        Assert.Equal("acd", sink.Text);
        Assert.Equal((1, 3), (t.ComposingStart, t.ComposingEnd));
    }

    [Fact]
    public void DeleteSurrounding_PastParagraphStart_ReportsCrossing()
    {
        var sink = new FakeSink("ab", selStart: 0);
        var t = new ImeCompositionTracker(sink);

        Assert.True(t.DeleteSurrounding(1, 0, sink.Context));
        Assert.Equal(new[] { "before:1" }, sink.Ops);   // the editor joins the paragraphs
    }

    [Fact]
    public void DeleteSurrounding_PastParagraphEnd_ReportsCrossing()
    {
        var sink = new FakeSink("ab");
        var t = new ImeCompositionTracker(sink);

        Assert.True(t.DeleteSurrounding(0, 1, sink.Context));
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
    public void DeleteSurroundingInCodePoints_PastParagraphStart_CountsBreak()
    {
        var sink = new FakeSink("a", selStart: 0);
        var t = new ImeCompositionTracker(sink);

        Assert.True(t.DeleteSurroundingInCodePoints(1, 0, sink.Context));
        Assert.Equal(new[] { "before:1" }, sink.Ops);
    }

    [Fact]
    public void FinishComposing_ThenCompose_IsPureInsert()
    {
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);
        Compose(t, sink, "ab");
        t.FinishComposing();
        sink.Ops.Clear();

        Compose(t, sink, "x");

        Assert.Equal(new[] { "insert:x" }, sink.Ops);
        Assert.Equal("abx", sink.Text);
    }

    [Fact]
    public void Enter_FinishesCompositionAndSplits()
    {
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);
        Compose(t, sink, "hi");
        sink.Ops.Clear();

        t.Enter();
        Compose(t, sink, "x");

        Assert.Equal(new[] { "enter", "insert:x" }, sink.Ops);
    }

    [Fact]
    public void ComposingDiff_DoesNotSplitSurrogatePair()
    {
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);
        Compose(t, sink, "a\U0001F600");   // a😀
        sink.Ops.Clear();

        Compose(t, sink, "a\U0001F601");   // a😁 — shares the high surrogate

        Assert.Equal(new[] { "before:2", "insert:\U0001F601" }, sink.Ops);
    }

    [Fact]
    public void Reset_ForgetsComposition()
    {
        var sink = new FakeSink();
        var t = new ImeCompositionTracker(sink);
        Compose(t, sink, "ab");
        t.Reset();
        sink.Ops.Clear();

        Compose(t, sink, "ab");

        Assert.Equal(new[] { "insert:ab" }, sink.Ops);
    }

    [Fact]
    public void Replace_SuggestionPick_ReplacesWordAndEndsComposition()
    {
        var sink = new FakeSink("say tet");
        var t = new ImeCompositionTracker(sink);

        t.Replace(4, 7, "Tet", 1, sink.Context);

        Assert.Equal(new[] { "select:4,7", "insert:Tet" }, sink.Ops);
        Assert.Equal("say Tet", sink.Text);
        Assert.Equal(7, sink.SelStart);
        Assert.False(t.HasComposition);
    }

    [Fact]
    public void Replace_AlwaysEstablishesRange_EvenWhenContextMatches()
    {
        // The editor's real selection may extend past this paragraph (context is clamped).
        var sink = new FakeSink("def", selStart: 0, selEnd: 2);
        var t = new ImeCompositionTracker(sink);

        t.Replace(0, 2, "X", 1, sink.Context);

        Assert.Equal(new[] { "select:0,2", "insert:X" }, sink.Ops);
        Assert.Equal("Xf", sink.Text);
    }

    [Fact]
    public void Replace_NewCursorPositionZero_PlacesCursorAtStart()
    {
        var sink = new FakeSink("abcdef");
        var t = new ImeCompositionTracker(sink);

        t.Replace(2, 4, "x", 0, sink.Context);

        Assert.Equal("abxef", sink.Text);
        Assert.Equal(2, sink.SelStart);
    }

    [Fact]
    public void Replace_WithEmptyText_DeletesRange()
    {
        var sink = new FakeSink("abcdef", selStart: 1);
        var t = new ImeCompositionTracker(sink);

        t.Replace(2, 4, "", 1, sink.Context);

        Assert.Equal("abef", sink.Text);
        Assert.Equal(2, sink.SelStart);
    }
}
