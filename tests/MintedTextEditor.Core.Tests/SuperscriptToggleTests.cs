using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;

namespace MintedTextEditor.Core.Tests;

/// <summary>
/// Tests for the superscript toolbar bug (#5): repeated toggle cycles drift the
/// selection and can drop characters.
///
/// Symptom recap:
///   1. Select text → tap Superscript: works the FIRST time.
///   2. Tap Superscript again to DISABLE: nothing happens.
///   3. Deselect, reselect, apply: doesn't work AND deletes characters (data loss).
///   4. Sometimes the selection visibly moves to a different spot.
///
/// Root causes under test:
///   A. Selection coordinate drift — style ops split runs; NormalizePosition (old path)
///      could not recover after merges, so the selection drifted across cycles.
///   B. ApplyStyleToRunSegment — bad offsets (from drifted selection) caused it to
///      throw or silently drop characters.
///   C. Android IME staleness — tested via Core invariants; IME fix is Android-only.
/// </summary>
public class SuperscriptToggleTests
{
    private readonly FormattingEngine _fmt = new();

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static Paragraph Para(EditorDocument doc) => (Paragraph)doc.Blocks[0];

    /// <summary>
    /// Concatenates the plain text of all runs in block 0, irrespective of style.
    /// This is the data-loss invariant: the total text must never change after any
    /// sequence of formatting operations.
    /// </summary>
    private static string PlainText(EditorDocument doc)
        => string.Concat(Para(doc).Inlines.OfType<TextRun>().Select(r => r.Text));

    /// <summary>
    /// Mirrors the new robust toolbar flow that uses
    /// <see cref="DocumentEditor.ApplyCharacterFormatting"/> (capture absolute offsets,
    /// apply, normalize inlines, rebuild selection).  This is the fix under test.
    /// </summary>
    private void ToggleSuperscriptRobust(EditorDocument doc, Selection selection)
    {
        var (newAnchor, newActive) = DocumentEditor.ApplyCharacterFormatting(
            doc,
            selection.Anchor,
            selection.Active,
            range => _fmt.ToggleSuperscript(doc, range));
        selection.Set(newAnchor, newActive);
    }

    // ── 1. Data-loss invariant ─────────────────────────────────────────────────

    [Fact]
    public void DataLossInvariant_SuperscriptApplyDisableReselect_PlainTextUnchanged()
    {
        // "Hello beautiful World" — select "beautiful" and cycle: on / off / on again.
        // The concatenated plain text must be exactly "Hello beautiful World" after every
        // step regardless of run splits and merges.
        const string original = "Hello beautiful World";
        var doc = new EditorDocument();
        Para(doc).AppendRun(original);

        var selection = new Selection();
        selection.Set(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 15)); // "beautiful"

        ToggleSuperscriptRobust(doc, selection); // on
        Assert.Equal(original, PlainText(doc));

        ToggleSuperscriptRobust(doc, selection); // off
        Assert.Equal(original, PlainText(doc));

        ToggleSuperscriptRobust(doc, selection); // on again
        Assert.Equal(original, PlainText(doc));
    }

    [Fact]
    public void DataLossInvariant_BoldApplyDisableReselect_PlainTextUnchanged()
    {
        // Same invariant but for bold — guards the ApplyStyleToRunSegment clamping fix.
        const string original = "foo bar baz";
        var doc = new EditorDocument();
        Para(doc).AppendRun(original);

        var selection = new Selection();
        selection.Set(new DocumentPosition(0, 0, 4), new DocumentPosition(0, 0, 7)); // "bar"

        var (newAnchor, newActive) = DocumentEditor.ApplyCharacterFormatting(
            doc, selection.Anchor, selection.Active,
            range => new FormattingEngine().ToggleBold(doc, range));
        selection.Set(newAnchor, newActive);
        Assert.Equal(original, PlainText(doc));

        (newAnchor, newActive) = DocumentEditor.ApplyCharacterFormatting(
            doc, selection.Anchor, selection.Active,
            range => new FormattingEngine().ToggleBold(doc, range));
        selection.Set(newAnchor, newActive);
        Assert.Equal(original, PlainText(doc));
    }

    // ── 2. Toggle-off works after toggle-on ────────────────────────────────────

    [Fact]
    public void ToggleSuperscript_Off_AfterOn_ActuallyRemovesSuperscript()
    {
        // Symptom 2: tapping Superscript a second time did nothing.
        // After the fix, toggling off must actually remove the style from every run in range.
        var doc = new EditorDocument();
        Para(doc).AppendRun("Hello beautiful World");

        var selection = new Selection();
        selection.Set(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 15)); // "beautiful"

        ToggleSuperscriptRobust(doc, selection); // on

        // Sanity: the styled word must be superscript.
        var styledRuns = Para(doc).Inlines.OfType<TextRun>()
            .Where(r => r.Style.IsSuperscript).ToList();
        Assert.NotEmpty(styledRuns);
        Assert.Contains(styledRuns, r => r.Text == "beautiful");

        ToggleSuperscriptRobust(doc, selection); // off

        // Every run in the paragraph must now be NOT superscript.
        var allRuns = Para(doc).Inlines.OfType<TextRun>().ToList();
        Assert.DoesNotContain(allRuns, r => r.Style.IsSuperscript);
    }

    [Fact]
    public void ToggleSuperscript_OnOffOn_StyledRunContainsExpectedText()
    {
        // After apply / disable / re-apply, the superscript runs must contain exactly
        // "beautiful" and nothing from the surrounding text.
        var doc = new EditorDocument();
        Para(doc).AppendRun("Hello beautiful World");

        var selection = new Selection();
        selection.Set(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 15)); // "beautiful"

        ToggleSuperscriptRobust(doc, selection); // on
        ToggleSuperscriptRobust(doc, selection); // off
        ToggleSuperscriptRobust(doc, selection); // on

        var superscriptText = string.Concat(
            Para(doc).Inlines.OfType<TextRun>()
            .Where(r => r.Style.IsSuperscript)
            .Select(r => r.Text));

        Assert.Equal("beautiful", superscriptText);
    }

    // ── 3. Selection does not drift across toggle cycles ───────────────────────

    [Fact]
    public void Selection_CoversSameAbsoluteRange_AfterMultipleToggleCycles()
    {
        // Symptom 4: the selection visibly moves after a toggle.
        // After the fix, the selection's absolute start/end character offsets must
        // be identical before and after any toggle cycle.
        var doc = new EditorDocument();
        Para(doc).AppendRun("Hello beautiful World");

        var selection = new Selection();
        selection.Set(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 15)); // "beautiful"

        // Record expected absolute offsets (6..15 for "beautiful").
        const int expectedAnchorAbs = 6;
        const int expectedActiveAbs = 15;

        ToggleSuperscriptRobust(doc, selection); // on
        AssertAbsoluteOffsets(doc, selection, expectedAnchorAbs, expectedActiveAbs, "after 1st toggle (on)");

        ToggleSuperscriptRobust(doc, selection); // off
        AssertAbsoluteOffsets(doc, selection, expectedAnchorAbs, expectedActiveAbs, "after 2nd toggle (off)");

        ToggleSuperscriptRobust(doc, selection); // on again
        AssertAbsoluteOffsets(doc, selection, expectedAnchorAbs, expectedActiveAbs, "after 3rd toggle (on)");
    }

    [Fact]
    public void Selection_CoversSameWord_AfterCollapseAndReselect_ThenApply()
    {
        // Symptom 3: after deselect→reselect→apply, the formatting applied to wrong range.
        // Simulate: apply, collapse (deselect), rebuild the same selection, apply again.
        var doc = new EditorDocument();
        Para(doc).AppendRun("Hello beautiful World");

        var selection = new Selection();
        selection.Set(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 15)); // "beautiful"

        ToggleSuperscriptRobust(doc, selection); // on
        ToggleSuperscriptRobust(doc, selection); // off

        // Simulate collapsing the selection (deselect) and re-applying to the same word.
        // After the fix, AbsoluteCharOffsetToPosition correctly remaps to "beautiful".
        var anchor = DocumentEditor.AbsoluteCharOffsetToPosition(doc, new DocumentPosition(0, 0, 0), 6);
        var active = DocumentEditor.AbsoluteCharOffsetToPosition(doc, new DocumentPosition(0, 0, 0), 15);
        selection.Set(anchor, active);

        ToggleSuperscriptRobust(doc, selection); // on again

        // Only "beautiful" must be superscript.
        var superscriptText = string.Concat(
            Para(doc).Inlines.OfType<TextRun>()
            .Where(r => r.Style.IsSuperscript)
            .Select(r => r.Text));
        Assert.Equal("beautiful", superscriptText);

        // Plain text must be unchanged.
        Assert.Equal("Hello beautiful World", PlainText(doc));
    }

    // ── 4. No empty or duplicate-style runs after toggle cycles ───────────────

    [Fact]
    public void RunsAreClean_AfterRepeatedToggleCycles_NoEmptyOrDuplicateStyleRuns()
    {
        // After any number of toggles, NormalizeInlines must leave the paragraph with
        // no empty runs and no adjacent runs of the same style.
        var doc = new EditorDocument();
        Para(doc).AppendRun("Hello beautiful World");

        var selection = new Selection();
        selection.Set(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 15));

        for (int cycle = 0; cycle < 4; cycle++)
        {
            ToggleSuperscriptRobust(doc, selection);

            var inlines = Para(doc).Inlines.OfType<TextRun>().ToList();

            // No empty runs.
            Assert.DoesNotContain(inlines, r => r.Text.Length == 0);

            // No adjacent runs with the same style.
            for (int i = 0; i < inlines.Count - 1; i++)
            {
                Assert.False(
                    inlines[i].Style == inlines[i + 1].Style,
                    $"Cycle {cycle + 1}: adjacent runs at indices {i} and {i + 1} have the same style — NormalizeInlines should have merged them.");
            }
        }
    }

    // ── 5. ApplyStyleToRunSegment degenerate-offset guard ─────────────────────

    [Fact]
    public void ApplyTextStyle_StartOffsetBeyondRunLength_DoesNotThrowAndDoesNotDropText()
    {
        // Root cause B guard: if a stale/drifted selection gives startOffset > run.Text.Length,
        // the old code would throw ArgumentOutOfRangeException.  After the fix it must clamp
        // silently and leave the paragraph's plain text intact.
        var doc = new EditorDocument();
        Para(doc).AppendRun("short");

        // Deliberately pass an out-of-range selection (startOffset = 100, endOffset = 200).
        var bogusRange = new TextRange(
            new DocumentPosition(0, 0, 100),
            new DocumentPosition(0, 0, 200));

        var ex = Record.Exception(() =>
            DocumentEditor.ApplyTextStyle(doc, bogusRange, s => s.WithSuperscript(true)));

        Assert.Null(ex); // must not throw
        Assert.Equal("short", PlainText(doc)); // must not drop text
    }

    [Fact]
    public void ApplyTextStyle_InvertedOffsets_DoesNotThrowAndDoesNotDropText()
    {
        // Guard: startOffset > endOffset (can happen if selection was reversed/stale).
        var doc = new EditorDocument();
        Para(doc).AppendRun("Hello");

        var invertedRange = new TextRange(
            new DocumentPosition(0, 0, 4),
            new DocumentPosition(0, 0, 1));

        // TextRange normalises start≤end, so ApplyTextStyle sees a valid (small) range.
        // The segment within the run is [1..4] = "ell".
        var ex = Record.Exception(() =>
            DocumentEditor.ApplyTextStyle(doc, invertedRange, s => s.WithBold(true)));

        Assert.Null(ex);
        Assert.Equal("Hello", PlainText(doc));
    }

    [Fact]
    public void ApplyStyleToRunSegment_ZeroLengthSegment_LeavesRunUntouched()
    {
        // If clamped start == clamped end, the segment is zero-length — nothing to style.
        var doc = new EditorDocument();
        Para(doc).AppendRun("Hello");

        // Select an empty range at offset 3.
        var emptyRange = new TextRange(
            new DocumentPosition(0, 0, 3),
            new DocumentPosition(0, 0, 3));

        DocumentEditor.ApplyTextStyle(doc, emptyRange, s => s.WithBold(true));

        // Single run, unchanged.
        var run = (TextRun)Para(doc).Inlines[0];
        Assert.Equal("Hello", run.Text);
        Assert.False(run.Style.IsBold);
    }

    // ── 6. NormalizeInlines is called and merges correctly ─────────────────────

    [Fact]
    public void NormalizeInlines_MergesAdjacentSameStyleRuns()
    {
        // Directly verify the public NormalizeInlines helper merges runs.
        var doc = new EditorDocument();
        var para = Para(doc);
        para.AppendRun("foo");
        para.AppendRun("bar"); // same default style → should merge

        DocumentEditor.NormalizeInlines(para);

        Assert.Single(para.Inlines);
        Assert.Equal("foobar", ((TextRun)para.Inlines[0]).Text);
    }

    [Fact]
    public void NormalizeInlines_RemovesEmptyRuns()
    {
        var doc = new EditorDocument();
        var para = Para(doc);
        para.AppendRun("hello");
        para.AppendRun(""); // empty run
        para.AppendRun("world");

        DocumentEditor.NormalizeInlines(para);

        // The empty run is removed; "hello" and "world" also merge (same style).
        Assert.Single(para.Inlines);
        Assert.Equal("helloworld", ((TextRun)para.Inlines[0]).Text);
    }

    // ── 7. AbsoluteCharOffsetToPosition round-trips ────────────────────────────

    [Fact]
    public void AbsoluteCharOffset_RoundTrips_AfterRunSplits()
    {
        // After ApplyTextStyle splits a run, AbsoluteCharOffsetToPosition must remap
        // the captured absolute offset back to the correct (inlineIndex, offset).
        var doc = new EditorDocument();
        Para(doc).AppendRun("Hello beautiful World"); // single run, abs [0..21)

        // Apply superscript to "beautiful" (6..15) → splits into 3 runs.
        var range = new TextRange(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 15));
        DocumentEditor.ApplyTextStyle(doc, range, s => s.WithSuperscript(true));
        DocumentEditor.NormalizeInlines(Para(doc));

        // Now remap abs 6 (start of "beautiful") and abs 15 (end of "beautiful").
        var anchor = DocumentEditor.AbsoluteCharOffsetToPosition(doc, new DocumentPosition(0, 0, 0), 6);
        var active = DocumentEditor.AbsoluteCharOffsetToPosition(doc, new DocumentPosition(0, 0, 0), 15);

        // The remapped selection must cover "beautiful" exactly.
        var selectedText = DocumentEditor.GetSelectedText(doc, new TextRange(anchor, active));
        Assert.Equal("beautiful", selectedText);
    }

    [Fact]
    public void AbsoluteCharOffset_RoundTrips_AfterRunMerges()
    {
        // After NormalizeInlines merges same-style runs back together, the remapped
        // absolute offset must still point to the same logical character.
        //
        // Use ApplyCharacterFormatting (the robust path) for both on and off so the
        // selection is correctly rebuilt between the two operations.
        var doc = new EditorDocument();
        Para(doc).AppendRun("Hello beautiful World");

        var selection = new Selection();
        selection.Set(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 15)); // "beautiful"

        // Apply superscript → splits into 3 runs; selection is rebuilt by the helper.
        ToggleSuperscriptRobust(doc, selection);

        // Remove superscript → all runs return to same style; NormalizeInlines merges them.
        ToggleSuperscriptRobust(doc, selection);

        // After the merge the paragraph collapses back to a single run.
        // The selection Anchor/Active must still point to offset 6 / 15 within run 0.
        var anchor = selection.Anchor;
        var active = selection.Active;

        Assert.Equal(0, anchor.InlineIndex);
        Assert.Equal(6, anchor.Offset);
        Assert.Equal(0, active.InlineIndex);
        Assert.Equal(15, active.Offset);
    }

    // ── Private assertion helper ───────────────────────────────────────────────

    private static void AssertAbsoluteOffsets(
        EditorDocument doc, Selection selection,
        int expectedAnchorAbs, int expectedActiveAbs,
        string context)
    {
        int actualAnchorAbs = DocumentEditor.GetAbsoluteCharOffset(doc, selection.Anchor);
        int actualActiveAbs = DocumentEditor.GetAbsoluteCharOffset(doc, selection.Active);

        Assert.True(actualAnchorAbs == expectedAnchorAbs,
            $"{context}: anchor abs offset expected {expectedAnchorAbs}, got {actualAnchorAbs}");
        Assert.True(actualActiveAbs == expectedActiveAbs,
            $"{context}: active abs offset expected {expectedActiveAbs}, got {actualActiveAbs}");
    }
}
