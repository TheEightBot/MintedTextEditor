using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Rendering;
using MintedTextEditor.Core.Toolbar;

namespace MintedTextEditor.Core.Tests;

public class ToolbarTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static EditorDocument MakeDoc()
    {
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun("Hello world");
        return doc;
    }

    private static EditorContext MakeCtx(EditorDocument? doc = null)
    {
        doc ??= MakeDoc();
        return new EditorContext(doc, new Selection(), new UndoManager(),
            new FormattingEngine(), new FontFormattingEngine());
    }

    // ── ToolbarDefinition ────────────────────────────────────────────────────

    [Fact]
    public void DefaultToolbar_HasAtLeastEightGroups()
    {
        var toolbar = ToolbarDefinition.CreateDefault();
        Assert.True(toolbar.Groups.Count >= 8,
            $"Expected >= 8 groups, got {toolbar.Groups.Count}");
    }

    [Fact]
    public void DefaultToolbar_ContainsBoldToggleButton()
    {
        var toolbar = ToolbarDefinition.CreateDefault();
        var btn = toolbar.AllItems.OfType<ToolbarButton>()
                         .FirstOrDefault(b => b.Label == "Bold");

        Assert.NotNull(btn);
        Assert.IsType<ToggleBoldCommand>(btn.Command);
        Assert.True(btn.IsToggle);
    }

    [Fact]
    public void DefaultToolbar_ContainsFontFamilyDropdown()
    {
        var toolbar = ToolbarDefinition.CreateDefault();
        var dd = toolbar.AllItems.OfType<ToolbarDropdown>()
                        .FirstOrDefault(d => d.Label == "Font family");
        Assert.NotNull(dd);
    }

    [Fact]
    public void DefaultToolbar_ContainsColorPickers()
    {
        var toolbar = ToolbarDefinition.CreateDefault();
        var pickers = toolbar.AllItems.OfType<ToolbarColorPicker>().ToList();
        Assert.True(pickers.Count >= 2, $"Expected >= 2 color pickers, got {pickers.Count}");
    }

    [Fact]
    public void DefaultToolbar_HasAtLeastFifteenButtons()
    {
        var toolbar = ToolbarDefinition.CreateDefault();
        int count = toolbar.AllItems.OfType<ToolbarButton>().Count();
        Assert.True(count >= 15, $"Expected >= 15 buttons, got {count}");
    }

    [Fact]
    public void AllItems_EnumeratesAllGroupItems()
    {
        var toolbar = new ToolbarDefinition();
        toolbar.Groups.Add(new ToolbarGroup { Name = "A", Items = { new ToolbarButton { Label = "X" } } });
        toolbar.Groups.Add(new ToolbarGroup { Name = "B", Items = { new ToolbarButton { Label = "Y" }, new ToolbarSeparator() } });

        var all = toolbar.AllItems.ToList();
        Assert.Equal(3, all.Count);
    }

    // ── ToolbarButton ────────────────────────────────────────────────────────

    [Fact]
    public void ToolbarButton_Execute_RunsCommand_WithoutThrowing()
    {
        var ctx = MakeCtx();
        var btn = new ToolbarButton
        {
            Label    = "Bold",
            Command  = new ToggleBoldCommand(),
            IsToggle = true,
        };

        Assert.True(btn.Command.CanExecute(ctx));
        btn.Command.Execute(ctx); // no selection — no-op, must not throw
    }

    [Fact]
    public void ToolbarButton_CanExecute_ReflectsCommandState()
    {
        var ctx = MakeCtx();

        var undoBtn = new ToolbarButton { Label = "Undo", Command = new UndoCommand() };
        // Fresh document with nothing undoable
        Assert.False(undoBtn.Command!.CanExecute(ctx));
    }

    // ── ToolbarSeparator ─────────────────────────────────────────────────────

    [Fact]
    public void ToolbarSeparator_IsDistinctFromButton()
    {
        var sep = new ToolbarSeparator();
        Assert.IsType<ToolbarSeparator>(sep);
        Assert.IsNotType<ToolbarButton>(sep);
    }

    // ── ToolbarDropdown ──────────────────────────────────────────────────────

    [Fact]
    public void ToolbarDropdown_SelectionChanged_InvokesCallback()
    {
        int received = -1;
        var dd = new ToolbarDropdown
        {
            Label              = "Font Size",
            Items              = new List<string> { "10", "12", "14", "16" },
            OnSelectionChanged = i => received = i,
        };

        dd.OnSelectionChanged?.Invoke(2);
        Assert.Equal(2, received);
    }

    // ── ToolbarColorPicker ───────────────────────────────────────────────────

    [Fact]
    public void ToolbarColorPicker_ColorSelected_InvokesCallback()
    {
        EditorColor? selected = null;
        var cp = new ToolbarColorPicker { OnColorSelected = c => selected = c };

        cp.OnColorSelected?.Invoke(EditorColor.Red);
        Assert.Equal(EditorColor.Red, selected);
    }

    [Fact]
    public void ToolbarColorPicker_HasDefaultColors_IncludingBlackAndWhite()
    {
        var cp = new ToolbarColorPicker();
        Assert.True(cp.Colors.Count >= 5, $"Expected >= 5 colors, got {cp.Colors.Count}");
        Assert.Contains(EditorColor.Black, cp.Colors);
        Assert.Contains(EditorColor.White, cp.Colors);
    }

    // ── ToolbarRenderer ──────────────────────────────────────────────────────

    [Fact]
    public void ToolbarRenderer_HitTest_ReturnsNull_BeforeRender()
    {
        var toolbar  = ToolbarDefinition.CreateDefault();
        var renderer = new ToolbarRenderer(toolbar);
        Assert.Null(renderer.HitTest(50f, 10f));
    }

    [Fact]
    public void ToolbarRenderer_DefaultIconPack_IsLucide()
    {
        var toolbar = ToolbarDefinition.CreateDefault();
        var renderer = new ToolbarRenderer(toolbar);

        Assert.Equal(ToolbarIconPack.Lucide, renderer.IconPack);
    }

    [Fact]
    public void ToolbarRenderer_IconPack_CanBeChanged()
    {
        var toolbar = ToolbarDefinition.CreateDefault();
        var renderer = new ToolbarRenderer(toolbar)
        {
            IconPack = ToolbarIconPack.MaterialSymbols,
        };

        Assert.Equal(ToolbarIconPack.MaterialSymbols, renderer.IconPack);
    }

    [Fact]
    public void ToolbarRenderer_UpdateToggleStates_SetsBoldActive_WhenSelectionIsBold()
    {
        var doc = MakeDoc();
        var ctx = MakeCtx(doc);

        // Apply bold to the entire paragraph text
        var range = new TextRange(
            new DocumentPosition(0, 0, 0),
            new DocumentPosition(0, 0, 11));
        ctx.FormattingEngine.ToggleBold(doc, range);

        // Extend selection over the bold range
        ctx.Selection.Set(
            new DocumentPosition(0, 0, 0),
            new DocumentPosition(0, 0, 11));

        var toolbar  = ToolbarDefinition.CreateDefault();
        var renderer = new ToolbarRenderer(toolbar);
        renderer.UpdateToggleStates(ctx);

        var boldBtn = toolbar.AllItems
            .OfType<ToolbarButton>()
            .First(b => b.Label == "Bold");

        Assert.True(boldBtn.IsActive);
    }

    [Fact]
    public void ToolbarRenderer_UpdateToggleStates_LeavesBoldInactive_WhenNotBold()
    {
        var ctx      = MakeCtx();          // no formatting applied
        var toolbar  = ToolbarDefinition.CreateDefault();
        var renderer = new ToolbarRenderer(toolbar);
        renderer.UpdateToggleStates(ctx);

        var boldBtn = toolbar.AllItems
            .OfType<ToolbarButton>()
            .First(b => b.Label == "Bold");

        Assert.False(boldBtn.IsActive);
    }

    // ── Custom definition ────────────────────────────────────────────────────

    [Fact]
    public void CustomToolbarDefinition_CanAddItemsToGroups()
    {
        var toolbar = new ToolbarDefinition();
        var group   = new ToolbarGroup { Name = "Custom" };
        group.Items.Add(new ToolbarButton { Label = "My Button", Command = new ClearFormattingCommand() });
        group.Items.Add(new ToolbarSeparator());
        toolbar.Groups.Add(group);

        Assert.Single(toolbar.Groups);
        Assert.Equal(2, toolbar.Groups[0].Items.Count);
    }

    [Fact]
    public void ToolbarDefinition_DefaultLayoutMode_IsWrap()
    {
        var toolbar = ToolbarDefinition.CreateDefault();
        Assert.Equal(ToolbarLayoutMode.Wrap, toolbar.LayoutMode);
    }

    [Fact]
    public void ToolbarDefinition_CreateFromItems_BuildsSingleCustomGroup()
    {
        var items = new List<ToolbarItem>
        {
            new ToolbarButton { Label = "A" },
            new ToolbarSeparator(),
            new ToolbarButton { Label = "B" },
        };

        var toolbar = ToolbarDefinition.CreateFromItems(items);

        Assert.Single(toolbar.Groups);
        Assert.Equal("Custom", toolbar.Groups[0].Name);
        Assert.Equal(3, toolbar.Groups[0].Items.Count);
        Assert.Equal(3, toolbar.AllItems.Count());
    }

    [Fact]
    public void ToolbarRenderer_WrapMode_GrowsToolbarHeight_WhenWidthIsSmall()
    {
        var toolbar = ToolbarDefinition.CreateFromItems(new List<ToolbarItem>
        {
            new ToolbarButton { Label = "A", Icon = "bold" },
            new ToolbarButton { Label = "B", Icon = "italic" },
            new ToolbarButton { Label = "C", Icon = "underline" },
            new ToolbarButton { Label = "D", Icon = "strikethrough" },
            new ToolbarButton { Label = "E", Icon = "table" },
        });
        toolbar.LayoutMode = ToolbarLayoutMode.Wrap;

        var renderer = new ToolbarRenderer(toolbar)
        {
            ButtonSize = 36f,
            ButtonPadding = 5f,
            RowSpacing = 6f,
        };

        var dc = new MockDrawingContext();
        renderer.Render(dc, new EditorRect(0, 0, 110, 200));

        Assert.True(renderer.ToolbarHeight > (renderer.ButtonSize + renderer.ButtonPadding * 2));
    }

    [Fact]
    public void ToolbarRenderer_WrapMode_HitTestFindsButtonOnSecondRow()
    {
        var toolbar = ToolbarDefinition.CreateFromItems(new List<ToolbarItem>
        {
            new ToolbarButton { Label = "One", Icon = "bold" },
            new ToolbarButton { Label = "Two", Icon = "italic" },
            new ToolbarButton { Label = "Three", Icon = "underline" },
            new ToolbarButton { Label = "Four", Icon = "strikethrough" },
        });
        toolbar.LayoutMode = ToolbarLayoutMode.Wrap;

        var renderer = new ToolbarRenderer(toolbar)
        {
            ButtonSize = 36f,
            ButtonPadding = 5f,
            RowSpacing = 6f,
        };

        var dc = new MockDrawingContext();
        renderer.Render(dc, new EditorRect(0, 0, 90, 200));

        var secondRowHit = renderer.HitTest(20f, 50f);
        Assert.NotNull(secondRowHit);
        Assert.IsType<ToolbarButton>(secondRowHit);
    }

    // ── Hover ────────────────────────────────────────────────────────────────

    private static (ToolbarRenderer Renderer, ToolbarButton Plain, ToolbarButton Active, ToolbarButton Disabled, ToolbarDropdown Dropdown)
        MakeHoverToolbar()
    {
        var plain = new ToolbarButton { Label = "B", Icon = "bold" };
        var active = new ToolbarButton { Label = "I", Icon = "italic", IsActive = true };
        var disabled = new ToolbarButton { Label = "U", Icon = "underline", IsEnabled = false };
        var dropdown = new ToolbarDropdown { Label = "Font", Items = new List<string> { "A" } };
        var renderer = new ToolbarRenderer(ToolbarDefinition.CreateFromItems(
            new List<ToolbarItem> { plain, active, disabled, dropdown }))
        {
            ButtonSize = 36f,
            ButtonPadding = 5f,
            HoverColor = new EditorColor(1, 2, 3),
            ActiveColor = new EditorColor(4, 5, 6),
        };
        return (renderer, plain, active, disabled, dropdown);
    }

    // Items sit in one row: buttons at x = 5, 46, 87 (36 wide), the drop-down at x = 128.
    private const float RowY = 20f;

    [Fact]
    public void ToolbarRenderer_UpdateHover_TracksItemUnderPointer()
    {
        var (renderer, plain, _, _, _) = MakeHoverToolbar();
        renderer.Render(new MockDrawingContext(), new EditorRect(0, 0, 400, 100));

        Assert.True(renderer.UpdateHover(20f, RowY));
        Assert.Same(plain, renderer.HoveredItem);
        Assert.False(renderer.UpdateHover(25f, RowY)); // same item: no redraw needed

        Assert.True(renderer.UpdateHover(300f, RowY)); // empty toolbar space
        Assert.Null(renderer.HoveredItem);
        Assert.False(renderer.ClearHover());
    }

    [Fact]
    public void ToolbarRenderer_HoveredButton_IsFilledWithHoverColor()
    {
        var (renderer, _, _, _, _) = MakeHoverToolbar();
        var ctx = new MockDrawingContext();
        renderer.Render(ctx, new EditorRect(0, 0, 400, 100));
        Assert.DoesNotContain(ctx.FillRoundRectCalls, c => c.Paint.Color == renderer.HoverColor);

        renderer.UpdateHover(20f, RowY);
        ctx = new MockDrawingContext();
        renderer.Render(ctx, new EditorRect(0, 0, 400, 100));

        var fill = Assert.Single(ctx.FillRoundRectCalls, c => c.Paint.Color == renderer.HoverColor);
        Assert.Equal(5f, fill.Rect.X);

        Assert.True(renderer.ClearHover());
        ctx = new MockDrawingContext();
        renderer.Render(ctx, new EditorRect(0, 0, 400, 100));
        Assert.DoesNotContain(ctx.FillRoundRectCalls, c => c.Paint.Color == renderer.HoverColor);
    }

    [Fact]
    public void ToolbarRenderer_HoveredDropdown_IsFilledWithHoverColor()
    {
        var (renderer, _, _, _, dropdown) = MakeHoverToolbar();
        renderer.Render(new MockDrawingContext(), new EditorRect(0, 0, 400, 100));

        Assert.True(renderer.UpdateHover(150f, RowY));
        Assert.Same(dropdown, renderer.HoveredItem);

        var ctx = new MockDrawingContext();
        renderer.Render(ctx, new EditorRect(0, 0, 400, 100));
        var fill = Assert.Single(ctx.FillRoundRectCalls, c => c.Paint.Color == renderer.HoverColor);
        Assert.Equal(128f, fill.Rect.X);
    }

    [Fact]
    public void ToolbarRenderer_ActiveButton_KeepsActiveColorWhenHovered()
    {
        var (renderer, _, active, _, _) = MakeHoverToolbar();
        renderer.Render(new MockDrawingContext(), new EditorRect(0, 0, 400, 100));

        renderer.UpdateHover(60f, RowY);
        Assert.Same(active, renderer.HoveredItem);

        var ctx = new MockDrawingContext();
        renderer.Render(ctx, new EditorRect(0, 0, 400, 100));
        Assert.DoesNotContain(ctx.FillRoundRectCalls, c => c.Paint.Color == renderer.HoverColor);
        Assert.Contains(ctx.FillRoundRectCalls, c => c.Paint.Color == renderer.ActiveColor && c.Rect.X == 46f);
    }

    [Fact]
    public void ToolbarRenderer_DisabledButton_IsNotHovered()
    {
        var (renderer, _, _, _, _) = MakeHoverToolbar();
        renderer.Render(new MockDrawingContext(), new EditorRect(0, 0, 400, 100));

        Assert.False(renderer.UpdateHover(100f, RowY));
        Assert.Null(renderer.HoveredItem);
    }

    [Fact]
    public void ToolbarRenderer_HoveredButtonDisabledLater_IsNotDrawnHovered()
    {
        var (renderer, plain, _, _, _) = MakeHoverToolbar();
        renderer.Render(new MockDrawingContext(), new EditorRect(0, 0, 400, 100));
        Assert.True(renderer.UpdateHover(20f, RowY));
        Assert.Same(plain, renderer.HoveredItem);

        // e.g. UpdateToggleStates disables the button while the pointer rests on it.
        plain.IsEnabled = false;
        var ctx = new MockDrawingContext();
        renderer.Render(ctx, new EditorRect(0, 0, 400, 100));

        Assert.Null(renderer.HoveredItem);
        Assert.DoesNotContain(ctx.FillRoundRectCalls, c => c.Paint.Color == renderer.HoverColor);
    }

    [Fact]
    public void ToolbarRenderer_HoveredOverflowItemDisabledLater_IsNotDrawnHovered()
    {
        var items = new List<ToolbarItem>();
        for (int i = 0; i < 8; i++)
            items.Add(new ToolbarButton { Label = "B" + i, Icon = "bold" });
        var def = ToolbarDefinition.CreateFromItems(items);
        def.MaxRows = 1;
        var renderer = new ToolbarRenderer(def)
        {
            ButtonSize = 36f,
            ButtonPadding = 5f,
            HoverColor = new EditorColor(1, 2, 3),
        };
        var bounds = new EditorRect(0, 0, 120, 100);
        renderer.Render(new MockDrawingContext(), bounds);
        Assert.True(renderer.HasOverflow);

        bool opened = false;
        for (float x = 0; x < bounds.Width && !opened; x += 2f)
            opened = renderer.HandleOverflowButtonTap(x, RowY);
        Assert.True(opened);
        Assert.True(renderer.IsOverflowPanelOpen);
        renderer.RenderOverflowPanel(new MockDrawingContext(), bounds.Width, 400f);

        // Find an overflow item under the pointer.
        for (float y = 50f; y < 400f && renderer.HoveredItem is null; y += 4f)
            renderer.UpdateHover(20f, y);
        var hovered = Assert.IsType<ToolbarButton>(renderer.HoveredItem);
        Assert.Contains(hovered, renderer.OverflowItems);

        var ctx = new MockDrawingContext();
        renderer.RenderOverflowPanel(ctx, bounds.Width, 400f);
        Assert.Contains(ctx.FillRoundRectCalls, c => c.Paint.Color == renderer.HoverColor);

        hovered.IsEnabled = false;
        ctx = new MockDrawingContext();
        renderer.RenderOverflowPanel(ctx, bounds.Width, 400f);

        Assert.Null(renderer.HoveredItem);
        Assert.DoesNotContain(ctx.FillRoundRectCalls, c => c.Paint.Color == renderer.HoverColor);
    }

    [Fact]
    public void DropdownList_WidensToFitLongestOption()
    {
        const string longOption = "Insertar columna a la derecha de la celda";
        var dd = new ToolbarDropdown { Label = "Table", Items = { "Short", longOption } };
        var renderer = new ToolbarRenderer(ToolbarDefinition.CreateFromItems(new List<ToolbarItem> { dd }));

        var dc = new MockDrawingContext();
        var bounds = new EditorRect(0, 0, 800, 600);
        renderer.Render(dc, bounds);
        renderer.ToggleOverlay(dd);
        dc.DrawTextInRectCalls.Clear();
        renderer.RenderOverlay(dc, bounds.Width, bounds.Height);

        var drawn = dc.DrawTextInRectCalls.Single(c => c.Text == longOption);
        Assert.True(drawn.Rect.Width >= dc.MeasureText(longOption, new EditorPaint()).Width,
            $"Option rect {drawn.Rect.Width}px is narrower than its text");
    }

    // ── MaxRowsOverride ──────────────────────────────────────────────────────

    private static readonly EditorRect NarrowToolbarBounds = new(0, 0, 100, 400);

    private static ToolbarRenderer RenderNarrow(ToolbarDefinition def, int? maxRowsOverride)
    {
        var renderer = new ToolbarRenderer(def) { MaxRowsOverride = maxRowsOverride };
        renderer.Render(new MockDrawingContext(), NarrowToolbarBounds);
        return renderer;
    }

    [Fact]
    public void MaxRowsOverride_DefaultsToNull_AndUsesDefinitionMaxRows()
    {
        var def = ToolbarDefinition.CreateDefault();
        def.MaxRows = 1;
        var renderer = new ToolbarRenderer(def);

        Assert.Null(renderer.MaxRowsOverride);
        Assert.Equal(1, renderer.EffectiveMaxRows);
    }

    [Fact]
    public void MaxRowsOverride_LimitsRows_WhenDefinitionIsUnlimited()
    {
        var def = ToolbarDefinition.CreateDefault(); // MaxRows = 0

        Assert.False(RenderNarrow(def, null).HasOverflow);
        Assert.True(RenderNarrow(def, 1).HasOverflow);
    }

    [Fact]
    public void MaxRowsOverride_Zero_RemovesDefinitionLimit()
    {
        var def = ToolbarDefinition.CreateDefault();
        def.MaxRows = 1;

        Assert.True(RenderNarrow(def, null).HasOverflow);
        Assert.False(RenderNarrow(def, 0).HasOverflow);
    }

    [Fact]
    public void MaxRowsOverride_DoesNotMutateDefinition()
    {
        var def = ToolbarDefinition.CreateDefault();
        def.MaxRows = 3;

        var renderer = RenderNarrow(def, 1);

        Assert.Equal(3, def.MaxRows);
        Assert.Equal(1, renderer.EffectiveMaxRows);
    }

    [Fact]
    public void MaxRowsOverride_Negative_IsUnlimited()
    {
        var renderer = new ToolbarRenderer(ToolbarDefinition.CreateDefault()) { MaxRowsOverride = -2 };
        Assert.Equal(0, renderer.EffectiveMaxRows);
    }

    [Fact]
    public void RowLimit_StoredOnDefinition_IsLostWhenToolbarIsRebuilt()
    {
        // Reproduces the old MintedEditorView behaviour: ToolbarMaxRows was written to the
        // current renderer's Definition, so a rebuilt toolbar came back unlimited.
        var first = new ToolbarRenderer(ToolbarDefinition.CreateDefault());
        first.Definition.MaxRows = 1;
        first.Render(new MockDrawingContext(), NarrowToolbarBounds);
        Assert.True(first.HasOverflow);

        var rebuilt = new ToolbarRenderer(ToolbarDefinition.CreateDefault());
        rebuilt.Render(new MockDrawingContext(), NarrowToolbarBounds);
        Assert.False(rebuilt.HasOverflow);
    }

    [Fact]
    public void RowLimit_CarriedAsOverride_SurvivesToolbarRebuild()
    {
        // The host's limit lives outside the definition, so it is re-applied to each new renderer
        // regardless of what the new definition (or flat item list) specifies.
        const int hostMaxRows = 1;

        var fromDefault = RenderNarrow(ToolbarDefinition.CreateDefault(), hostMaxRows);
        var fromCustom  = RenderNarrow(
            new ToolbarDefinition { Groups = ToolbarDefinition.CreateDefault().Groups, MaxRows = 0 }, hostMaxRows);
        var fromItems   = RenderNarrow(
            ToolbarDefinition.CreateFromItems(ToolbarDefinition.CreateDefault().AllItems.ToList()), hostMaxRows);

        Assert.True(fromDefault.HasOverflow);
        Assert.True(fromCustom.HasOverflow);
        Assert.True(fromItems.HasOverflow);
    }
}
