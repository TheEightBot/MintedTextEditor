using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;

namespace MintedTextEditor.Core.Tests;

/// <summary>
/// Regression tests for a character-format bug where applying a format to a selection
/// that spans multiple inlines within a single paragraph leaked the format onto every
/// run from the selection start to the END of the paragraph (issue: re-applying a format
/// after unsetting it formatted the whole row).
/// <para>
/// Root cause: <see cref="DocumentEditor.ApplyTextStyle"/> classified a "middle" run as
/// in-range using only an after-start check (<c>ii &gt; start.InlineIndex</c>) without also
/// requiring it to be before the end boundary, so runs past <c>end.InlineIndex</c> were
/// styled too.
/// </para>
/// </summary>
public class FormatSelectionLeakTests
{
    private readonly FormattingEngine _fmt = new();

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static Paragraph Para(EditorDocument doc) => (Paragraph)doc.Blocks[0];

    private static TextRun Run(EditorDocument doc, int inlineIndex)
        => (TextRun)Para(doc).Inlines[inlineIndex];

    /// <summary>
    /// Mirrors the real toolbar flow (<c>MintedEditorView.ExecuteToolbarCommand</c>):
    /// apply the toggle over the current selection, then re-normalize both selection
    /// endpoints because the style op may have split runs and shifted inline indices.
    /// Returns the re-normalized selection range to feed into the next toggle.
    /// </summary>
    private TextRange ToggleBoldAndRenormalize(EditorDocument doc, Selection selection)
    {
        _fmt.ToggleBold(doc, selection.Range);

        var anchor = DocumentEditor.NormalizePosition(doc, selection.Anchor);
        var active = DocumentEditor.NormalizePosition(doc, selection.Active);
        selection.Set(anchor, active);
        return selection.Range;
    }

    // ── 1. Direct ApplyTextStyle over a sub-range of a multi-run paragraph ───────

    [Fact]
    public void ApplyTextStyle_RangeCoveringFirstTwoOfThreeRuns_LeavesThirdRunUnchanged()
    {
        // Paragraph: ["AAA"(0), "BBB"(1), "CCC"(2)] — three separate runs.
        var doc = new EditorDocument();
        var para = Para(doc);
        para.AppendRun("AAA");
        para.AppendRun("BBB");
        para.AppendRun("CCC");

        // Select from the start of run 0 through the end of run 1 ("AAABBB").
        var range = new TextRange(
            new DocumentPosition(0, 0, 0),
            new DocumentPosition(0, 1, 3));

        DocumentEditor.ApplyTextStyle(doc, range, s => s.WithBold(true));

        // Runs 0 and 1 are inside the range and must be bold.
        Assert.True(Run(doc, 0).Style.IsBold);
        Assert.True(Run(doc, 1).Style.IsBold);

        // Run 2 lies entirely past the selection end and must be untouched.
        Assert.False(Run(doc, 2).Style.IsBold);
        Assert.Equal("CCC", Run(doc, 2).Text);
    }

    // ── 2. End-to-end repro: toggle on / off / on via the toolbar flow ───────────

    [Fact]
    public void ToggleBold_FirstApply_BoldsOnlyTheSelectedWord()
    {
        // Baseline of the user report: applying a format to a highlighted word works the
        // FIRST time. (Single run → start and end land in the same inline.)
        var doc = new EditorDocument();
        Para(doc).AppendRun("Hello beautiful World");

        var selection = new Selection();
        selection.Set(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 15)); // "beautiful"

        ToggleBoldAndRenormalize(doc, selection);

        Assert.Equal("beautiful", ConcatBold(doc));
    }

    [Fact]
    public void ToggleBold_OnOffOn_DoesNotLeakToTrailingTextOnReapply()
    {
        // The reported bug: after the first apply splits the run, re-applying the format to
        // the same highlighted word leaked it to the REST OF THE ROW (here, the trailing
        // " World"). Drive selection exactly like the toolbar does — re-normalizing both
        // endpoints after each toggle because style ops split runs and shift inline indices.
        var doc = new EditorDocument();
        Para(doc).AppendRun("Hello beautiful World");

        var selection = new Selection();
        selection.Set(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 15)); // "beautiful"

        ToggleBoldAndRenormalize(doc, selection); // on
        ToggleBoldAndRenormalize(doc, selection); // off
        ToggleBoldAndRenormalize(doc, selection); // on again — must not leak past the word

        // The precise defect: any text AFTER the selected word must stay un-bold.
        AssertNoTrailingTextIsBold(doc, afterWord: "beautiful");
    }

    [Fact]
    public void ToggleSuperscript_OnOffOn_DoesNotLeakToTrailingTextOnReapply()
    {
        var doc = new EditorDocument();
        Para(doc).AppendRun("Hello beautiful World");

        var selection = new Selection();
        selection.Set(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 15)); // "beautiful"

        void ToggleAndRenormalize()
        {
            _fmt.ToggleSuperscript(doc, selection.Range);
            var anchor = DocumentEditor.NormalizePosition(doc, selection.Anchor);
            var active = DocumentEditor.NormalizePosition(doc, selection.Active);
            selection.Set(anchor, active);
        }

        ToggleAndRenormalize(); // on
        ToggleAndRenormalize(); // off
        ToggleAndRenormalize(); // on again — must not leak

        // The trailing " World" must not have become superscript.
        var afterWord = Para(doc).Inlines.OfType<TextRun>().SkipWhile(r => r.Text != "beautiful").Skip(1);
        Assert.DoesNotContain(afterWord, r => r.Style.IsSuperscript);
    }

    /// <summary>Concatenates the text of every bold run in block 0.</summary>
    private static string ConcatBold(EditorDocument doc)
        => string.Concat(Para(doc).Inlines.OfType<TextRun>().Where(r => r.Style.IsBold).Select(r => r.Text));

    /// <summary>Asserts no run positioned after <paramref name="afterWord"/> is bold.</summary>
    private static void AssertNoTrailingTextIsBold(EditorDocument doc, string afterWord)
    {
        var trailing = Para(doc).Inlines.OfType<TextRun>()
            .SkipWhile(r => r.Text != afterWord)
            .Skip(1);
        Assert.DoesNotContain(trailing, r => r.Style.IsBold);
    }

    // ── 3. Selection ending mid-paragraph with multiple trailing runs ────────────

    [Fact]
    public void ApplyTextStyle_SelectionEndsMidParagraph_AllTrailingRunsUntouched()
    {
        // Five runs; select runs 1 and 2, leaving 3 trailing runs (3 and 4) plus the
        // leading run (0) untouched.
        var doc = new EditorDocument();
        var para = Para(doc);
        para.AppendRun("one");    // 0
        para.AppendRun("two");    // 1
        para.AppendRun("three");  // 2
        para.AppendRun("four");   // 3
        para.AppendRun("five");   // 4

        var range = new TextRange(
            new DocumentPosition(0, 1, 0),
            new DocumentPosition(0, 2, 5)); // through end of "three"

        DocumentEditor.ApplyTextStyle(doc, range, s => s.WithBold(true));

        Assert.False(Run(doc, 0).Style.IsBold); // leading run untouched
        Assert.True(Run(doc, 1).Style.IsBold);
        Assert.True(Run(doc, 2).Style.IsBold);
        Assert.False(Run(doc, 3).Style.IsBold); // trailing runs untouched
        Assert.False(Run(doc, 4).Style.IsBold);
    }

    // ── Multi-block path stays correct (intermediate + partial first/last) ───────

    [Fact]
    public void ApplyTextStyle_MultiBlockRange_StylesIntermediateAndPartialEnds()
    {
        // Block 0: ["AA"(0), "BB"(1)]   — selection starts at offset 1 of inline 1.
        // Block 1: ["CC"(0)]            — whole intermediate block.
        // Block 2: ["DD"(0), "EE"(1)]   — selection ends at offset 1 of inline 0.
        var doc = new EditorDocument();
        var p0 = (Paragraph)doc.Blocks[0];
        p0.AppendRun("AA");
        p0.AppendRun("BB");

        var p1 = new Paragraph { Parent = doc };
        p1.AppendRun("CC");
        doc.Blocks.Add(p1);

        var p2 = new Paragraph { Parent = doc };
        p2.AppendRun("DD");
        p2.AppendRun("EE");
        doc.Blocks.Add(p2);

        var range = new TextRange(
            new DocumentPosition(0, 1, 1),  // middle of "BB"
            new DocumentPosition(2, 0, 1)); // middle of "DD"

        DocumentEditor.ApplyTextStyle(doc, range, s => s.WithBold(true));

        // Block 0: "AA" untouched; "BB" split into "B"(unbold) + "B"(bold).
        Assert.False(((TextRun)p0.Inlines[0]).Style.IsBold);
        Assert.Equal("AA", ((TextRun)p0.Inlines[0]).Text);
        Assert.Equal("B", ((TextRun)p0.Inlines[1]).Text);
        Assert.False(((TextRun)p0.Inlines[1]).Style.IsBold);
        Assert.Equal("B", ((TextRun)p0.Inlines[2]).Text);
        Assert.True(((TextRun)p0.Inlines[2]).Style.IsBold);

        // Block 1: whole intermediate block "CC" is bold.
        Assert.True(((TextRun)p1.Inlines[0]).Style.IsBold);

        // Block 2: "DD" split into "D"(bold) + "D"(unbold); "EE" untouched.
        Assert.Equal("D", ((TextRun)p2.Inlines[0]).Text);
        Assert.True(((TextRun)p2.Inlines[0]).Style.IsBold);
        Assert.Equal("D", ((TextRun)p2.Inlines[1]).Text);
        Assert.False(((TextRun)p2.Inlines[1]).Style.IsBold);
        Assert.Equal("EE", ((TextRun)p2.Inlines[2]).Text);
        Assert.False(((TextRun)p2.Inlines[2]).Style.IsBold);
    }
}
