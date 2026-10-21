using MintedTextEditor.Core.Commands;
using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Localization;

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
    /// <remarks>Item labels are taken from <see cref="EditorStrings.Current"/> at the time of the call.</remarks>
    public static ContextMenuDefinition CreateDefault(EditorContext ctx)
        => CreateDefault(ctx, EditorStrings.Current);

    /// <summary>
    /// Creates the default context menu for <paramref name="ctx"/>, labelling items from
    /// <paramref name="strings"/>. Hyperlink-specific items are included only when relevant.
    /// </summary>
    /// <param name="ctx">The current editor context.</param>
    /// <param name="strings">The strings used for item labels.</param>
    /// <returns>A new menu definition with clipboard, hyperlink, image, and table items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="strings"/> is null.</exception>
    public static ContextMenuDefinition CreateDefault(EditorContext ctx, EditorStrings strings)
    {
        ArgumentNullException.ThrowIfNull(strings);

        bool hasSelection    = !ctx.Selection.IsEmpty;
        bool onHyperlink     = IsSelectionOnHyperlink(ctx);
        bool inTable         = ctx.Selection.Active.IsInTableCell;
        bool canPaste        = true; // platform would check clipboard; assume true for model

        var def = new ContextMenuDefinition();

        def.Items.Add(new ContextMenuItem
        {
            Label     = strings.Cut,
            Icon      = "cut",
            Command   = new CutCommand(),
            IsEnabled = hasSelection,
        });
        def.Items.Add(new ContextMenuItem
        {
            Label     = strings.Copy,
            Icon      = "copy",
            Command   = new CopyCommand(),
            IsEnabled = hasSelection,
        });
        def.Items.Add(new ContextMenuItem
        {
            Label          = strings.Paste,
            Icon           = "paste",
            Command        = null, // Paste is handled by the platform layer
            IsEnabled      = canPaste,
            SeparatorAfter = true,
        });

        def.Items.Add(new ContextMenuItem
        {
            Label          = strings.SelectAll,
            Icon           = "select-all",
            Command        = new SelectAllCommand(),
            SeparatorAfter = true,
        });

        if (onHyperlink)
        {
            def.Items.Add(new ContextMenuItem
            {
                Label   = strings.EditLink,
                Icon    = "hyperlink-edit",
                Command = new InsertHyperlinkCommand(),
            });
            def.Items.Add(new ContextMenuItem
            {
                Label   = strings.RemoveLink,
                Icon    = "hyperlink-remove",
                Command = new RemoveHyperlinkCommand(),
            });
            def.Items.Add(new ContextMenuItem
            {
                Label          = strings.OpenLink,
                Icon           = "hyperlink-open",
                Command        = new OpenHyperlinkCommand(),
                SeparatorAfter = true,
            });
        }
        else
        {
            def.Items.Add(new ContextMenuItem
            {
                Label          = strings.InsertLink,
                Icon           = "hyperlink",
                Command        = new InsertHyperlinkCommand(),
                SeparatorAfter = true,
            });
        }

        def.Items.Add(new ContextMenuItem
        {
            Label   = strings.InsertImage,
            Icon    = "image",
            Command = new InsertImageCommand(),
        });
        def.Items.Add(new ContextMenuItem
        {
            Label          = strings.InsertTable,
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
                Label   = strings.InsertRowAbove,
                Icon    = "table-insert-row-above",
                Command = new InsertRowAboveCommand(),
            });
            def.Items.Add(new ContextMenuItem
            {
                Label   = strings.InsertRowBelow,
                Icon    = "table-insert-row-below",
                Command = new InsertRowBelowCommand(),
            });
            def.Items.Add(new ContextMenuItem
            {
                Label     = strings.DeleteRow,
                Icon      = "table-delete-row",
                Command   = new DeleteRowCommand(),
                IsEnabled = table is not null && table.RowCount > 1,
            });
            def.Items.Add(new ContextMenuItem
            {
                Label   = strings.InsertColumnLeft,
                Icon    = "table-insert-col-left",
                Command = new InsertColumnLeftCommand(),
            });
            def.Items.Add(new ContextMenuItem
            {
                Label   = strings.InsertColumnRight,
                Icon    = "table-insert-col-right",
                Command = new InsertColumnRightCommand(),
            });
            def.Items.Add(new ContextMenuItem
            {
                Label     = strings.DeleteColumn,
                Icon      = "table-delete-col",
                Command   = new DeleteColumnCommand(),
                IsEnabled = table is not null && table.ColumnCount > 1,
            });
            def.Items.Add(new ContextMenuItem
            {
                Label          = strings.DeleteTable,
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
