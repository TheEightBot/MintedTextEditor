using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Layout;

namespace MintedTextEditor.Core.Tests;

public class CaretTests
{
    // ── Default state ─────────────────────────────────────────────────

    [Fact]
    public void Caret_DefaultState_HasExpectedValues()
    {
        var caret = new Caret();

        Assert.Equal(new DocumentPosition(0, 0, 0), caret.Position);
        Assert.Equal(-1f, caret.PreferredX);
        Assert.True(caret.IsVisible);
        Assert.True(caret.BlinkEnabled);
    }

    // ── MoveTo ────────────────────────────────────────────────────────

    [Fact]
    public void MoveTo_SetsPosition()
    {
        var caret = new Caret();

        caret.MoveTo(new DocumentPosition(1, 2, 3));

        Assert.Equal(new DocumentPosition(1, 2, 3), caret.Position);
    }

    [Fact]
    public void MoveTo_ResetsPreferredX()
    {
        var caret = new Caret { PreferredX = 42f };

        caret.MoveTo(new DocumentPosition(0, 0, 1));

        Assert.Equal(-1f, caret.PreferredX);
    }

    [Fact]
    public void MoveTo_MakesCaretVisible()
    {
        var caret = new Caret { IsVisible = false };

        caret.MoveTo(new DocumentPosition(0, 0, 1));

        Assert.True(caret.IsVisible);
    }

    // ── MoveToPreservingX ─────────────────────────────────────────────

    [Fact]
    public void MoveToPreservingX_SetsPosition()
    {
        var caret = new Caret();

        caret.MoveToPreservingX(new DocumentPosition(0, 0, 2));

        Assert.Equal(new DocumentPosition(0, 0, 2), caret.Position);
    }

    [Fact]
    public void MoveToPreservingX_KeepsPreferredX()
    {
        var caret = new Caret { PreferredX = 64f };

        caret.MoveToPreservingX(new DocumentPosition(0, 0, 2));

        Assert.Equal(64f, caret.PreferredX);
    }

    [Fact]
    public void MoveToPreservingX_MakesCaretVisible()
    {
        var caret = new Caret { IsVisible = false };

        caret.MoveToPreservingX(new DocumentPosition(0, 0, 1));

        Assert.True(caret.IsVisible);
    }

    // ── ResetBlink ────────────────────────────────────────────────────

    [Fact]
    public void ResetBlink_MakesCaretVisible()
    {
        var caret = new Caret { IsVisible = false };

        caret.ResetBlink();

        Assert.True(caret.IsVisible);
    }

    // ── BlinkEnabled ──────────────────────────────────────────────────

    [Fact]
    public void BlinkEnabled_SetFalse_ForcesIsVisibleTrue()
    {
        var caret = new Caret { IsVisible = false };

        caret.BlinkEnabled = false;

        Assert.True(caret.IsVisible);
    }

    [Fact]
    public void BlinkEnabled_SetTrue_DoesNotChangeVisibility()
    {
        var caret = new Caret();
        caret.BlinkEnabled = false;
        caret.IsVisible = false; // manually set after disabling

        caret.BlinkEnabled = true; // re-enabling doesn't force visible

        // IsVisible not forced by re-enabling
        Assert.False(caret.IsVisible);
    }

    // ── UpdateBlink ───────────────────────────────────────────────────

    [Fact]
    public void UpdateBlink_WithBlinkDisabled_DoesNotToggle()
    {
        var caret = new Caret();
        caret.BlinkEnabled = false;
        caret.IsVisible = true;

        caret.UpdateBlink();

        Assert.True(caret.IsVisible); // unchanged
    }

    [Fact]
    public void UpdateBlink_CalledImmediately_DoesNotToggle()
    {
        var caret = new Caret();
        caret.BlinkEnabled = true;
        caret.ResetBlink(); // resets timer to now

        caret.UpdateBlink(); // not enough time elapsed

        Assert.True(caret.IsVisible); // still visible
    }

    // ── CaretRenderer ─────────────────────────────────────────────────

    [Fact]
    public void CaretRenderer_GetCaretX_MatchesCharacterOffset()
    {
        var context = new MockDrawingContext(); // CharWidth = 8f per char
        var engine = new TextLayoutEngine();
        var para = new Paragraph();
        para.AppendRun("Hello");
        var doc = new EditorDocument(new Block[] { para });
        var layout = engine.Layout(doc, 800f, context);
        var renderer = new CaretRenderer();

        // offset 2 = 2 chars × 8px = 16px
        float x = renderer.GetCaretX(new DocumentPosition(0, 0, 2), layout, doc, context);

        Assert.Equal(16f, x);
    }

    [Fact]
    public void CaretRenderer_GetCaretX_AtStartIsZero()
    {
        var context = new MockDrawingContext();
        var engine = new TextLayoutEngine();
        var para = new Paragraph();
        para.AppendRun("Hello");
        var doc = new EditorDocument(new Block[] { para });
        var layout = engine.Layout(doc, 800f, context);
        var renderer = new CaretRenderer();

        float x = renderer.GetCaretX(new DocumentPosition(0, 0, 0), layout, doc, context);

        Assert.Equal(0f, x);
    }

    [Fact]
    public void CaretRenderer_GetCaretRect_WidthEqualsCaretWidth()
    {
        var context = new MockDrawingContext();
        var engine = new TextLayoutEngine();
        var para = new Paragraph();
        para.AppendRun("Hello");
        var doc = new EditorDocument(new Block[] { para });
        var layout = engine.Layout(doc, 800f, context);
        var renderer = new CaretRenderer { CaretWidth = 3f };

        var rect = renderer.GetCaretRect(new DocumentPosition(0, 0, 0), layout, doc, context);

        Assert.Equal(3f, rect.Width);
    }

    [Fact]
    public void CaretRenderer_GetCaretRect_EmptyDoc_ReturnsNonZeroHeight()
    {
        var context = new MockDrawingContext();
        var engine = new TextLayoutEngine();
        var doc = new EditorDocument(); // one empty paragraph
        var layout = engine.Layout(doc, 800f, context);
        var renderer = new CaretRenderer();

        var rect = renderer.GetCaretRect(new DocumentPosition(0, 0, 0), layout, doc, context);

        Assert.True(rect.Height > 0);
        Assert.Equal(renderer.CaretWidth, rect.Width);
    }

    [Theory]
    [InlineData(ListType.Bullet)]
    [InlineData(ListType.Number)]
    public void CaretRenderer_EmptyListParagraph_StartsAtListTextIndent(ListType listType)
    {
        var context = new MockDrawingContext();
        var engine = new TextLayoutEngine();

        var para = new Paragraph();
        para.Style.ListType = listType;
        var doc = new EditorDocument(new Block[] { para });
        var layout = engine.Layout(doc, 800f, context);
        var renderer = new CaretRenderer();

        var x = renderer.GetCaretX(new DocumentPosition(0, 0, 0), layout, doc, context);

        Assert.Equal(24f, x);
    }

    [Fact]
    public void CaretRenderer_Render_WhenVisible_CallsFillRect()
    {
        var context = new MockDrawingContext();
        var engine = new TextLayoutEngine();
        var para = new Paragraph();
        para.AppendRun("Hello");
        var doc = new EditorDocument(new Block[] { para });
        var layout = engine.Layout(doc, 800f, context);
        var renderer = new CaretRenderer();
        var caret = new Caret();
        caret.MoveTo(new DocumentPosition(0, 0, 2));
        caret.IsVisible = true;

        renderer.Render(caret, layout, doc, context);

        Assert.Single(context.FillRectCalls);
    }

    [Fact]
    public void CaretRenderer_Render_WhenInvisible_DoesNotCallFillRect()
    {
        var context = new MockDrawingContext();
        var engine = new TextLayoutEngine();
        var para = new Paragraph();
        para.AppendRun("Hello");
        var doc = new EditorDocument(new Block[] { para });
        var layout = engine.Layout(doc, 800f, context);
        var renderer = new CaretRenderer();
        var caret = new Caret { IsVisible = false };

        renderer.Render(caret, layout, doc, context);

        Assert.Empty(context.FillRectCalls);
    }
}
