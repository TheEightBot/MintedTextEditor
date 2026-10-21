using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.ContextMenu;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Rendering;
using MintedTextEditor.Core.Theming;

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

    private static EditorContext MakeCtxWithSelection()
    {
        var sel = new Selection();
        sel.Set(new DocumentPosition(0, 0, 0), new DocumentPosition(0, 0, 5));
        return MakeCtx(MakeDoc(), sel);
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

        Assert.Contains(def.Items, i => i.Label == "Insert hyperlink");
        Assert.DoesNotContain(def.Items, i => i.Label == "Edit hyperlink…");
        Assert.DoesNotContain(def.Items, i => i.Label == "Remove hyperlink");
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

        Assert.Contains(def.Items, i => i.Label == "Edit hyperlink…");
        Assert.Contains(def.Items, i => i.Label == "Remove hyperlink");
        Assert.Contains(def.Items, i => i.Label == "Open hyperlink");
        Assert.DoesNotContain(def.Items, i => i.Label == "Insert hyperlink");
    }

    [Fact]
    public void CreateDefault_ContainsSelectAll_InsertImage_InsertTable()
    {
        var ctx = MakeCtx();
        var def = ContextMenuFactory.CreateDefault(ctx);

        Assert.Contains(def.Items, i => i.Label == "Select all");
        Assert.Contains(def.Items, i => i.Label == "Insert image");
        Assert.Contains(def.Items, i => i.Label == "Insert table");
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
        var ctx      = MakeCtxWithSelection(); // Cut and Copy enabled
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
        var ctx      = MakeCtxWithSelection();
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

    // ── ContextMenuFactory – selection state ─────────────────────────────────

    [Fact]
    public void CreateDefault_CaretOnHyperlink_ShowsHyperlinkEditItems()
    {
        var doc = MakeDoc("Visit example.com today");
        HyperlinkEngine.InsertHyperlink(doc,
            new TextRange(new DocumentPosition(0, 0, 6), new DocumentPosition(0, 0, 17)),
            "https://example.com");
        var para = (Paragraph)doc.Blocks[0];
        int linkIndex = para.Inlines.ToList().FindIndex(i => i is HyperlinkInline);

        // A tap or long press on link text leaves a collapsed caret inside the link.
        var sel = new Selection();
        sel.CollapseTo(new DocumentPosition(0, linkIndex, 3));
        var def = ContextMenuFactory.CreateDefault(MakeCtx(doc, sel));

        Assert.Contains(def.Items, i => i.Label == "Remove hyperlink");
        Assert.Contains(def.Items, i => i.Label == "Open hyperlink");
        Assert.DoesNotContain(def.Items, i => i.Label == "Insert hyperlink");
    }

    [Fact]
    public void CreateDefault_ClipboardAndSelectAllItems_HaveExpectedCommands()
    {
        var def = ContextMenuFactory.CreateDefault(MakeCtxWithSelection());

        Assert.IsType<CutCommand>(def.Items.First(i => i.Label == "Cut").Command);
        Assert.IsType<CopyCommand>(def.Items.First(i => i.Label == "Copy").Command);
        Assert.IsType<SelectAllCommand>(def.Items.First(i => i.Label == "Select all").Command);
        // Paste reads the clipboard asynchronously, so the host runs it.
        var paste = def.Items.First(i => i.Label == "Paste");
        Assert.Null(paste.Command);
        Assert.Equal("paste", paste.Icon);
        Assert.True(paste.IsEnabled);
    }

    [Fact]
    public void CreateDefault_OutsideTable_HasNoTableEditingItems()
    {
        var def = ContextMenuFactory.CreateDefault(MakeCtx());

        Assert.DoesNotContain(def.Items, i => i.Label == "Delete row");
        Assert.DoesNotContain(def.Items, i => i.Label == "Delete table");
    }

    [Fact]
    public void CreateDefault_InSingleRowTableCell_AddsTableItems_DeleteRowDisabled()
    {
        var doc = new EditorDocument();
        doc.Blocks.Clear();
        doc.AddBlock(new TableBlock(1, 2));
        var sel = new Selection();
        sel.CollapseTo(new DocumentPosition(0, 0, 0, 0, 0));

        var def = ContextMenuFactory.CreateDefault(MakeCtx(doc, sel));

        Assert.Contains(def.Items, i => i.Label == "Insert row above");
        Assert.Contains(def.Items, i => i.Label == "Delete table");
        Assert.False(def.Items.First(i => i.Label == "Delete row").IsEnabled);
        Assert.True(def.Items.First(i => i.Label == "Delete column").IsEnabled);
    }

    // ── ContextMenuRenderer – hit testing ────────────────────────────────────

    private static (ContextMenuRenderer Renderer, ContextMenuDefinition Def) OpenAndRender(
        EditorContext ctx, float x = 0, float y = 0)
    {
        var def      = ContextMenuFactory.CreateDefault(ctx);
        var renderer = new ContextMenuRenderer();
        renderer.Open(def, x, y, new EditorRect(0, 0, 800, 600));
        renderer.Render(new MockDrawingContext());
        return (renderer, def);
    }

    [Fact]
    public void Renderer_HitTest_BeforeRender_ReturnsNull()
    {
        var renderer = new ContextMenuRenderer();
        renderer.Open(ContextMenuFactory.CreateDefault(MakeCtx()), 0, 0, new EditorRect(0, 0, 800, 600));

        Assert.Null(renderer.HitTest(renderer.Padding + 10, renderer.Padding + 10));
    }

    [Fact]
    public void Renderer_HitTest_AccountsForSeparators()
    {
        var (renderer, _) = OpenAndRender(MakeCtxWithSelection(), 50, 40);

        // Cut, Copy, Paste, [separator], Select All
        float selectAllMidY = 40 + renderer.Padding + 3 * renderer.ItemHeight
                              + renderer.SeparatorHeight + renderer.ItemHeight / 2f;
        var hit = renderer.HitTest(50 + renderer.MinWidth / 2f, selectAllMidY);

        Assert.Equal("Select all", hit?.Label);
        Assert.Equal(3, renderer.FocusedIndex);
    }

    [Fact]
    public void Renderer_HitTest_OnSeparatorGap_ReturnsNullButIsInsidePopup()
    {
        var (renderer, _) = OpenAndRender(MakeCtxWithSelection());

        float gapY = renderer.Padding + 3 * renderer.ItemHeight + renderer.SeparatorHeight / 2f;
        Assert.Null(renderer.HitTest(renderer.MinWidth / 2f, gapY));
        Assert.False(renderer.IsClickOutside(renderer.MinWidth / 2f, gapY));
    }

    [Fact]
    public void Renderer_HitTest_DisabledItem_ReturnsItemWithoutFocusingIt()
    {
        var (renderer, _) = OpenAndRender(MakeCtx()); // no selection: Cut disabled
        renderer.HandleKey(ConsoleKey.DownArrow);

        var hit = renderer.HitTest(renderer.MinWidth / 2f, renderer.Padding + renderer.ItemHeight / 2f);

        Assert.Equal("Cut", hit?.Label);
        Assert.False(hit!.IsEnabled);
        Assert.Equal(-1, renderer.FocusedIndex);
    }

    [Fact]
    public void Renderer_HitTest_OutsideItems_ClearsFocus()
    {
        var (renderer, _) = OpenAndRender(MakeCtxWithSelection());
        renderer.HandleKey(ConsoleKey.DownArrow);

        Assert.Null(renderer.HitTest(700, 500));
        Assert.Equal(-1, renderer.FocusedIndex);
    }

    [Fact]
    public void Renderer_Bounds_MatchesOpenPositionAndIsEmptyWhenClosed()
    {
        var renderer = new ContextMenuRenderer();
        Assert.Equal(default, renderer.Bounds);

        renderer.Open(ContextMenuFactory.CreateDefault(MakeCtx()), 120, 80, new EditorRect(0, 0, 800, 600));

        Assert.Equal(120, renderer.Bounds.X);
        Assert.Equal(80, renderer.Bounds.Y);
        Assert.Equal(renderer.MinWidth, renderer.Bounds.Width);
        Assert.True(renderer.IsClickOutside(119, 81));
        Assert.False(renderer.IsClickOutside(121, 81));
    }

    [Fact]
    public void Renderer_Open_ClampsInsideViewportEdges()
    {
        var renderer = new ContextMenuRenderer();
        renderer.Open(ContextMenuFactory.CreateDefault(MakeCtx()), 790, 590, new EditorRect(0, 0, 800, 600));

        Assert.Equal(800 - renderer.MinWidth, renderer.X);
        Assert.Equal(600 - renderer.Bounds.Height, renderer.Y);
    }

    // ── ContextMenuRenderer – keyboard ───────────────────────────────────────

    [Fact]
    public void Renderer_HandleKey_DownArrow_SkipsDisabledItems()
    {
        var (renderer, def) = OpenAndRender(MakeCtx()); // Cut and Copy disabled

        renderer.HandleKey(ConsoleKey.DownArrow);

        Assert.Equal("Paste", def.VisibleItems.ElementAt(renderer.FocusedIndex).Label);
    }

    [Fact]
    public void Renderer_HandleKey_UpArrow_FromNoFocus_WrapsToLastEnabledItem()
    {
        var (renderer, def) = OpenAndRender(MakeCtx());

        renderer.HandleKey(ConsoleKey.UpArrow);

        Assert.Equal(def.VisibleItems.Count() - 1, renderer.FocusedIndex);
        renderer.HandleKey(ConsoleKey.DownArrow); // wraps past disabled Cut and Copy
        Assert.Equal("Paste", def.VisibleItems.ElementAt(renderer.FocusedIndex).Label);
    }

    [Fact]
    public void Renderer_HandleKey_AllItemsDisabled_KeepsNoFocus()
    {
        var def = new ContextMenuDefinition();
        def.Items.Add(new ContextMenuItem { Label = "A", IsEnabled = false });
        def.Items.Add(new ContextMenuItem { Label = "B", IsEnabled = false });
        var renderer = new ContextMenuRenderer();
        renderer.Open(def, 0, 0, new EditorRect(0, 0, 800, 600));

        renderer.HandleKey(ConsoleKey.DownArrow);

        Assert.Equal(-1, renderer.FocusedIndex);
        Assert.Null(renderer.HandleKey(ConsoleKey.Enter));
    }

    [Fact]
    public void Renderer_HandleKey_EnterWithoutFocus_ReturnsNullAndStaysOpen()
    {
        var (renderer, _) = OpenAndRender(MakeCtx());

        Assert.Null(renderer.HandleKey(ConsoleKey.Enter));
        Assert.True(renderer.IsOpen);
    }

    // ── ContextMenuRenderer – theming ────────────────────────────────────────

    [Fact]
    public void Renderer_ApplyStyle_UsesThemeContextMenuColours()
    {
        var theme    = EditorTheme.CreateDark();
        var renderer = new ContextMenuRenderer();

        renderer.ApplyStyle(theme);

        Assert.Equal(theme.ContextMenuBackground, renderer.BackgroundColor);
        Assert.Equal(theme.ContextMenuTextColor, renderer.TextColor);
        Assert.Equal(theme.ContextMenuHoverColor, renderer.HoverColor);
        Assert.Equal(theme.ToolbarSeparatorColor, renderer.BorderColor);
        Assert.Equal(theme.ToolbarSeparatorColor, renderer.SeparatorColor);
        Assert.NotEqual(renderer.TextColor, renderer.DisabledTextColor);
        Assert.NotEqual(renderer.BackgroundColor, renderer.DisabledTextColor);
    }

    [Fact]
    public void Renderer_Render_DrawsWithConfiguredColours()
    {
        var theme    = EditorTheme.CreateDark();
        var def      = ContextMenuFactory.CreateDefault(MakeCtx()); // Cut disabled
        var renderer = new ContextMenuRenderer();
        renderer.ApplyStyle(theme);
        renderer.Open(def, 0, 0, new EditorRect(0, 0, 800, 600));
        renderer.HandleKey(ConsoleKey.DownArrow); // focus Paste

        var mock = new MockDrawingContext();
        renderer.Render(mock);

        Assert.Contains(mock.FillRoundRectCalls, c => c.Paint.Color == theme.ContextMenuBackground);
        Assert.Contains(mock.FillRoundRectCalls, c => c.Paint.Color == theme.ContextMenuHoverColor);
        Assert.Equal(renderer.DisabledTextColor, mock.DrawTextInRectCalls.First(c => c.Text == "Cut").Paint.Color);
        Assert.Equal(theme.ContextMenuTextColor, mock.DrawTextInRectCalls.First(c => c.Text == "Paste").Paint.Color);
    }

    // ── ContextMenuRenderer – size and scrolling ─────────────────────────────

    private static ContextMenuDefinition MakeItems(int count, string prefix = "Item ")
    {
        var def = new ContextMenuDefinition();
        for (int i = 0; i < count; i++)
            def.Items.Add(new ContextMenuItem { Label = prefix + i });
        return def;
    }

    [Fact]
    public void Renderer_Open_TallerThanViewport_ShrinksToViewportAndScrolls()
    {
        var renderer = new ContextMenuRenderer();
        var def      = MakeItems(10); // 4 + 10 * 28 + 4 = 288 tall
        renderer.Open(def, 50, 60, new EditorRect(0, 0, 400, 100));

        Assert.Equal(0, renderer.Y);
        Assert.Equal(100, renderer.Bounds.Height);
        Assert.Equal(188, renderer.MaxScrollOffset);
        Assert.Equal(0, renderer.ScrollOffset);
    }

    [Fact]
    public void Renderer_Open_FitsViewport_DoesNotScroll()
    {
        var renderer = new ContextMenuRenderer();
        renderer.Open(MakeItems(3), 0, 0, new EditorRect(0, 0, 400, 400));

        Assert.Equal(0, renderer.MaxScrollOffset);
        Assert.False(renderer.ScrollBy(50));
        Assert.Equal(0, renderer.ScrollOffset);
    }

    [Fact]
    public void Renderer_ScrollBy_ClampsToRange()
    {
        var renderer = new ContextMenuRenderer();
        renderer.Open(MakeItems(10), 0, 0, new EditorRect(0, 0, 400, 100));

        Assert.True(renderer.ScrollBy(1000));
        Assert.Equal(renderer.MaxScrollOffset, renderer.ScrollOffset);
        Assert.True(renderer.ScrollBy(-1000));
        Assert.Equal(0, renderer.ScrollOffset);
        Assert.False(renderer.ScrollBy(-1));
    }

    [Fact]
    public void Renderer_HitTest_UsesScrollOffset_AndIgnoresItemsScrolledOut()
    {
        var renderer = new ContextMenuRenderer();
        renderer.Open(MakeItems(10), 0, 0, new EditorRect(0, 0, 400, 100));
        renderer.Render(new MockDrawingContext());

        renderer.ScrollBy(renderer.MaxScrollOffset); // last item at the bottom
        float lastMidY = 100 - renderer.Padding - renderer.ItemHeight / 2f;
        Assert.Equal("Item 9", renderer.HitTest(50, lastMidY)?.Label);
        Assert.Equal(9, renderer.FocusedIndex);

        // Item 0 is scrolled out: its old position now shows a later item, and above the popup
        // nothing is hit.
        Assert.NotEqual("Item 0", renderer.HitTest(50, renderer.Padding + 1)?.Label);
        Assert.Null(renderer.HitTest(50, -5));
        Assert.Equal(-1, renderer.FocusedIndex);
    }

    [Fact]
    public void Renderer_HitTest_BelowViewport_DoesNotHitClippedItems()
    {
        var renderer = new ContextMenuRenderer();
        renderer.Open(MakeItems(10), 0, 0, new EditorRect(0, 0, 400, 100));
        renderer.Render(new MockDrawingContext());

        // Item 4 starts at y = 116, below the 100-tall popup, until the menu is scrolled.
        Assert.Null(renderer.HitTest(50, 120));
        Assert.True(renderer.IsClickOutside(50, 120));
    }

    [Fact]
    public void Renderer_HandleKey_ScrollsFocusedItemIntoView()
    {
        var renderer = new ContextMenuRenderer();
        renderer.Open(MakeItems(10), 0, 0, new EditorRect(0, 0, 400, 100));

        renderer.HandleKey(ConsoleKey.UpArrow); // wraps to the last item
        Assert.Equal(9, renderer.FocusedIndex);
        Assert.Equal(renderer.MaxScrollOffset, renderer.ScrollOffset);

        renderer.HandleKey(ConsoleKey.DownArrow); // wraps to the first item
        Assert.Equal(0, renderer.FocusedIndex);
        Assert.Equal(0, renderer.ScrollOffset);

        for (int i = 0; i < 4; i++)
            renderer.HandleKey(ConsoleKey.DownArrow);
        // Item 4 spans 116..144 in the content; its bottom plus padding must be visible.
        Assert.Equal(4, renderer.FocusedIndex);
        Assert.Equal(4 + 5 * renderer.ItemHeight + 4 - 100, renderer.ScrollOffset);
    }

    [Fact]
    public void Renderer_Render_ClipsAndOffsetsItemsByScroll()
    {
        var renderer = new ContextMenuRenderer();
        renderer.Open(MakeItems(10), 0, 0, new EditorRect(0, 0, 400, 100));
        renderer.ScrollBy(40);

        var mock = new MockDrawingContext();
        renderer.Render(mock);

        Assert.Equal(
            new[] { "Save", "ClipRoundRect(0,0,180,100,6)", "Translate(0,-40)", "Restore" },
            mock.TransformCalls);
    }

    [Fact]
    public void Renderer_Open_WithMeasureContext_WidensToLongestLabel()
    {
        var renderer = new ContextMenuRenderer();
        var def      = MakeItems(2);
        def.Items.Add(new ContextMenuItem { Label = new string('x', 40) }); // 320 wide at 8 per char

        renderer.Open(def, 0, 0, new EditorRect(0, 0, 800, 600), new MockDrawingContext());

        float expected = renderer.Padding * 2 + renderer.IconWidth + 4 + 320 + renderer.TrailingPadding;
        Assert.Equal(expected, renderer.Bounds.Width);
    }

    [Fact]
    public void Renderer_Open_WithMeasureContext_ShortLabels_KeepsMinWidth()
    {
        var renderer = new ContextMenuRenderer();
        renderer.Open(MakeItems(2), 0, 0, new EditorRect(0, 0, 800, 600), new MockDrawingContext());

        Assert.Equal(renderer.MinWidth, renderer.Bounds.Width);
    }

    [Fact]
    public void Renderer_Open_WiderThanViewport_ClampsWidthAndPosition()
    {
        var renderer = new ContextMenuRenderer();
        var def      = MakeItems(1, new string('x', 100)); // 800+ wide

        renderer.Open(def, 120, 0, new EditorRect(0, 0, 300, 600), new MockDrawingContext());

        Assert.Equal(0, renderer.X);
        Assert.Equal(300, renderer.Bounds.Width);
    }

    [Fact]
    public void Renderer_Close_ResetsScroll()
    {
        var renderer = new ContextMenuRenderer();
        renderer.Open(MakeItems(10), 0, 0, new EditorRect(0, 0, 400, 100));
        renderer.ScrollBy(50);

        renderer.Close();

        Assert.Equal(0, renderer.ScrollOffset);
        Assert.Equal(0, renderer.MaxScrollOffset);
    }
}
