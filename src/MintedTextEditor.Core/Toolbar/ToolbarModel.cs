using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.Localization;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Toolbar;

/// <summary>Layout behaviour when toolbar items overflow available width.</summary>
public enum ToolbarLayoutMode
{
    /// <summary>Wrap items onto additional rows, up to <see cref="ToolbarDefinition.MaxRows"/>.</summary>
    Wrap,
    /// <summary>Keep all items on a single row without wrapping.</summary>
    Scroll,
    /// <summary>Wrap like <see cref="Wrap"/>; items beyond <see cref="ToolbarDefinition.MaxRows"/> rows move to the overflow (…) panel.</summary>
    Overflow
}

// ── Base ─────────────────────────────────────────────────────────────────────

/// <summary>Abstract base for all toolbar elements.</summary>
public abstract class ToolbarItem
{
    private string _label = "";

    /// <summary>
    /// Text shown when no icon is drawn; for a dropdown, shown while nothing is selected.
    /// When <see cref="LocalizedLabel"/> is set, the value is resolved from
    /// <see cref="EditorStrings.Current"/> on every read, so replacing the active strings takes
    /// effect the next time the toolbar is drawn. Assigning this property clears
    /// <see cref="LocalizedLabel"/> and fixes the label to the assigned text.
    /// </summary>
    public string Label
    {
        get => LocalizedLabel is { } resolve ? resolve(EditorStrings.Current) : _label;
        set
        {
            _label         = value ?? "";
            LocalizedLabel = null;
        }
    }

    /// <summary>
    /// Selects the label text from an <see cref="EditorStrings"/> instance (for example
    /// <c>s =&gt; s.Bold</c>). The built-in toolbars set this; when null, <see cref="Label"/>
    /// returns the text it was assigned.
    /// </summary>
    public Func<EditorStrings, string>? LocalizedLabel { get; set; }

    /// <summary>Icon key (for example "bold") used to look up a vector or image icon.</summary>
    public string Icon      { get; set; } = "";
    /// <summary>Whether the item is enabled. Disabled items are drawn in the disabled color.</summary>
    public bool   IsEnabled { get; set; } = true;
    /// <summary>Whether the item is laid out and drawn. Hidden items take no space.</summary>
    public bool   IsVisible { get; set; } = true;
}

// ── Concrete items ────────────────────────────────────────────────────────────

/// <summary>A clickable button optionally associated with an <see cref="IEditorCommand"/>.</summary>
public sealed class ToolbarButton : ToolbarItem
{
    /// <summary>Command executed when the button is tapped, or null if none.</summary>
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
    /// <summary>Options shown in the drop-down list.</summary>
    public IList<string> Items           { get; set; } = new List<string>();
    /// <summary>Index of the selected option in <see cref="Items"/>, or -1 for none.</summary>
    public int           SelectedIndex   { get; set; } = -1;
    /// <summary>Called with the new index when the user picks an option.</summary>
    public Action<int>?  OnSelectionChanged { get; set; }
}

/// <summary>A colour-swatch grid picker (text colour, highlight colour, …).</summary>
public sealed class ToolbarColorPicker : ToolbarItem
{
    /// <summary>Swatches offered by the picker. Defaults to ten basic colors.</summary>
    public IList<EditorColor>   Colors          { get; set; } = CreateDefaultColors();
    /// <summary>Currently selected color, shown on the picker button.</summary>
    public EditorColor          SelectedColor   { get; set; }
    /// <summary>Called with the chosen color when the user picks a swatch.</summary>
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
    /// <summary>Name identifying the group.</summary>
    public string            Name  { get; set; } = "";
    /// <summary>Items in the group, in display order.</summary>
    public IList<ToolbarItem> Items { get; set; } = new List<ToolbarItem>();
}

// ── Definition ────────────────────────────────────────────────────────────────

/// <summary>
/// Configurable ordered collection of <see cref="ToolbarGroup"/> instances,
/// together with a layout mode for overflow handling.
/// </summary>
public sealed class ToolbarDefinition
{
    /// <summary>Groups in display order.</summary>
    public IList<ToolbarGroup> Groups     { get; set; } = new List<ToolbarGroup>();
    /// <summary>How items are laid out when they exceed the available width. Defaults to <see cref="ToolbarLayoutMode.Wrap"/>.</summary>
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
            new ToolbarButton { LocalizedLabel = s => s.Undo, Icon = "undo", Command = new UndoCommand(), IsToggle = false },
            new ToolbarButton { LocalizedLabel = s => s.Redo, Icon = "redo", Command = new RedoCommand(), IsToggle = false }));

        def.Groups.Add(MakeGroup("Font",
            new ToolbarDropdown { LocalizedLabel = s => s.FontFamily, Icon = "font-family" },
            new ToolbarDropdown { LocalizedLabel = s => s.FontSize, Icon = "font-size"   }));

        def.Groups.Add(MakeGroup("Formatting",
            new ToolbarButton { LocalizedLabel = s => s.Bold, Icon = "bold",          Command = new ToggleBoldCommand(),          IsToggle = true },
            new ToolbarButton { LocalizedLabel = s => s.Italic, Icon = "italic",        Command = new ToggleItalicCommand(),        IsToggle = true },
            new ToolbarButton { LocalizedLabel = s => s.Underline, Icon = "underline",     Command = new ToggleUnderlineCommand(),     IsToggle = true },
            new ToolbarButton { LocalizedLabel = s => s.Strikethrough, Icon = "strikethrough", Command = new ToggleStrikethroughCommand(), IsToggle = true }));

        def.Groups.Add(MakeGroup("Colors",
            new ToolbarColorPicker { LocalizedLabel = s => s.TextColor, Icon = "text-color"      },
            new ToolbarColorPicker { LocalizedLabel = s => s.Highlight, Icon = "highlight-color" }));

        def.Groups.Add(MakeGroup("Alignment",
            new ToolbarButton { LocalizedLabel = s => s.AlignLeft, Icon = "align-left",    Command = new AlignLeftCommand(),    IsToggle = true },
            new ToolbarButton { LocalizedLabel = s => s.AlignCenter, Icon = "align-center",  Command = new AlignCenterCommand(),  IsToggle = true },
            new ToolbarButton { LocalizedLabel = s => s.AlignRight, Icon = "align-right",   Command = new AlignRightCommand(),   IsToggle = true },
            new ToolbarButton { LocalizedLabel = s => s.AlignJustify, Icon = "align-justify", Command = new AlignJustifyCommand(), IsToggle = true }));

        def.Groups.Add(MakeGroup("Lists",
            new ToolbarButton { LocalizedLabel = s => s.BulletList, Icon = "bullet-list",     Command = new ToggleBulletListCommand(), IsToggle = true  },
            new ToolbarButton { LocalizedLabel = s => s.NumberedList, Icon = "number-list",     Command = new ToggleNumberListCommand(), IsToggle = true  },
            new ToolbarButton { LocalizedLabel = s => s.IndentDecrease, Icon = "indent-decrease", Command = new DecreaseIndentCommand(),   IsToggle = false },
            new ToolbarButton { LocalizedLabel = s => s.IndentIncrease, Icon = "indent-increase", Command = new IncreaseIndentCommand(),   IsToggle = false }));

        def.Groups.Add(MakeGroup("Script",
            new ToolbarButton { LocalizedLabel = s => s.Subscript, Icon = "subscript",   Command = new ToggleSubscriptCommand(),   IsToggle = true },
            new ToolbarButton { LocalizedLabel = s => s.Superscript, Icon = "superscript", Command = new ToggleSuperscriptCommand(), IsToggle = true }));

        def.Groups.Add(MakeGroup("Insert",
            new ToolbarButton { LocalizedLabel = s => s.InsertLink, Icon = "hyperlink", Command = new InsertHyperlinkCommand(), IsToggle = false },
            new ToolbarButton { LocalizedLabel = s => s.InsertImage, Icon = "image",     Command = new InsertImageCommand(),     IsToggle = false },
            new ToolbarButton { LocalizedLabel = s => s.InsertTable, Icon = "table",     Command = new InsertTableCommand(),     IsToggle = false }));

        def.Groups.Add(MakeGroup("Edit",
            new ToolbarDropdown { LocalizedLabel = s => s.EditMenu, Icon = "edit-actions" },
            new ToolbarDropdown { LocalizedLabel = s => s.ObjectMenu, Icon = "object-actions" },
            new ToolbarDropdown { LocalizedLabel = s => s.TableMenu, Icon = "table-actions" }));

        def.Groups.Add(MakeGroup("Paragraph",
            new ToolbarDropdown { LocalizedLabel = s => s.HeadingLevel, Icon = "heading"          },
            new ToolbarButton   { LocalizedLabel = s => s.ClearFormat, Icon = "clear-formatting",
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
            new ToolbarDropdown { LocalizedLabel = s => s.FontFamily, Icon = "font-family" },
            new ToolbarDropdown { LocalizedLabel = s => s.FontSize, Icon = "font-size"   }));

        def.Groups.Add(MakeGroup("Formatting",
            new ToolbarButton { LocalizedLabel = s => s.Bold, Icon = "bold",          Command = new ToggleBoldCommand(),          IsToggle = true },
            new ToolbarButton { LocalizedLabel = s => s.Italic, Icon = "italic",        Command = new ToggleItalicCommand(),        IsToggle = true },
            new ToolbarButton { LocalizedLabel = s => s.Underline, Icon = "underline",     Command = new ToggleUnderlineCommand(),     IsToggle = true },
            new ToolbarButton { LocalizedLabel = s => s.Strikethrough, Icon = "strikethrough", Command = new ToggleStrikethroughCommand(), IsToggle = true }));

        def.Groups.Add(MakeGroup("Colors",
            new ToolbarColorPicker { LocalizedLabel = s => s.TextColor, Icon = "text-color"      },
            new ToolbarColorPicker { LocalizedLabel = s => s.Highlight, Icon = "highlight-color" }));

        def.Groups.Add(MakeGroup("Alignment",
            new ToolbarButton { LocalizedLabel = s => s.AlignLeft, Icon = "align-left",    Command = new AlignLeftCommand(),    IsToggle = true },
            new ToolbarButton { LocalizedLabel = s => s.AlignCenter, Icon = "align-center",  Command = new AlignCenterCommand(),  IsToggle = true },
            new ToolbarButton { LocalizedLabel = s => s.AlignRight, Icon = "align-right",   Command = new AlignRightCommand(),   IsToggle = true },
            new ToolbarButton { LocalizedLabel = s => s.AlignJustify, Icon = "align-justify", Command = new AlignJustifyCommand(), IsToggle = true }));

        def.Groups.Add(MakeGroup("Lists",
            new ToolbarButton { LocalizedLabel = s => s.BulletList, Icon = "bullet-list",     Command = new ToggleBulletListCommand(), IsToggle = true  },
            new ToolbarButton { LocalizedLabel = s => s.NumberedList, Icon = "number-list",     Command = new ToggleNumberListCommand(), IsToggle = true  },
            new ToolbarButton { LocalizedLabel = s => s.IndentDecrease, Icon = "indent-decrease", Command = new DecreaseIndentCommand(),   IsToggle = false },
            new ToolbarButton { LocalizedLabel = s => s.IndentIncrease, Icon = "indent-increase", Command = new IncreaseIndentCommand(),   IsToggle = false }));

        def.Groups.Add(MakeGroup("Script",
            new ToolbarButton { LocalizedLabel = s => s.Subscript, Icon = "subscript",   Command = new ToggleSubscriptCommand(),   IsToggle = true },
            new ToolbarButton { LocalizedLabel = s => s.Superscript, Icon = "superscript", Command = new ToggleSuperscriptCommand(), IsToggle = true }));

        return def;
    }

    /// <summary>
    /// Creates a toolbar with only insert controls (hyperlink, image, table).
    /// </summary>
    public static ToolbarDefinition CreateInsertOnly()
    {
        var def = new ToolbarDefinition();

        def.Groups.Add(MakeGroup("Insert",
            new ToolbarButton { LocalizedLabel = s => s.InsertLink, Icon = "hyperlink", Command = new InsertHyperlinkCommand(), IsToggle = false },
            new ToolbarButton { LocalizedLabel = s => s.InsertImage, Icon = "image",     Command = new InsertImageCommand(),     IsToggle = false },
            new ToolbarButton { LocalizedLabel = s => s.InsertTable, Icon = "table",     Command = new InsertTableCommand(),     IsToggle = false }));

        return def;
    }

    /// <summary>
    /// Creates a minimal toolbar with undo/redo and core text formatting only.
    /// </summary>
    public static ToolbarDefinition CreateMinimal()
    {
        var def = new ToolbarDefinition();

        def.Groups.Add(MakeGroup("History",
            new ToolbarButton { LocalizedLabel = s => s.Undo, Icon = "undo", Command = new UndoCommand(), IsToggle = false },
            new ToolbarButton { LocalizedLabel = s => s.Redo, Icon = "redo", Command = new RedoCommand(), IsToggle = false }));

        def.Groups.Add(MakeGroup("Formatting",
            new ToolbarButton { LocalizedLabel = s => s.Bold, Icon = "bold",      Command = new ToggleBoldCommand(),      IsToggle = true },
            new ToolbarButton { LocalizedLabel = s => s.Italic, Icon = "italic",    Command = new ToggleItalicCommand(),    IsToggle = true },
            new ToolbarButton { LocalizedLabel = s => s.Underline, Icon = "underline", Command = new ToggleUnderlineCommand(), IsToggle = true }));

        return def;
    }

    private static ToolbarGroup MakeGroup(string name, params ToolbarItem[] items)
    {
        var g = new ToolbarGroup { Name = name };
        foreach (var item in items) g.Items.Add(item);
        return g;
    }
}
