using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Toolbar;

/// <summary>Layout behaviour when toolbar items overflow available width.</summary>
public enum ToolbarLayoutMode { Wrap, Scroll, Overflow }

// ── Base ─────────────────────────────────────────────────────────────────────

/// <summary>Abstract base for all toolbar elements.</summary>
public abstract class ToolbarItem
{
    public string Label     { get; set; } = "";
    public string Icon      { get; set; } = "";
    public bool   IsEnabled { get; set; } = true;
    public bool   IsVisible { get; set; } = true;
}

// ── Concrete items ────────────────────────────────────────────────────────────

/// <summary>A clickable button optionally associated with an <see cref="IEditorCommand"/>.</summary>
public sealed class ToolbarButton : ToolbarItem
{
    public IEditorCommand? Command  { get; set; }
    /// <summary>When true the button represents a toggle (e.g. Bold).</summary>
    public bool IsToggle { get; set; }
    /// <summary>Whether the toggle is currently in its 'active' (pressed) state.</summary>
    public bool IsActive { get; set; }
}

/// <summary>A visual divider between groups of toolbar items.</summary>
public sealed class ToolbarSeparator : ToolbarItem { }

/// <summary>A drop-down selector (font family, font size, heading level, …).</summary>
public sealed class ToolbarDropdown : ToolbarItem
{
    public IList<string> Items           { get; set; } = new List<string>();
    public int           SelectedIndex   { get; set; } = -1;
    public Action<int>?  OnSelectionChanged { get; set; }
}

/// <summary>A colour-swatch grid picker (text colour, highlight colour, …).</summary>
public sealed class ToolbarColorPicker : ToolbarItem
{
    public IList<EditorColor>   Colors          { get; set; } = CreateDefaultColors();
    public EditorColor          SelectedColor   { get; set; }
    public Action<EditorColor>? OnColorSelected { get; set; }

    private static List<EditorColor> CreateDefaultColors() => new()
    {
        EditorColor.Black,
        EditorColor.White,
        EditorColor.Red,
        EditorColor.FromRgb(0xFF, 0x99, 0x00), // orange
        EditorColor.Yellow,
        EditorColor.Green,
        EditorColor.Blue,
        EditorColor.FromRgb(0x80, 0x00, 0x80), // purple
        EditorColor.Gray,
        EditorColor.LightGray,
    };
}

// ── Group ─────────────────────────────────────────────────────────────────────

/// <summary>Logical grouping of toolbar items rendered together.</summary>
public sealed class ToolbarGroup
{
    public string            Name  { get; set; } = "";
    public IList<ToolbarItem> Items { get; set; } = new List<ToolbarItem>();
}

// ── Definition ────────────────────────────────────────────────────────────────

/// <summary>
/// Configurable ordered collection of <see cref="ToolbarGroup"/> instances,
/// together with a layout mode for overflow handling.
/// </summary>
public sealed class ToolbarDefinition
{
    public IList<ToolbarGroup> Groups     { get; set; } = new List<ToolbarGroup>();
    public ToolbarLayoutMode   LayoutMode { get; set; } = ToolbarLayoutMode.Wrap;

    /// <summary>
    /// Maximum number of toolbar rows to display before items overflow into an
    /// ellipsis (…) button.  0 means unlimited (show all rows).
    /// Only meaningful when <see cref="LayoutMode"/> is <see cref="ToolbarLayoutMode.Wrap"/>
    /// or <see cref="ToolbarLayoutMode.Overflow"/>.
    /// </summary>
    public int MaxRows { get; set; } = 0;

    /// <summary>Flat enumeration of every <see cref="ToolbarItem"/> across all groups.</summary>
    public IEnumerable<ToolbarItem> AllItems => Groups.SelectMany(g => g.Items);

    /// <summary>
    /// Creates a toolbar definition from a flat collection of items.
    /// </summary>
    public static ToolbarDefinition CreateFromItems(IList<ToolbarItem> items)
    {
        var def = new ToolbarDefinition();
        var group = new ToolbarGroup { Name = "Custom" };
        foreach (var item in items)
            group.Items.Add(item);
        def.Groups.Add(group);
        return def;
    }

    /// <summary>
    /// Creates the canonical default toolbar matching the full MintedTextEditor feature set.
    /// </summary>
    public static ToolbarDefinition CreateDefault()
    {
        var def = new ToolbarDefinition();

        def.Groups.Add(MakeGroup("History",
            new ToolbarButton { Label = "Undo", Icon = "undo", Command = new UndoCommand(), IsToggle = false },
            new ToolbarButton { Label = "Redo", Icon = "redo", Command = new RedoCommand(), IsToggle = false }));

        def.Groups.Add(MakeGroup("Font",
            new ToolbarDropdown { Label = "Font Family", Icon = "font-family" },
            new ToolbarDropdown { Label = "Font Size",   Icon = "font-size"   }));

        def.Groups.Add(MakeGroup("Formatting",
            new ToolbarButton { Label = "Bold",          Icon = "bold",          Command = new ToggleBoldCommand(),          IsToggle = true },
            new ToolbarButton { Label = "Italic",        Icon = "italic",        Command = new ToggleItalicCommand(),        IsToggle = true },
            new ToolbarButton { Label = "Underline",     Icon = "underline",     Command = new ToggleUnderlineCommand(),     IsToggle = true },
            new ToolbarButton { Label = "Strikethrough", Icon = "strikethrough", Command = new ToggleStrikethroughCommand(), IsToggle = true }));

        def.Groups.Add(MakeGroup("Colors",
            new ToolbarColorPicker { Label = "Text Color",      Icon = "text-color"      },
            new ToolbarColorPicker { Label = "Highlight Color", Icon = "highlight-color" }));

        def.Groups.Add(MakeGroup("Alignment",
            new ToolbarButton { Label = "Align Left",    Icon = "align-left",    Command = new AlignLeftCommand(),    IsToggle = true },
            new ToolbarButton { Label = "Align Center",  Icon = "align-center",  Command = new AlignCenterCommand(),  IsToggle = true },
            new ToolbarButton { Label = "Align Right",   Icon = "align-right",   Command = new AlignRightCommand(),   IsToggle = true },
            new ToolbarButton { Label = "Justify",       Icon = "align-justify", Command = new AlignJustifyCommand(), IsToggle = true }));

        def.Groups.Add(MakeGroup("Lists",
            new ToolbarButton { Label = "Bullet List",     Icon = "bullet-list",     Command = new ToggleBulletListCommand(), IsToggle = true  },
            new ToolbarButton { Label = "Number List",     Icon = "number-list",     Command = new ToggleNumberListCommand(), IsToggle = true  },
            new ToolbarButton { Label = "Decrease Indent", Icon = "indent-decrease", Command = new DecreaseIndentCommand(),   IsToggle = false },
            new ToolbarButton { Label = "Increase Indent", Icon = "indent-increase", Command = new IncreaseIndentCommand(),   IsToggle = false }));

        def.Groups.Add(MakeGroup("Script",
            new ToolbarButton { Label = "Subscript",   Icon = "subscript",   Command = new ToggleSubscriptCommand(),   IsToggle = true },
            new ToolbarButton { Label = "Superscript", Icon = "superscript", Command = new ToggleSuperscriptCommand(), IsToggle = true }));

        def.Groups.Add(MakeGroup("Insert",
            new ToolbarButton { Label = "Insert Hyperlink", Icon = "hyperlink", Command = new InsertHyperlinkCommand(), IsToggle = false },
            new ToolbarButton { Label = "Insert Image",     Icon = "image",     Command = new InsertImageCommand(),     IsToggle = false },
            new ToolbarButton { Label = "Insert Table",     Icon = "table",     Command = new InsertTableCommand(),     IsToggle = false }));

        def.Groups.Add(MakeGroup("Edit",
            new ToolbarDropdown { Label = "Edit",   Icon = "edit-actions" },
            new ToolbarDropdown { Label = "Object", Icon = "object-actions" },
            new ToolbarDropdown { Label = "Table",  Icon = "table-actions" }));

        def.Groups.Add(MakeGroup("Paragraph",
            new ToolbarDropdown { Label = "Heading Level",    Icon = "heading"          },
            new ToolbarButton   { Label = "Clear Formatting", Icon = "clear-formatting",
                Command = new ClearFormattingCommand(), IsToggle = false }));

        return def;
    }

    /// <summary>
    /// Creates a toolbar with only formatting controls (font, style, colour, alignment, lists).
    /// </summary>
    public static ToolbarDefinition CreateFormattingOnly()
    {
        var def = new ToolbarDefinition();

        def.Groups.Add(MakeGroup("Font",
            new ToolbarDropdown { Label = "Font Family", Icon = "font-family" },
            new ToolbarDropdown { Label = "Font Size",   Icon = "font-size"   }));

        def.Groups.Add(MakeGroup("Formatting",
            new ToolbarButton { Label = "Bold",          Icon = "bold",          Command = new ToggleBoldCommand(),          IsToggle = true },
            new ToolbarButton { Label = "Italic",        Icon = "italic",        Command = new ToggleItalicCommand(),        IsToggle = true },
            new ToolbarButton { Label = "Underline",     Icon = "underline",     Command = new ToggleUnderlineCommand(),     IsToggle = true },
            new ToolbarButton { Label = "Strikethrough", Icon = "strikethrough", Command = new ToggleStrikethroughCommand(), IsToggle = true }));

        def.Groups.Add(MakeGroup("Colors",
            new ToolbarColorPicker { Label = "Text Color",      Icon = "text-color"      },
            new ToolbarColorPicker { Label = "Highlight Color", Icon = "highlight-color" }));

        def.Groups.Add(MakeGroup("Alignment",
            new ToolbarButton { Label = "Align Left",   Icon = "align-left",    Command = new AlignLeftCommand(),    IsToggle = true },
            new ToolbarButton { Label = "Align Center", Icon = "align-center",  Command = new AlignCenterCommand(),  IsToggle = true },
            new ToolbarButton { Label = "Align Right",  Icon = "align-right",   Command = new AlignRightCommand(),   IsToggle = true },
            new ToolbarButton { Label = "Justify",      Icon = "align-justify", Command = new AlignJustifyCommand(), IsToggle = true }));

        def.Groups.Add(MakeGroup("Lists",
            new ToolbarButton { Label = "Bullet List",     Icon = "bullet-list",     Command = new ToggleBulletListCommand(), IsToggle = true  },
            new ToolbarButton { Label = "Number List",     Icon = "number-list",     Command = new ToggleNumberListCommand(), IsToggle = true  },
            new ToolbarButton { Label = "Decrease Indent", Icon = "indent-decrease", Command = new DecreaseIndentCommand(),   IsToggle = false },
            new ToolbarButton { Label = "Increase Indent", Icon = "indent-increase", Command = new IncreaseIndentCommand(),   IsToggle = false }));

        def.Groups.Add(MakeGroup("Script",
            new ToolbarButton { Label = "Subscript",   Icon = "subscript",   Command = new ToggleSubscriptCommand(),   IsToggle = true },
            new ToolbarButton { Label = "Superscript", Icon = "superscript", Command = new ToggleSuperscriptCommand(), IsToggle = true }));

        return def;
    }

    /// <summary>
    /// Creates a toolbar with only insert controls (hyperlink, image, table).
    /// </summary>
    public static ToolbarDefinition CreateInsertOnly()
    {
        var def = new ToolbarDefinition();

        def.Groups.Add(MakeGroup("Insert",
            new ToolbarButton { Label = "Insert Hyperlink", Icon = "hyperlink", Command = new InsertHyperlinkCommand(), IsToggle = false },
            new ToolbarButton { Label = "Insert Image",     Icon = "image",     Command = new InsertImageCommand(),     IsToggle = false },
            new ToolbarButton { Label = "Insert Table",     Icon = "table",     Command = new InsertTableCommand(),     IsToggle = false }));

        return def;
    }

    /// <summary>
    /// Creates a minimal toolbar with undo/redo and core text formatting only.
    /// </summary>
    public static ToolbarDefinition CreateMinimal()
    {
        var def = new ToolbarDefinition();

        def.Groups.Add(MakeGroup("History",
            new ToolbarButton { Label = "Undo", Icon = "undo", Command = new UndoCommand(), IsToggle = false },
            new ToolbarButton { Label = "Redo", Icon = "redo", Command = new RedoCommand(), IsToggle = false }));

        def.Groups.Add(MakeGroup("Formatting",
            new ToolbarButton { Label = "Bold",      Icon = "bold",      Command = new ToggleBoldCommand(),      IsToggle = true },
            new ToolbarButton { Label = "Italic",    Icon = "italic",    Command = new ToggleItalicCommand(),    IsToggle = true },
            new ToolbarButton { Label = "Underline", Icon = "underline", Command = new ToggleUnderlineCommand(), IsToggle = true }));

        return def;
    }

    private static ToolbarGroup MakeGroup(string name, params ToolbarItem[] items)
    {
        var g = new ToolbarGroup { Name = name };
        foreach (var item in items) g.Items.Add(item);
        return g;
    }
}
