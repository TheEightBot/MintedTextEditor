using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.ContextMenu;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;
using MintedTextEditor.Core.Localization;
using MintedTextEditor.Core.Rendering;
using MintedTextEditor.Core.Toolbar;

namespace MintedTextEditor.Core.Tests;

/// <summary>
/// Tests that replace <see cref="EditorStrings.Current"/>. They run in a non-parallel collection so
/// other tests that read the default English labels never observe the swapped strings.
/// </summary>
[CollectionDefinition(nameof(EditorStringsCollection), DisableParallelization = true)]
public sealed class EditorStringsCollection { }

[Collection(nameof(EditorStringsCollection))]
public class LocalizedUiStringsTests
{
    private sealed class SpanishStrings : EditorStrings
    {
        public override string Bold       => "Negrita";
        public override string FontFamily => "Fuente";
        public override string InsertLink => "Insertar hipervínculo";
        public override string Cut        => "Cortar";
        public override string SelectAll  => "Seleccionar todo";
        public override string InsertTable => "Insertar tabla";
        public override string DeleteRow  => "Eliminar fila";
    }

    private sealed class FrenchStrings : EditorStrings
    {
        public override string Bold => "Gras";
        public override string Cut  => "Couper";
    }

    private static void WithStrings(EditorStrings strings, Action test)
    {
        var original = EditorStrings.Current;
        try
        {
            EditorStrings.Current = strings;
            test();
        }
        finally
        {
            EditorStrings.Current = original;
        }
    }

    private static EditorContext MakeCtx(EditorDocument doc, Selection? sel = null)
        => new(doc, sel ?? new Selection(), new UndoManager(),
            new FormattingEngine(), new FontFormattingEngine());

    private static EditorDocument MakeDoc()
    {
        var doc = new EditorDocument();
        ((Paragraph)doc.Blocks[0]).AppendRun("Hello world");
        return doc;
    }

    private static ToolbarItem FindByIcon(ToolbarDefinition def, string icon)
        => def.AllItems.First(i => i.Icon == icon);

    // ── EditorStrings.Current ────────────────────────────────────────────────

    [Fact]
    public void Current_CanBeReplaced()
    {
        WithStrings(new SpanishStrings(), () =>
            Assert.Equal("Negrita", EditorStrings.Current.Bold));
    }

    [Fact]
    public void Current_RaisesCurrentChanged_OnlyWhenInstanceChanges()
    {
        int raised = 0;
        void Handler(object? s, EventArgs e) => raised++;
        var spanish = new SpanishStrings();

        EditorStrings.CurrentChanged += Handler;
        try
        {
            WithStrings(spanish, () =>
            {
                Assert.Equal(1, raised);
                EditorStrings.Current = spanish; // same instance: no event
                Assert.Equal(1, raised);
            });
            Assert.Equal(2, raised); // restored
        }
        finally
        {
            EditorStrings.CurrentChanged -= Handler;
        }
    }

    // ── Toolbar ──────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultToolbar_LabelsComeFromEditorStringsDefaults()
    {
        var def = ToolbarDefinition.CreateDefault();
        var s   = EditorStrings.Current;

        Assert.Equal(s.Bold,         FindByIcon(def, "bold").Label);
        Assert.Equal(s.FontFamily,   FindByIcon(def, "font-family").Label);
        Assert.Equal(s.NumberedList, FindByIcon(def, "number-list").Label);
        Assert.Equal(s.InsertLink,   FindByIcon(def, "hyperlink").Label);
        Assert.Equal(s.Highlight,    FindByIcon(def, "highlight-color").Label);
        Assert.Equal(s.HeadingLevel, FindByIcon(def, "heading").Label);
        Assert.Equal(s.ClearFormat,  FindByIcon(def, "clear-formatting").Label);
    }

    [Fact]
    public void ToolbarCreatedBeforeSwap_UsesNewStringsAfterSwap()
    {
        // Created with English strings; the swap must still take effect.
        var def = ToolbarDefinition.CreateDefault();

        WithStrings(new SpanishStrings(), () =>
        {
            Assert.Equal("Negrita",               FindByIcon(def, "bold").Label);
            Assert.Equal("Fuente",                FindByIcon(def, "font-family").Label);
            Assert.Equal("Insertar hipervínculo", FindByIcon(def, "hyperlink").Label);
        });

        Assert.Equal("Bold", FindByIcon(def, "bold").Label);
    }

    [Theory]
    [InlineData("minimal")]
    [InlineData("formatting")]
    [InlineData("insert")]
    public void PresetToolbars_UseCustomStrings(string preset)
    {
        WithStrings(new SpanishStrings(), () =>
        {
            var def = preset switch
            {
                "minimal"    => ToolbarDefinition.CreateMinimal(),
                "formatting" => ToolbarDefinition.CreateFormattingOnly(),
                _            => ToolbarDefinition.CreateInsertOnly(),
            };
            var item = preset == "insert" ? FindByIcon(def, "hyperlink") : FindByIcon(def, "bold");
            Assert.Equal(preset == "insert" ? "Insertar hipervínculo" : "Negrita", item.Label);
        });
    }

    [Fact]
    public void AssigningLabel_OverridesLocalizedLabel()
    {
        var def = ToolbarDefinition.CreateDefault();
        var bold = FindByIcon(def, "bold");
        bold.Label = "B";

        Assert.Null(bold.LocalizedLabel);
        WithStrings(new SpanishStrings(), () => Assert.Equal("B", bold.Label));
    }

    [Fact]
    public void CustomItem_CanOptIntoLocalizedLabel()
    {
        var btn = new ToolbarButton { LocalizedLabel = s => s.Bold };
        WithStrings(new FrenchStrings(), () => Assert.Equal("Gras", btn.Label));
    }

    [Fact]
    public void ToolbarRenderer_DrawsLabelResolvedAtDrawTime()
    {
        // A dropdown with nothing selected draws its label.
        var renderer = new ToolbarRenderer(ToolbarDefinition.CreateFormattingOnly());
        var bounds   = new EditorRect(0, 0, 2000, 200);

        var english = new MockDrawingContext();
        renderer.Render(english, bounds);
        Assert.Contains(english.DrawTextInRectCalls, c => c.Text == "Font family");

        WithStrings(new SpanishStrings(), () =>
        {
            var spanish = new MockDrawingContext();
            renderer.Render(spanish, bounds);
            Assert.Contains(spanish.DrawTextInRectCalls, c => c.Text == "Fuente");
            Assert.DoesNotContain(spanish.DrawTextInRectCalls, c => c.Text == "Font family");
        });
    }

    // ── Context menu ─────────────────────────────────────────────────────────

    [Fact]
    public void ContextMenu_UsesCurrentStrings()
    {
        var ctx = MakeCtx(MakeDoc());

        WithStrings(new SpanishStrings(), () =>
        {
            var def = ContextMenuFactory.CreateDefault(ctx);
            Assert.Contains(def.Items, i => i.Label == "Cortar");
            Assert.Contains(def.Items, i => i.Label == "Seleccionar todo");
            Assert.Contains(def.Items, i => i.Label == "Insertar hipervínculo");
            Assert.Contains(def.Items, i => i.Label == "Insertar tabla");
            Assert.Contains(def.Items, i => i.Label == "Copy"); // not overridden
        });
    }

    [Fact]
    public void ContextMenu_ExplicitStringsOverload_UsesGivenStrings()
    {
        var def = ContextMenuFactory.CreateDefault(MakeCtx(MakeDoc()), new FrenchStrings());
        Assert.Equal("Couper", def.Items[0].Label);
    }

    [Fact]
    public void ContextMenu_ExplicitStringsOverload_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ContextMenuFactory.CreateDefault(MakeCtx(MakeDoc()), null!));
    }

    [Fact]
    public void ContextMenu_TableItems_UseCurrentStrings()
    {
        var doc = new EditorDocument();
        doc.Blocks.Clear();
        doc.Blocks.Add(new TableBlock(2, 2));
        var sel = new Selection();
        sel.CollapseTo(new DocumentPosition(0, 0, 0, 0, 0));

        WithStrings(new SpanishStrings(), () =>
        {
            var def = ContextMenuFactory.CreateDefault(MakeCtx(doc, sel));
            Assert.Contains(def.Items, i => i.Label == "Eliminar fila");
        });
    }

    [Fact]
    public void ContextMenuRenderer_DrawsLocalizedLabels()
    {
        WithStrings(new FrenchStrings(), () =>
        {
            var menu = new ContextMenuRenderer();
            menu.Open(ContextMenuFactory.CreateDefault(MakeCtx(MakeDoc())), 0, 0, new EditorRect(0, 0, 800, 800));
            var dc = new MockDrawingContext();
            menu.Render(dc);
            Assert.Equal("Couper", dc.DrawTextInRectCalls[0].Text);
        });
    }
}
