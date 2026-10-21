using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.Document;

namespace MintedTextEditor.Core.ContextMenu;

// ── Item ─────────────────────────────────────────────────────────────────────

/// <summary>A single entry in a context menu.</summary>
public sealed class ContextMenuItem
{
    /// <summary>Text displayed for the item.</summary>
    public string         Label          { get; set; } = "";
    /// <summary>Icon identifier for the item (for example "cut" or "table").</summary>
    public string         Icon           { get; set; } = "";
    /// <summary>Command to execute when the item is chosen, or null when the platform layer handles it.</summary>
    public IEditorCommand? Command       { get; set; }
    /// <summary>When false the item is drawn greyed out.</summary>
    public bool           IsEnabled      { get; set; } = true;
    /// <summary>When false the item is omitted from the menu.</summary>
    public bool           IsVisible      { get; set; } = true;
    /// <summary>When true a visual separator is drawn below this item.</summary>
    public bool           SeparatorAfter { get; set; }
}

// ── Definition ────────────────────────────────────────────────────────────────

/// <summary>Ordered collection of <see cref="ContextMenuItem"/> instances.</summary>
public sealed class ContextMenuDefinition
{
    /// <summary>All items in display order, including hidden ones.</summary>
    public IList<ContextMenuItem> Items { get; set; } = new List<ContextMenuItem>();

    /// <summary>Visible items (IsVisible == true) in order.</summary>
    public IEnumerable<ContextMenuItem> VisibleItems => Items.Where(i => i.IsVisible);
}

// ── Event args ────────────────────────────────────────────────────────────────

/// <summary>
/// Raised before the context menu is displayed, allowing consumers to
/// add, remove, or modify items.
/// </summary>
public sealed class ContextMenuItemsRequestedEventArgs : EventArgs
{
    /// <summary>The menu about to be shown; modify its items to customise the menu.</summary>
    public ContextMenuDefinition Definition { get; }
    /// <summary>Editor state at the time the menu was requested.</summary>
    public EditorContext          Context   { get; }

    /// <summary>Creates event args for the given menu and editor context.</summary>
    /// <param name="definition">The menu about to be shown.</param>
    /// <param name="context">The current editor context.</param>
    public ContextMenuItemsRequestedEventArgs(ContextMenuDefinition definition, EditorContext context)
    {
        Definition = definition;
        Context    = context;
    }
}

// ── Factory ───────────────────────────────────────────────────────────────────

/// <summary>Builds the default context menu adjusted for the current editor state.</summary>
public static class ContextMenuFactory
{
    /// <summary>
    /// Creates the default context menu for <paramref name="ctx"/>.
    /// Hyperlink-specific items are included only when relevant.
    /// </summary>
    /// <param name="ctx">The current editor context.</param>
    /// <returns>A new menu definition with clipboard, hyperlink, image, and table items.</returns>
    public static ContextMenuDefinition CreateDefault(EditorContext ctx)
    {
        bool hasSelection    = !ctx.Selection.IsEmpty;
        bool onHyperlink     = IsSelectionOnHyperlink(ctx);
        bool inTable         = ctx.Selection.Active.IsInTableCell;
        bool canPaste        = true; // platform would check clipboard; assume true for model

        var def = new ContextMenuDefinition();

        def.Items.Add(new ContextMenuItem
        {
            Label     = "Cut",
            Icon      = "cut",
            Command   = new CutCommand(),
            IsEnabled = hasSelection,
        });
        def.Items.Add(new ContextMenuItem
        {
            Label     = "Copy",
            Icon      = "copy",
            Command   = new CopyCommand(),
            IsEnabled = hasSelection,
        });
        def.Items.Add(new ContextMenuItem
        {
            Label          = "Paste",
            Icon           = "paste",
            Command        = null, // Paste is handled by the platform layer
            IsEnabled      = canPaste,
            SeparatorAfter = true,
        });

        def.Items.Add(new ContextMenuItem
        {
            Label          = "Select All",
            Icon           = "select-all",
            Command        = new SelectAllCommand(),
            SeparatorAfter = true,
        });

        if (onHyperlink)
        {
            def.Items.Add(new ContextMenuItem
            {
                Label   = "Edit Hyperlink",
                Icon    = "hyperlink-edit",
                Command = new InsertHyperlinkCommand(),
            });
            def.Items.Add(new ContextMenuItem
            {
                Label   = "Remove Hyperlink",
                Icon    = "hyperlink-remove",
                Command = new RemoveHyperlinkCommand(),
            });
            def.Items.Add(new ContextMenuItem
            {
                Label          = "Open Hyperlink",
                Icon           = "hyperlink-open",
                Command        = new OpenHyperlinkCommand(),
                SeparatorAfter = true,
            });
        }
        else
        {
            def.Items.Add(new ContextMenuItem
            {
                Label          = "Insert Hyperlink",
                Icon           = "hyperlink",
                Command        = new InsertHyperlinkCommand(),
                SeparatorAfter = true,
            });
        }

        def.Items.Add(new ContextMenuItem
        {
            Label   = "Insert Image",
            Icon    = "image",
            Command = new InsertImageCommand(),
        });
        def.Items.Add(new ContextMenuItem
        {
            Label          = "Insert Table",
            Icon           = "table",
            Command        = new InsertTableCommand(),
            SeparatorAfter = inTable,
        });

        if (inTable)
        {
            var activePos = ctx.Selection.Active;
            var table = ctx.Document.Blocks[activePos.BlockIndex] as TableBlock;

            def.Items.Add(new ContextMenuItem
            {
                Label   = "Insert Row Above",
                Icon    = "table-insert-row-above",
                Command = new InsertRowAboveCommand(),
            });
            def.Items.Add(new ContextMenuItem
            {
                Label   = "Insert Row Below",
                Icon    = "table-insert-row-below",
                Command = new InsertRowBelowCommand(),
            });
            def.Items.Add(new ContextMenuItem
            {
                Label     = "Delete Row",
                Icon      = "table-delete-row",
                Command   = new DeleteRowCommand(),
                IsEnabled = table is not null && table.RowCount > 1,
            });
            def.Items.Add(new ContextMenuItem
            {
                Label   = "Insert Column Left",
                Icon    = "table-insert-col-left",
                Command = new InsertColumnLeftCommand(),
            });
            def.Items.Add(new ContextMenuItem
            {
                Label   = "Insert Column Right",
                Icon    = "table-insert-col-right",
                Command = new InsertColumnRightCommand(),
            });
            def.Items.Add(new ContextMenuItem
            {
                Label     = "Delete Column",
                Icon      = "table-delete-col",
                Command   = new DeleteColumnCommand(),
                IsEnabled = table is not null && table.ColumnCount > 1,
            });
            def.Items.Add(new ContextMenuItem
            {
                Label          = "Delete Table",
                Icon           = "table-delete",
                Command        = new DeleteTableCommand(),
                SeparatorAfter = false,
            });
        }

        return def;
    }

    private static bool IsSelectionOnHyperlink(EditorContext ctx)
    {
        if (ctx.Selection.IsEmpty) return false;

        var range = ctx.Selection.Range;
        var block = ctx.Document.Blocks.ElementAtOrDefault(range.Start.BlockIndex) as Paragraph;
        if (block is null) return false;

        var inline = block.Inlines.ElementAtOrDefault(range.Start.InlineIndex);
        return inline is HyperlinkInline;
    }
}
