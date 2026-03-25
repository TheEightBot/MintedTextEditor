using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Rendering;
using MintedTextEditor.Core.Toolbar;

namespace MintedTextEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="DocumentEditor.NormalizePosition"/>, verifying that a stale or
/// out-of-range <see cref="DocumentPosition"/> is correctly remapped after inline splits.
/// </summary>
public class NormalizePositionTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static EditorDocument MakeDoc(string text)
    {
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun(text, TextStyle.Default);
        return doc;
    }

    // ── Already-valid positions stay the same ─────────────────────────────────

    [Fact]
    public void ValidPosition_Unchanged()
    {
        var doc = MakeDoc("Hello World");
        var pos = new DocumentPosition(0, 0, 5);
        var result = DocumentEditor.NormalizePosition(doc, pos);
        Assert.Equal(pos, result);
    }

    [Fact]
    public void ValidEnd_Unchanged()
    {
        var doc = MakeDoc("Hello");
        var pos = new DocumentPosition(0, 0, 5);
        var result = DocumentEditor.NormalizePosition(doc, pos);
        Assert.Equal(pos, result);
    }

    // ── Position after a style-split inline ──────────────────────────────────

    [Fact]
    public void AfterSplit_PositionMapsToCorrectNewInline()
    {
        // Start: single run "Hello World" (inline 0)
        // Bold "World" → split into: inline0="Hello ", inline1="World" (bold)
        // A position stored as (0, 0, 11) — end of original run — should remap to (0, 1, 5)
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("Hello World", TextStyle.Default);

        // Simulate what bold toggle does: split at character 6
        var boldRange = new TextRange(
            new DocumentPosition(0, 0, 6),
            new DocumentPosition(0, 0, 11));
        DocumentEditor.ApplyTextStyle(doc, boldRange, s => s.WithBold(true));

        // para.Inlines should now be: [TextRun("Hello ", normal), TextRun("World", bold)]
        Assert.Equal(2, para.Inlines.Count);

        // Stale position: (0, 0, 11) — was valid before the split
        var stalePos = new DocumentPosition(0, 0, 11);
        var normalized = DocumentEditor.NormalizePosition(doc, stalePos);

        // Absolute offset 11 = all of inline0 (6) + 5 chars into inline1 → (0, 1, 5)
        Assert.Equal(0, normalized.BlockIndex);
        Assert.Equal(1, normalized.InlineIndex);
        Assert.Equal(5, normalized.Offset);
    }

    [Fact]
    public void OffsetExceedsInlineLength_ClampsToNextInline()
    {
        // Two inlines: "AB" and "CD"
        // Position (0, 0, 3) — offset 3 exceeds inline0 length 2 → should map to (0, 1, 1)
        var doc = new EditorDocument();
        var para = (Paragraph)doc.Blocks[0];
        para.AppendRun("AB", TextStyle.Default);
        para.AppendRun("CD", TextStyle.Default);

        var pos = new DocumentPosition(0, 0, 3);
        var result = DocumentEditor.NormalizePosition(doc, pos);

        // abs offset for (0, 0, 3): inlines before index 0 = 0 chars, + offset 3 = abs 3.
        // inline0 len=2 → 3 > 2 → move to inline1 (running=2), offset = 3-2 = 1.
        Assert.Equal(1, result.InlineIndex);
        Assert.Equal(1, result.Offset);
    }

    [Fact]
    public void OffsetPastEnd_ClampsToLastInline()
    {
        var doc = MakeDoc("Hello");
        // Position with offset beyond the only run's length
        var pos = new DocumentPosition(0, 0, 99);
        var result = DocumentEditor.NormalizePosition(doc, pos);

        // Should clamp to end of last inline
        Assert.Equal(0, result.InlineIndex);
        Assert.Equal(5, result.Offset); // "Hello" has 5 chars
    }

    [Fact]
    public void EmptyParagraph_ReturnsZeroPosition()
    {
        var doc = new EditorDocument();
        var pos = new DocumentPosition(0, 0, 5);
        var result = DocumentEditor.NormalizePosition(doc, pos);

        Assert.Equal(0, result.BlockIndex);
        Assert.Equal(0, result.InlineIndex);
        Assert.Equal(0, result.Offset);
    }

    // ── Toolbar overflow model ─────────────────────────────────────────────────

    [Fact]
    public void ToolbarDefinition_DefaultMaxRows_IsZero()
    {
        var def = Toolbar.ToolbarDefinition.CreateDefault();
        Assert.Equal(0, def.MaxRows);
    }

    [Fact]
    public void ToolbarDefinition_MaxRowsRoundTrip()
    {
        var def = new Toolbar.ToolbarDefinition { MaxRows = 2 };
        Assert.Equal(2, def.MaxRows);
    }

    // ── ToolbarRenderer overflow ───────────────────────────────────────────────

    [Fact]
    public void ToolbarRenderer_NoOverflow_WhenMaxRowsIsZero()
    {
        var def = Toolbar.ToolbarDefinition.CreateDefault();
        var renderer = new Toolbar.ToolbarRenderer(def);

        // Render into a wide bounds (all items fit)
        var ctx = new MockDrawingContext();
        renderer.Render(ctx, new Rendering.EditorRect(0, 0, 2000, 400));

        Assert.False(renderer.HasOverflow);
        Assert.Empty(renderer.OverflowItems);
    }

    [Fact]
    public void ToolbarRenderer_OverflowItems_WhenMaxRows1AndNarrow()
    {
        var def = Toolbar.ToolbarDefinition.CreateDefault();
        def.MaxRows = 1;
        var renderer = new Toolbar.ToolbarRenderer(def)
        {
            ButtonSize    = 36f,
            ButtonPadding = 5f,
        };

        // Very narrow width → only a couple of buttons can fit in one row
        var ctx = new MockDrawingContext();
        renderer.Render(ctx, new Rendering.EditorRect(0, 0, 100, 200));

        // At 36+5 per button, 100px wide: room for ~2 buttons before overflow button eats space
        Assert.True(renderer.HasOverflow);
        Assert.NotEmpty(renderer.OverflowItems);
    }

    [Fact]
    public void ToolbarRenderer_IsOverflowPanelOpen_StartsAs_False()
    {
        var def = Toolbar.ToolbarDefinition.CreateDefault();
        def.MaxRows = 1;
        var renderer = new Toolbar.ToolbarRenderer(def);
        var ctx = new MockDrawingContext();
        renderer.Render(ctx, new Rendering.EditorRect(0, 0, 100, 200));

        Assert.False(renderer.IsOverflowPanelOpen);
    }
}
