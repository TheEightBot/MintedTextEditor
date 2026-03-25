using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.Toolbar;
using EditorToolbarItem = MintedTextEditor.Core.Toolbar.ToolbarItem;

namespace SampleApp.Maui.Pages;

public partial class DynamicToolbarPage : ContentPage
{
    // ── State ─────────────────────────────────────────────────────────────────

    // "default" | "formatting" | "minimal"
    private string _preset     = "default";
    private bool   _showInsert = true;
    private bool   _boldEnabled = true;
    private int    _maxRows    = 0;

    // ── Construction ─────────────────────────────────────────────────────────

    public DynamicToolbarPage()
    {
        InitializeComponent();
        Editor.LoadHtml(
            "<h2>Dynamic Toolbar Demo</h2>" +
            "<p>Use the controls above to reconfigure the toolbar <strong>at runtime</strong> " +
            "without reloading the editor or losing document content.</p>" +
            "<p>Try <em>swapping presets</em>, <strong>removing the Insert group</strong>, " +
            "and <u>limiting visible rows</u> to trigger the overflow (…) button.</p>" +
            "<p>All changes take effect immediately — the document and selection " +
            "are preserved across every toolbar change.</p>");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a fresh <see cref="ToolbarDefinition"/> from the current state flags
    /// and assigns it to the editor.  Because <see cref="ToolbarDefinition"/> uses
    /// reference equality in the bindable property, we always supply a new instance
    /// so the property-changed callback fires and the canvas is redrawn.
    /// </summary>
    private void RebuildToolbar()
    {
        ToolbarDefinition def = _preset switch
        {
            "formatting" => ToolbarDefinition.CreateFormattingOnly(),
            "minimal"    => BuildMinimalDefinition(),
            _            => ToolbarDefinition.CreateDefault(),
        };

        // Remove the Insert group if the user has hidden it.
        if (!_showInsert)
        {
            var without = def.Groups.Where(g => g.Name != "Insert").ToList();
            def.Groups.Clear();
            foreach (var g in without)
                def.Groups.Add(g);
        }

        // Disable the Bold button when requested.
        if (!_boldEnabled)
        {
            foreach (var btn in def.AllItems.OfType<ToolbarButton>()
                                            .Where(b => b.Label == "Bold"))
                btn.IsEnabled = false;
        }

        def.MaxRows = _maxRows;
        Editor.ToolbarDefinition = def;
    }

    /// <summary>Minimal preset: Undo/Redo + Bold/Italic/Underline only.</summary>
    private static ToolbarDefinition BuildMinimalDefinition()
    {
        var def = new ToolbarDefinition();

        def.Groups.Add(new ToolbarGroup
        {
            Name  = "History",
            Items = new List<EditorToolbarItem>
            {
                new ToolbarButton { Label = "Undo", Icon = "undo", Command = new UndoCommand() },
                new ToolbarButton { Label = "Redo", Icon = "redo", Command = new RedoCommand() },
            }
        });

        def.Groups.Add(new ToolbarGroup
        {
            Name  = "Formatting",
            Items = new List<EditorToolbarItem>
            {
                new ToolbarButton { Label = "Bold",      Icon = "bold",      Command = new ToggleBoldCommand(),      IsToggle = true },
                new ToolbarButton { Label = "Italic",    Icon = "italic",    Command = new ToggleItalicCommand(),    IsToggle = true },
                new ToolbarButton { Label = "Underline", Icon = "underline", Command = new ToggleUnderlineCommand(), IsToggle = true },
            }
        });

        return def;
    }

    // ── Preset buttons ────────────────────────────────────────────────────────

    private void OnPresetDefault(object sender, EventArgs e)
    {
        _preset = "default";
        HighlightPresetButton(BtnDefault);
        RebuildToolbar();
    }

    private void OnPresetFormatting(object sender, EventArgs e)
    {
        _preset = "formatting";
        HighlightPresetButton(BtnFormatting);
        RebuildToolbar();
    }

    private void OnPresetMinimal(object sender, EventArgs e)
    {
        _preset = "minimal";
        HighlightPresetButton(BtnMinimal);
        RebuildToolbar();
    }

    private void HighlightPresetButton(Button active)
    {
        Color activeColor  = Color.FromArgb("#2563EB");
        Color inactiveColor = Color.FromArgb("#9CA3AF");
        BtnDefault.BackgroundColor    = inactiveColor;
        BtnFormatting.BackgroundColor = inactiveColor;
        BtnMinimal.BackgroundColor    = inactiveColor;
        active.BackgroundColor        = activeColor;
    }

    // ── Runtime toggle buttons ────────────────────────────────────────────────

    private void OnToggleToolbar(object sender, EventArgs e)
    {
        Editor.ShowToolbar          = !Editor.ShowToolbar;
        BtnToggleToolbar.Text       = Editor.ShowToolbar ? "Hide Toolbar" : "Show Toolbar";
        BtnToggleToolbar.BackgroundColor = Editor.ShowToolbar
            ? Color.FromArgb("#7C3AED")
            : Color.FromArgb("#374151");
    }

    private void OnToggleInsert(object sender, EventArgs e)
    {
        _showInsert               = !_showInsert;
        BtnToggleInsert.Text      = _showInsert ? "Remove Insert" : "Add Insert";
        BtnToggleInsert.BackgroundColor = _showInsert
            ? Color.FromArgb("#0891B2")
            : Color.FromArgb("#374151");
        RebuildToolbar();
    }

    private void OnToggleBold(object sender, EventArgs e)
    {
        _boldEnabled             = !_boldEnabled;
        BtnToggleBold.Text       = _boldEnabled ? "Disable Bold" : "Enable Bold";
        BtnToggleBold.BackgroundColor = _boldEnabled
            ? Color.FromArgb("#DC2626")
            : Color.FromArgb("#374151");
        RebuildToolbar();
    }

    // ── Max rows buttons ──────────────────────────────────────────────────────

    private void OnRowsUnlimited(object sender, EventArgs e) => SetMaxRows(0);
    private void OnRows1(object sender, EventArgs e)         => SetMaxRows(1);
    private void OnRows2(object sender, EventArgs e)         => SetMaxRows(2);

    private void SetMaxRows(int rows)
    {
        _maxRows = rows;
        HighlightRowButton(rows);
        RebuildToolbar();
    }

    private void HighlightRowButton(int rows)
    {
        Color activeColor   = Color.FromArgb("#2563EB");
        Color inactiveColor = Color.FromArgb("#9CA3AF");
        BtnRowsAll.BackgroundColor = inactiveColor;
        BtnRows1.BackgroundColor   = inactiveColor;
        BtnRows2.BackgroundColor   = inactiveColor;
        (rows switch
        {
            1 => BtnRows1,
            2 => BtnRows2,
            _ => BtnRowsAll,
        }).BackgroundColor = activeColor;
    }
}
