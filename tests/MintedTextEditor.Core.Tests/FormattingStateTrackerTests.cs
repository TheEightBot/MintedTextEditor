using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Events;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Tests;

public class FormattingStateTrackerTests
{
    private static TextRange Caret(int block, int inline, int offset)
    {
        var p = new DocumentPosition(block, inline, offset);
        return new TextRange(p, p);
    }

    private static EditorDocument TwoRuns()
    {
        // "plain " (default) + "bold" (bold, Arial 20, red)
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("plain ");
        para.AppendRun("bold", new TextStyle("Arial", 20f, isBold: true, textColor: new EditorColor(255, 0, 0)));
        return doc;
    }

    [Fact]
    public void Compute_EmptyDocument_EqualsDefault()
    {
        var state = FormattingState.Compute(new EditorDocument(), Caret(0, 0, 0));

        Assert.Equal(FormattingState.Default, state);
        Assert.Equal("Normal", state.ParagraphFormat);
    }

    [Fact]
    public void Compute_Caret_UsesRunAtCaret()
    {
        var doc = TwoRuns();

        var state = FormattingState.Compute(doc, Caret(0, 1, 2));

        Assert.True(state.IsBold);
        Assert.Equal("Arial", state.FontFamily);
        Assert.Equal(20f, state.FontSize);
        Assert.Equal(new EditorColor(255, 0, 0), state.TextColor);
    }

    [Fact]
    public void Compute_Caret_PendingStyleWins()
    {
        var doc = TwoRuns();

        var state = FormattingState.Compute(doc, Caret(0, 0, 2), TextStyle.Default.WithItalic(true));

        Assert.True(state.IsItalic);
        Assert.False(state.IsBold);
    }

    [Fact]
    public void Compute_Selection_BoldOnlyWhenWholeRangeIsBold()
    {
        var doc = TwoRuns();

        var mixed = FormattingState.Compute(doc,
            new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 1, 4)));
        var boldOnly = FormattingState.Compute(doc,
            new TextRange(new DocumentPosition(0, 1, 0), new DocumentPosition(0, 1, 4)));

        Assert.False(mixed.IsBold);
        Assert.True(boldOnly.IsBold);
        Assert.Equal("Arial", boldOnly.FontFamily);
    }

    [Fact]
    public void Compute_ReadsParagraphStyle()
    {
        var doc = TwoRuns();
        var range = Caret(0, 0, 0);
        ParagraphFormattingEngine.SetHeadingLevel(doc, range, 2);
        ParagraphFormattingEngine.SetAlignment(doc, range, TextAlignment.Center);
        ParagraphFormattingEngine.ToggleBulletList(doc, range);

        var state = FormattingState.Compute(doc, range);

        Assert.Equal("Heading2", state.ParagraphFormat);
        Assert.Equal(TextAlignment.Center, state.Alignment);
        Assert.Equal(ListType.Bullet, state.ListType);
    }

    [Fact]
    public void Compute_ParagraphFormat_Quote()
    {
        var doc = TwoRuns();
        ParagraphFormattingEngine.SetParagraphFormat(doc, Caret(0, 0, 0), "Quote");

        Assert.Equal("Quote", FormattingState.Compute(doc, Caret(0, 0, 0)).ParagraphFormat);
    }

    [Fact]
    public void Compute_CaretInTableCell_ReadsCellParagraph()
    {
        var doc = new EditorDocument();
        var table = new TableBlock(1, 1);
        var cellPara = (Paragraph)table.GetCell(0, 0)!.Blocks[0];
        cellPara.AppendRun("x", TextStyle.Default.WithUnderline(true));
        cellPara.Style.Alignment = TextAlignment.Right;
        doc.AddBlock(table);
        var p = new DocumentPosition(1, 0, 0, 0, 0);

        var state = FormattingState.Compute(doc, new TextRange(p, p));

        Assert.True(state.IsUnderline);
        Assert.Equal(TextAlignment.Right, state.Alignment);
    }

    [Fact]
    public void Compute_OutOfRangePosition_DoesNotThrow()
    {
        var state = FormattingState.Compute(new EditorDocument(), Caret(5, 3, 1));

        Assert.Equal(FormattingState.Default, state);
    }

    [Fact]
    public void Update_SameState_ReportsNoChange()
    {
        var tracker = new FormattingStateTracker();

        Assert.Equal(FormattingStateChanges.None, tracker.Update(FormattingState.Default));
        Assert.Equal(FormattingStateChanges.None, tracker.Update(FormattingState.Default with { }));
    }

    [Fact]
    public void Update_MovingCaretIntoBoldRun_ReportsOnlyChangedGroups()
    {
        var doc = TwoRuns();
        var tracker = new FormattingStateTracker(FormattingState.Compute(doc, Caret(0, 0, 1)));

        var changes = tracker.Update(FormattingState.Compute(doc, Caret(0, 1, 1)));

        Assert.Equal(
            FormattingStateChanges.FontAttributes | FormattingStateChanges.FontFamily
            | FormattingStateChanges.FontSize | FormattingStateChanges.TextColor,
            changes);
        Assert.True(tracker.Current.IsBold);

        // Moving within the same run changes nothing.
        Assert.Equal(FormattingStateChanges.None, tracker.Update(FormattingState.Compute(doc, Caret(0, 1, 3))));
    }

    [Theory]
    [InlineData(nameof(FormattingState.IsUnderline), FormattingStateChanges.TextDecorations)]
    [InlineData(nameof(FormattingState.IsStrikethrough), FormattingStateChanges.TextDecorations)]
    [InlineData(nameof(FormattingState.IsItalic), FormattingStateChanges.FontAttributes)]
    [InlineData(nameof(FormattingState.IsSubscript), FormattingStateChanges.FontAttributes)]
    [InlineData(nameof(FormattingState.IsSuperscript), FormattingStateChanges.FontAttributes)]
    [InlineData(nameof(FormattingState.IsBlockQuote), FormattingStateChanges.ParagraphFormat)]
    public void Compare_EachBooleanMapsToItsGroup(string property, FormattingStateChanges expected)
    {
        var d = FormattingState.Default;
        var changed = property switch
        {
            nameof(FormattingState.IsUnderline) => d with { IsUnderline = true },
            nameof(FormattingState.IsStrikethrough) => d with { IsStrikethrough = true },
            nameof(FormattingState.IsItalic) => d with { IsItalic = true },
            nameof(FormattingState.IsSubscript) => d with { IsSubscript = true },
            nameof(FormattingState.IsSuperscript) => d with { IsSuperscript = true },
            _ => d with { IsBlockQuote = true },
        };

        Assert.Equal(expected, FormattingStateTracker.Compare(d, changed));
    }

    [Fact]
    public void Compare_ParagraphAndColourGroups()
    {
        var d = FormattingState.Default;

        Assert.Equal(FormattingStateChanges.ParagraphFormat, FormattingStateTracker.Compare(d, d with { HeadingLevel = 1 }));
        Assert.Equal(FormattingStateChanges.Alignment, FormattingStateTracker.Compare(d, d with { Alignment = TextAlignment.Justify }));
        Assert.Equal(FormattingStateChanges.ListType, FormattingStateTracker.Compare(d, d with { ListType = ListType.Number }));
        Assert.Equal(FormattingStateChanges.HighlightColor, FormattingStateTracker.Compare(d, d with { HighlightColor = new EditorColor(255, 255, 0) }));
    }

    [Fact]
    public void Update_AfterToggleBoldOnSelection_ReportsFontAttributes()
    {
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun("hello");
        var range = new TextRange(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 5));
        var tracker = new FormattingStateTracker(FormattingState.Compute(doc, range));

        new FormattingEngine().ToggleBold(doc, range);
        var changes = tracker.Update(FormattingState.Compute(doc, range));

        Assert.Equal(FormattingStateChanges.FontAttributes, changes);
        Assert.True(tracker.Current.IsBold);
    }
}
