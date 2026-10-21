using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Layout;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Tests;

public class DocumentRendererTests
{
    [Fact]
    public void Render_CullsOffscreenLines_InVisibleBlock()
    {
        var layout = new DocumentLayout();
        var block = new LayoutBlock { Y = 0f, TotalHeight = 80f, BlockIndex = 0 };

        block.Lines.Add(CreateLine("L1", y: 0f));
        block.Lines.Add(CreateLine("L2", y: 20f));
        block.Lines.Add(CreateLine("L3", y: 40f));
        block.Lines.Add(CreateLine("L4", y: 60f));

        layout.Blocks.Add(block);
        layout.TotalHeight = 80f;

        var renderer = new DocumentRenderer
        {
            ScrollOffset = 25f,
            ShowScrollbar = false
        };

        var ctx = new MockDrawingContext();
        renderer.Render(layout, new EditorRect(0f, 0f, 300f, 20f), ctx);

        var rendered = ctx.DrawTextCalls.Select(c => c.Text).ToList();
        Assert.DoesNotContain("L1", rendered);
        Assert.Contains("L2", rendered);
        Assert.Contains("L3", rendered);
        Assert.DoesNotContain("L4", rendered);
    }

    [Fact]
    public void Render_CullsOffscreenTableCellLines()
    {
        var cell = new LayoutBlock { TotalHeight = 90f, BlockIndex = 0 };
        cell.Lines.Add(CreateLine("T1", y: 0f));
        cell.Lines.Add(CreateLine("T2", y: 30f));
        cell.Lines.Add(CreateLine("T3", y: 60f));

        var table = new TableLayoutBlock
        {
            Y = 0f,
            TotalHeight = 100f,
            BlockIndex = 0,
            ColumnWidths = new[] { 120f },
            RowHeights = new[] { 90f },
            CellPadding = 0f,
            Cells = new[] { new[] { cell } }
        };

        var layout = new DocumentLayout();
        layout.Blocks.Add(table);
        layout.TotalHeight = 100f;

        var renderer = new DocumentRenderer
        {
            ScrollOffset = 35f,
            ShowScrollbar = false
        };

        var ctx = new MockDrawingContext();
        renderer.Render(layout, new EditorRect(0f, 0f, 300f, 20f), ctx);

        var rendered = ctx.DrawTextCalls.Select(c => c.Text).ToList();
        Assert.DoesNotContain("T1", rendered);
        Assert.Contains("T2", rendered);
        Assert.DoesNotContain("T3", rendered);
    }

    // ── Selection text colour ────────────────────────────────────

    private static (DocumentLayout Layout, IReadOnlyList<EditorRect> Rects) LayoutWithSelection(
        EditorDocument doc, DocumentPosition from, DocumentPosition to, MockDrawingContext ctx)
    {
        var layout = new TextLayoutEngine().Layout(doc, 500f, ctx);
        var selection = new MintedTextEditor.Core.Editing.Selection();
        selection.CollapseTo(from);
        selection.ExtendTo(to);
        var rects = new MintedTextEditor.Core.Editing.SelectionRenderer(new MintedTextEditor.Core.Editing.CaretRenderer())
            .GetSelectionRects(selection, layout, doc, ctx);
        return (layout, rects);
    }

    [Fact]
    public void Render_SelectionTextColor_RecoloursOnlyTheSelectedSpan()
    {
        var doc = new EditorDocument(new Block[] { new Paragraph("Hello world") });
        var ctx = new MockDrawingContext();
        // Select "world" (chars 6–11): 8px per char in the mock, so x = 48..88.
        var (layout, rects) = LayoutWithSelection(doc, new(0, 0, 6), new(0, 0, 11), ctx);

        var renderer = new DocumentRenderer
        {
            ShowScrollbar = false,
            DefaultTextColor = EditorColor.Black,
            SelectionTextColor = EditorColor.White,
            SelectionRects = rects,
        };
        ctx.DrawTextCalls.Clear();
        renderer.Render(layout, new EditorRect(0f, 0f, 500f, 200f), ctx);

        // Every run is drawn in each of three passes, clipped to before / inside / after the selection.
        var white = ctx.DrawTextCalls.Where(c => c.Paint.Color == EditorColor.White).ToList();
        var black = ctx.DrawTextCalls.Where(c => c.Paint.Color == EditorColor.Black).ToList();
        Assert.Equal(3 * layout.Blocks[0].Lines[0].Runs.Count, white.Count + black.Count);

        Assert.Contains(white, c => c.Text == "world");
        Assert.All(white, c =>
        {
            Assert.Equal(48f, c.Clip!.Value.X, 3);
            Assert.Equal(88f, c.Clip.Value.Right, 3);
        });

        // Outside the selection the text keeps its colour.
        Assert.Contains(black, c => c.Text == "Hello " && c.Clip!.Value.Right <= 48.001f);
        Assert.All(black, c => Assert.True(c.Clip!.Value.Right <= 48.001f || c.Clip.Value.X >= 87.999f));
    }

    [Fact]
    public void Render_SelectionTextColor_LeavesUnselectedLinesAlone()
    {
        var doc = new EditorDocument(new Block[] { new Paragraph("One"), new Paragraph("Two") });
        var ctx = new MockDrawingContext();
        var (layout, rects) = LayoutWithSelection(doc, new(0, 0, 0), new(0, 0, 3), ctx);

        var renderer = new DocumentRenderer
        {
            ShowScrollbar = false,
            DefaultTextColor = EditorColor.Black,
            SelectionTextColor = EditorColor.White,
            SelectionRects = rects,
        };
        ctx.DrawTextCalls.Clear();
        renderer.Render(layout, new EditorRect(0f, 0f, 500f, 200f), ctx);

        var two = Assert.Single(ctx.DrawTextCalls, c => c.Text == "Two");
        Assert.Equal(EditorColor.Black, two.Paint.Color);
        Assert.Contains(ctx.DrawTextCalls, c => c.Text == "One" && c.Paint.Color == EditorColor.White);
    }

    [Fact]
    public void Render_TransparentSelectionTextColor_KeepsOriginalColours()
    {
        var doc = new EditorDocument(new Block[] { new Paragraph("Hello world") });
        var ctx = new MockDrawingContext();
        var (layout, rects) = LayoutWithSelection(doc, new(0, 0, 0), new(0, 0, 5), ctx);

        var renderer = new DocumentRenderer
        {
            ShowScrollbar = false,
            DefaultTextColor = EditorColor.Black,
            SelectionTextColor = EditorColor.Transparent,
            SelectionRects = rects,
        };
        ctx.DrawTextCalls.Clear();
        renderer.Render(layout, new EditorRect(0f, 0f, 500f, 200f), ctx);

        // Drawn once per run, as without a selection.
        Assert.Equal(layout.Blocks[0].Lines[0].Runs.Count, ctx.DrawTextCalls.Count);
        Assert.All(ctx.DrawTextCalls, c => Assert.Equal(EditorColor.Black, c.Paint.Color));
    }

    [Fact]
    public void Render_SelectedText_SkipsRunHighlightInsideSelection()
    {
        var highlight = new EditorColor(255, 255, 0);
        var para = new Paragraph();
        para.AppendRun("Marked", TextStyle.Default.WithHighlightColor(highlight));
        var doc = new EditorDocument(new Block[] { para });
        var ctx = new MockDrawingContext();
        var (layout, rects) = LayoutWithSelection(doc, new(0, 0, 0), new(0, 0, 6), ctx);

        var renderer = new DocumentRenderer
        {
            ShowScrollbar = false,
            SelectionTextColor = EditorColor.White,
            SelectionRects = rects,
        };
        ctx.FillRectCalls.Clear();
        renderer.Render(layout, new EditorRect(0f, 0f, 500f, 200f), ctx);

        // Whole run selected: the highlight is filled only by the two unselected (empty) passes.
        Assert.Equal(2, ctx.FillRectCalls.Count(c => c.Paint.Color == highlight));
    }

    // ── Hyperlink hover colour ───────────────────────────────────

    [Fact]
    public void Render_HoveredHyperlink_UsesHoverColour()
    {
        var hover = new EditorColor(0, 70, 153);
        var link = new HyperlinkInline("https://example.com");
        link.AddChild(new TextRun("link"));
        var other = new HyperlinkInline("https://example.org");
        other.AddChild(new TextRun("other"));
        var para = new Paragraph("Go ");
        para.AddInline(link);
        para.AppendRun(" ");
        para.AddInline(other);
        var doc = new EditorDocument(new Block[] { para });
        var ctx = new MockDrawingContext();
        var engine = new TextLayoutEngine { HyperlinkTextColor = new EditorColor(0, 102, 204) };
        var layout = engine.Layout(doc, 500f, ctx);

        var renderer = new DocumentRenderer
        {
            ShowScrollbar = false,
            HoveredHyperlink = link,
            HyperlinkHoverColor = hover,
        };
        renderer.Render(layout, new EditorRect(0f, 0f, 500f, 200f), ctx);

        Assert.Equal(hover, Assert.Single(ctx.DrawTextCalls, c => c.Text.StartsWith("link")).Paint.Color);
        Assert.Equal(new EditorColor(0, 102, 204), Assert.Single(ctx.DrawTextCalls, c => c.Text == "other").Paint.Color);

        // Without a hover colour the link keeps its normal colour.
        ctx.DrawTextCalls.Clear();
        renderer.HyperlinkHoverColor = EditorColor.Transparent;
        renderer.Render(layout, new EditorRect(0f, 0f, 500f, 200f), ctx);
        Assert.Equal(new EditorColor(0, 102, 204), Assert.Single(ctx.DrawTextCalls, c => c.Text.StartsWith("link")).Paint.Color);
    }

    private static LayoutLine CreateLine(string text, float y)
    {
        var line = new LayoutLine { Y = y, Height = 12f, Baseline = 9f, BlockIndex = 0, LineIndexInBlock = 0 };
        line.Runs.Add(new LayoutRun(text, 0f, 20f, null, TextStyle.Default, 0));
        return line;
    }
}
