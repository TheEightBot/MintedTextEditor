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

    private static LayoutLine CreateLine(string text, float y)
    {
        var line = new LayoutLine { Y = y, Height = 12f, Baseline = 9f, BlockIndex = 0, LineIndexInBlock = 0 };
        line.Runs.Add(new LayoutRun(text, 0f, 20f, null, TextStyle.Default, 0));
        return line;
    }
}
