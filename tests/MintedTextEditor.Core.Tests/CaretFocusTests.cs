using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Layout;

namespace MintedTextEditor.Core.Tests;

/// <summary>
/// Issue #6 symptom 3: the caret must not be drawn (or blink) while the editor
/// does not hold keyboard focus.
/// </summary>
public class CaretFocusTests
{
    private static (DocumentLayout layout, EditorDocument doc, MockDrawingContext ctx) Setup()
    {
        var ctx = new MockDrawingContext();
        var para = new Paragraph();
        para.AppendRun("Hello");
        var doc = new EditorDocument(new Block[] { para });
        var layout = new TextLayoutEngine().Layout(doc, 800f, ctx);
        return (layout, doc, ctx);
    }

    [Fact]
    public void Caret_IsFocused_DefaultsToTrue()
    {
        var caret = new Caret();
        Assert.True(caret.IsFocused);
        Assert.True(caret.IsDrawn);
    }

    [Fact]
    public void Caret_Unfocused_IsNotDrawn()
    {
        var caret = new Caret { IsFocused = false };
        Assert.False(caret.IsDrawn);
    }

    [Fact]
    public void Caret_Unfocused_UpdateBlinkDoesNotToggle()
    {
        var caret = new Caret { BlinkIntervalMs = 0, IsFocused = false };
        bool before = caret.IsVisible;
        caret.UpdateBlink();
        caret.UpdateBlink();
        Assert.Equal(before, caret.IsVisible);
        Assert.False(caret.IsDrawn);
    }

    [Fact]
    public void Caret_Refocus_IsDrawnImmediately()
    {
        var caret = new Caret { BlinkIntervalMs = 0 };
        caret.UpdateBlink(); // toggles to hidden
        Assert.False(caret.IsVisible);

        caret.IsFocused = false;
        caret.IsFocused = true;

        Assert.True(caret.IsVisible);
        Assert.True(caret.IsDrawn);
    }

    [Fact]
    public void CaretRenderer_Unfocused_DrawsNothing()
    {
        var (layout, doc, ctx) = Setup();
        var caret = new Caret { IsFocused = false };

        new CaretRenderer().Render(caret, layout, doc, ctx);

        Assert.Empty(ctx.FillRectCalls);
    }

    [Fact]
    public void CaretRenderer_Focused_DrawsCaret()
    {
        var (layout, doc, ctx) = Setup();
        var caret = new Caret();

        new CaretRenderer().Render(caret, layout, doc, ctx);

        Assert.Single(ctx.FillRectCalls);
    }
}
