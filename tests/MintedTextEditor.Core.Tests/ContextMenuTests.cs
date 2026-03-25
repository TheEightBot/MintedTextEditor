using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.ContextMenu;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Tests;

public class ContextMenuTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static EditorContext MakeCtx(EditorDocument? doc = null, Selection? sel = null)
    {
        doc ??= MakeDoc();
        return new EditorContext(doc, sel ?? new Selection(), new UndoManager(),
            new FormattingEngine(), new FontFormattingEngine());
    }

    private static EditorDocument MakeDoc(string text = "Hello world")
    {
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun(text);
        return doc;
    }

    // ── ContextMenuDefinition ─────────────────────────────────────────────────

    [Fact]
    public void Definition_VisibleItems_ExcludesHiddenItems()
    {
        var def = new ContextMenuDefinition();
        def.Items.Add(new ContextMenuItem { Label = "Visible", IsVisible = true });
        def.Items.Add(new ContextMenuItem { Label = "Hidden",  IsVisible = false });

        Assert.Single(def.VisibleItems);
        Assert.Equal("Visible", def.VisibleItems.First().Label);
    }

    // ── ContextMenuFactory – no selection, no hyperlink ───────────────────────

    [Fact]
    public void CreateDefault_NoSelection_CutAndCopyAreDisabled()
    {
        var ctx = MakeCtx();
        var def = ContextMenuFactory.CreateDefault(ctx);

        var cut  = def.Items.First(i => i.Label == "Cut");
        var copy = def.Items.First(i => i.Label == "Copy");

        Assert.False(cut.IsEnabled);
        Assert.False(copy.IsEnabled);
    }

    [Fact]
    public void CreateDefault_WithSelection_CutAndCopyAreEnabled()
    {
        var doc = MakeDoc();
        var sel = new Selection();
        sel.Set(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 5));
        var ctx = MakeCtx(doc, sel);

        var def  = ContextMenuFactory.CreateDefault(ctx);
        var cut  = def.Items.First(i => i.Label == "Cut");
        var copy = def.Items.First(i => i.Label == "Copy");

        Assert.True(cut.IsEnabled);
        Assert.True(copy.IsEnabled);
    }

    [Fact]
    public void CreateDefault_NoHyperlink_ShowsInsertHyperlinkItem()
    {
        var ctx = MakeCtx();
        var def = ContextMenuFactory.CreateDefault(ctx);

        Assert.Contains(def.Items, i => i.Label == "Insert Hyperlink");
        Assert.DoesNotContain(def.Items, i => i.Label == "Edit Hyperlink");
        Assert.DoesNotContain(def.Items, i => i.Label == "Remove Hyperlink");
    }

    [Fact]
    public void CreateDefault_OnHyperlink_ShowsHyperlinkEditItems()
    {
        var doc  = MakeDoc("Visit example.com today");
        var para = (Paragraph)doc.Blocks[0];
        var range = new TextRange(
            new DocumentPosition(0, 0, 6),
            new DocumentPosition(0, 0, 17));
        HyperlinkEngine.InsertHyperlink(doc, range, "https://example.com");

        // The hyperlink inline is now at inline index 1 (after the leading run).
        // Select so the caret sits on the HyperlinkInline.
        var inlineIndex = para.Inlines.ToList().FindIndex(i => i is HyperlinkInline);
        var sel = new Selection();
        sel.Set(new DocumentPosition(0, inlineIndex, 0),
                new DocumentPosition(0, inlineIndex, 1));

        var ctx = MakeCtx(doc, sel);
        var def = ContextMenuFactory.CreateDefault(ctx);

        Assert.Contains(def.Items, i => i.Label == "Edit Hyperlink");
        Assert.Contains(def.Items, i => i.Label == "Remove Hyperlink");
        Assert.Contains(def.Items, i => i.Label == "Open Hyperlink");
        Assert.DoesNotContain(def.Items, i => i.Label == "Insert Hyperlink");
    }

    [Fact]
    public void CreateDefault_ContainsSelectAll_InsertImage_InsertTable()
    {
        var ctx = MakeCtx();
        var def = ContextMenuFactory.CreateDefault(ctx);

        Assert.Contains(def.Items, i => i.Label == "Select All");
        Assert.Contains(def.Items, i => i.Label == "Insert Image");
        Assert.Contains(def.Items, i => i.Label == "Insert Table");
    }

    // ── Custom items via ContextMenuItemsRequestedEventArgs ──────────────────

    [Fact]
    public void CustomItems_CanBeAppendedViaEventArgs()
    {
        var ctx = MakeCtx();
        var def = ContextMenuFactory.CreateDefault(ctx);

        // Simulate host handling the extensibility event.
        var args = new ContextMenuItemsRequestedEventArgs(def, ctx);
        args.Definition.Items.Add(new ContextMenuItem
        {
            Label     = "Custom Action",
            IsEnabled = true,
        });

        Assert.Contains(def.Items, i => i.Label == "Custom Action");
    }

    [Fact]
    public void CustomItems_CanRemoveExistingItems()
    {
        var ctx = MakeCtx();
        var def = ContextMenuFactory.CreateDefault(ctx);

        var args = new ContextMenuItemsRequestedEventArgs(def, ctx);
        var paste = args.Definition.Items.FirstOrDefault(i => i.Label == "Paste");
        if (paste is not null) args.Definition.Items.Remove(paste);

        Assert.DoesNotContain(def.Items, i => i.Label == "Paste");
    }

    // ── ContextMenuRenderer ──────────────────────────────────────────────────

    [Fact]
    public void Renderer_IsOpen_FalseByDefault()
    {
        var renderer = new ContextMenuRenderer();
        Assert.False(renderer.IsOpen);
    }

    [Fact]
    public void Renderer_Open_SetsIsOpenTrue()
    {
        var ctx  = MakeCtx();
        var def  = ContextMenuFactory.CreateDefault(ctx);
        var renderer = new ContextMenuRenderer();
        var viewport = new EditorRect(0, 0, 800, 600);

        renderer.Open(def, 100, 150, viewport);

        Assert.True(renderer.IsOpen);
        Assert.Same(def, renderer.Definition);
    }

    [Fact]
    public void Renderer_Close_SetsIsOpenFalse()
    {
        var ctx  = MakeCtx();
        var def  = ContextMenuFactory.CreateDefault(ctx);
        var renderer = new ContextMenuRenderer();
        renderer.Open(def, 100, 150, new EditorRect(0, 0, 800, 600));
        renderer.Close();

        Assert.False(renderer.IsOpen);
    }

    [Fact]
    public void Renderer_Open_ClampsPositionToViewport()
    {
        var ctx  = MakeCtx();
        var def  = ContextMenuFactory.CreateDefault(ctx);
        var renderer = new ContextMenuRenderer();
        // Open far outside the viewport — position should be clamped.
        renderer.Open(def, 900, 700, new EditorRect(0, 0, 800, 600));

        Assert.True(renderer.X < 900);
        Assert.True(renderer.Y < 700);
    }

    [Fact]
    public void Renderer_HitTest_ReturnsNullWhenClosed()
    {
        var renderer = new ContextMenuRenderer();
        Assert.Null(renderer.HitTest(50, 50));
    }

    [Fact]
    public void Renderer_HitTest_ReturnsItemUnderPointer()
    {
        var ctx  = MakeCtx();
        var def  = ContextMenuFactory.CreateDefault(ctx);
        var renderer = new ContextMenuRenderer();
        var mock  = new MockDrawingContext();
        renderer.Open(def, 0, 0, new EditorRect(0, 0, 800, 600));
        renderer.Render(mock);

        // The first item starts at (padding, padding) with height ItemHeight.
        float midY = renderer.Padding + renderer.ItemHeight / 2f;
        float midX = renderer.Padding + renderer.MinWidth / 2f;
        var hit = renderer.HitTest(midX, midY);

        Assert.NotNull(hit);
    }

    [Fact]
    public void Renderer_IsClickOutside_ReturnsTrueForPointOutsidePopup()
    {
        var ctx      = MakeCtx();
        var def      = ContextMenuFactory.CreateDefault(ctx);
        var renderer = new ContextMenuRenderer();
        renderer.Open(def, 100, 100, new EditorRect(0, 0, 800, 600));

        Assert.True(renderer.IsClickOutside(5, 5));    // outside top-left
        Assert.False(renderer.IsClickOutside(110, 110)); // inside popup
    }

    [Fact]
    public void Renderer_HandleKey_EscapeDismissesMenu()
    {
        var ctx      = MakeCtx();
        var def      = ContextMenuFactory.CreateDefault(ctx);
        var renderer = new ContextMenuRenderer();
        renderer.Open(def, 100, 100, new EditorRect(0, 0, 800, 600));

        renderer.HandleKey(ConsoleKey.Escape);

        Assert.False(renderer.IsOpen);
    }

    [Fact]
    public void Renderer_HandleKey_DownArrow_AdvancesFocusedIndex()
    {
        var ctx      = MakeCtx();
        var def      = ContextMenuFactory.CreateDefault(ctx);
        var renderer = new ContextMenuRenderer();
        renderer.Open(def, 100, 100, new EditorRect(0, 0, 800, 600));

        Assert.Equal(-1, renderer.FocusedIndex);
        renderer.HandleKey(ConsoleKey.DownArrow);
        Assert.Equal(0, renderer.FocusedIndex);
        renderer.HandleKey(ConsoleKey.DownArrow);
        Assert.Equal(1, renderer.FocusedIndex);
    }

    [Fact]
    public void Renderer_HandleKey_EnterReturnsSelectedItem()
    {
        var ctx      = MakeCtx();
        var def      = ContextMenuFactory.CreateDefault(ctx);
        var renderer = new ContextMenuRenderer();
        renderer.Open(def, 100, 100, new EditorRect(0, 0, 800, 600));

        // Navigate to first item then press Enter.
        renderer.HandleKey(ConsoleKey.DownArrow);
        var item = renderer.HandleKey(ConsoleKey.Enter);

        Assert.NotNull(item);
        Assert.Equal(def.VisibleItems.First().Label, item!.Label);
    }

    [Fact]
    public void Renderer_Render_DoesNotThrow()
    {
        var ctx      = MakeCtx();
        var def      = ContextMenuFactory.CreateDefault(ctx);
        var renderer = new ContextMenuRenderer();
        renderer.Open(def, 50, 50, new EditorRect(0, 0, 800, 600));

        var mock = new MockDrawingContext();
        var ex   = Record.Exception(() => renderer.Render(mock));
        Assert.Null(ex);
    }
}
