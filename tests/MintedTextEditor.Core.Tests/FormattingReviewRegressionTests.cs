using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;

namespace MintedTextEditor.Core.Tests;

/// <summary>Regressions from the PR #7 review (run splitting, pending-style toggles, undo after normalization).</summary>
public class FormattingReviewRegressionTests
{
    private static readonly TextStyle Bold = TextStyle.Default.WithBold(true);

    private static string Describe(Paragraph p)
        => string.Join("|", p.Inlines.OfType<TextRun>().Select(r => (r.Style.IsBold ? "B:" : "") + r.Text));

    [Fact]
    public void ApplyTextStyle_PartialFirstAndLastRuns_FormatsWholeSelection()
    {
        var p = new Paragraph();
        p.AppendRun("AAA");
        p.AppendRun("BBB", TextStyle.Default.WithFontSize(20));   // distinct style keeps runs separate
        p.AppendRun("CCC");
        var doc = new EditorDocument(new Block[] { p });

        DocumentEditor.ApplyTextStyle(doc,
            new TextRange(new DocumentPosition(0, 0, 1), new DocumentPosition(0, 1, 2)),
            s => s.WithBold(true));

        string boldText = string.Concat(p.Inlines.OfType<TextRun>().Where(r => r.Style.IsBold).Select(r => r.Text));
        Assert.Equal("AABB", boldText);
        Assert.Equal("AAABBBCCC", p.GetText());
    }

    [Fact]
    public void ApplyCharacterFormatting_CollapsedRange_DoesNotRestructureRuns()
    {
        var p = new Paragraph();
        p.AppendRun("ab");
        p.AppendRun("cd");   // same style: normalization would merge these
        var doc = new EditorDocument(new Block[] { p });
        var caret = new DocumentPosition(0, 1, 1);

        var (anchor, active) = DocumentEditor.ApplyCharacterFormatting(doc, caret, caret, _ => { });

        Assert.Equal(2, p.Inlines.Count);
        Assert.Equal(caret, anchor);
        Assert.Equal(caret, active);
    }

    [Fact]
    public void Undo_AfterFormattingMergesRuns_RemovesInsertedText()
    {
        // Plain "ab", type a bold "c", select it and toggle bold off: runs merge to "abc".
        var doc = new EditorDocument(new Block[] { new Paragraph("ab") });
        var undo = new UndoManager();
        var end = undo.Push(new InsertTextAction(doc, new DocumentPosition(0, 0, 2), "c", Bold));
        var p = (Paragraph)doc.Blocks[0];
        Assert.Equal(2, p.Inlines.Count);

        DocumentEditor.ApplyCharacterFormatting(doc, new DocumentPosition(0, 1, 0), end,
            range => DocumentEditor.ApplyTextStyle(doc, range, s => s.WithBold(false)));
        Assert.Single(p.Inlines);

        undo.Undo();
        Assert.Equal("ab", doc.GetText());

        undo.Redo();
        Assert.Equal("abc", doc.GetText());
    }

    [Fact]
    public void Undo_DeleteRange_AfterRunsMerged_RestoresAndRedoes()
    {
        var p = new Paragraph();
        p.AppendRun("ab");
        p.AppendRun("cd", Bold);
        var doc = new EditorDocument(new Block[] { p });
        var undo = new UndoManager();

        // Delete "d" (in run 1), then unbold "c" so runs merge, then undo/redo the delete.
        undo.Push(new DeleteRangeAction(doc, new TextRange(new DocumentPosition(0, 1, 1), new DocumentPosition(0, 1, 2))));
        DocumentEditor.ApplyCharacterFormatting(doc, new DocumentPosition(0, 1, 0), new DocumentPosition(0, 1, 1),
            range => DocumentEditor.ApplyTextStyle(doc, range, s => s.WithBold(false)));
        Assert.Single(p.Inlines);

        undo.Undo();
        undo.Redo();
        Assert.Equal("abc", doc.GetText());
    }

    [Fact]
    public void CollapsedBoldToggle_ThenTyping_InsertsBoldText()
    {
        var doc = new EditorDocument(new Block[] { new Paragraph("ab") });
        var caret = new Caret();
        var ctrl = new MintedTextEditor.Core.Input.EditorInputController(caret, new CaretRenderer())
        {
            FormattingEngine = new MintedTextEditor.Core.Formatting.FormattingEngine(),
        };
        caret.MoveTo(new DocumentPosition(0, 0, 2));
        ctrl.Selection.CollapseTo(caret.Position);
        var layout = new MintedTextEditor.Core.Layout.TextLayoutEngine().Layout(doc, 800f, new MockDrawingContext());

        ctrl.HandleKeyDown(new MintedTextEditor.Core.Input.EditorKeyEventArgs(
            MintedTextEditor.Core.Input.EditorKey.B, modifiers: MintedTextEditor.Core.Input.InputModifiers.Control),
            doc, layout, new MockDrawingContext());
        ctrl.HandleTextInput("r", doc);

        var p = (Paragraph)doc.Blocks[0];
        Assert.Equal("B:r", Describe(p).Split('|').Last());
    }
}
