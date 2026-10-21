using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Editing;
using MintedTextEditor.Core.Formatting;

namespace MintedTextEditor.Core.Commands;

/// <summary>
/// Inserts a table at the current caret position.
/// Set <see cref="Rows"/> and <see cref="Columns"/> before calling <see cref="Execute"/>.
/// </summary>
public sealed class InsertTableCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "InsertTable";
    /// <inheritdoc/>
    public string Description => "Inserts a table with the specified number of rows and columns.";

    /// <summary>Number of rows in the new table. Must be greater than zero. Defaults to 2.</summary>
    public int Rows { get; set; } = 2;
    /// <summary>Number of columns in the new table. Must be greater than zero. Defaults to 2.</summary>
    public int Columns { get; set; } = 2;

    /// <inheritdoc/>
    public void Execute(EditorContext ctx)
    {
        if (Rows <= 0 || Columns <= 0) return;
        var action = new InsertTableAction(ctx.Document, ctx.Selection.Active, Rows, Columns);
        var pos = ctx.UndoManager.Push(action);
        ctx.Selection.CollapseTo(pos);
    }

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => Rows > 0 && Columns > 0;
}

/// <summary>Inserts a row above the row containing the caret.</summary>
public sealed class InsertRowAboveCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "InsertRowAbove";
    /// <inheritdoc/>
    public string Description => "Insert a row above the current row.";

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => ctx.Selection.Active.IsInTableCell;

    /// <inheritdoc/>
    public void Execute(EditorContext ctx)
    {
        var pos = ctx.Selection.Active;
        if (ctx.Document.Blocks[pos.BlockIndex] is not TableBlock table) return;
        var action = new InsertRowAction(ctx.Document, table, pos.CellRow - 1, pos.CellRow);
        ctx.Selection.CollapseTo(ctx.UndoManager.Push(action));
    }
}

/// <summary>Inserts a row below the row containing the caret.</summary>
public sealed class InsertRowBelowCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "InsertRowBelow";
    /// <inheritdoc/>
    public string Description => "Insert a row below the current row.";

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => ctx.Selection.Active.IsInTableCell;

    /// <inheritdoc/>
    public void Execute(EditorContext ctx)
    {
        var pos = ctx.Selection.Active;
        if (ctx.Document.Blocks[pos.BlockIndex] is not TableBlock table) return;
        var action = new InsertRowAction(ctx.Document, table, pos.CellRow, pos.CellRow);
        ctx.Selection.CollapseTo(ctx.UndoManager.Push(action));
    }
}

/// <summary>Deletes the row containing the caret. Disabled when the table has only one row.</summary>
public sealed class DeleteRowCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "DeleteRow";
    /// <inheritdoc/>
    public string Description => "Delete the current row.";

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx)
    {
        var pos = ctx.Selection.Active;
        return pos.IsInTableCell
            && ctx.Document.Blocks[pos.BlockIndex] is TableBlock t
            && t.RowCount > 1;
    }

    /// <inheritdoc/>
    public void Execute(EditorContext ctx)
    {
        var pos = ctx.Selection.Active;
        if (ctx.Document.Blocks[pos.BlockIndex] is not TableBlock table) return;
        var action = new DeleteRowAction(ctx.Document, table, pos.CellRow);
        ctx.Selection.CollapseTo(ctx.UndoManager.Push(action));
    }
}

/// <summary>Inserts a column to the left of the column containing the caret.</summary>
public sealed class InsertColumnLeftCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "InsertColumnLeft";
    /// <inheritdoc/>
    public string Description => "Insert a column to the left of the current column.";

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => ctx.Selection.Active.IsInTableCell;

    /// <inheritdoc/>
    public void Execute(EditorContext ctx)
    {
        var pos = ctx.Selection.Active;
        if (ctx.Document.Blocks[pos.BlockIndex] is not TableBlock table) return;
        var action = new InsertColumnAction(ctx.Document, table, pos.CellCol - 1, pos.CellCol);
        ctx.Selection.CollapseTo(ctx.UndoManager.Push(action));
    }
}

/// <summary>Inserts a column to the right of the column containing the caret.</summary>
public sealed class InsertColumnRightCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "InsertColumnRight";
    /// <inheritdoc/>
    public string Description => "Insert a column to the right of the current column.";

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => ctx.Selection.Active.IsInTableCell;

    /// <inheritdoc/>
    public void Execute(EditorContext ctx)
    {
        var pos = ctx.Selection.Active;
        if (ctx.Document.Blocks[pos.BlockIndex] is not TableBlock table) return;
        var action = new InsertColumnAction(ctx.Document, table, pos.CellCol, pos.CellCol);
        ctx.Selection.CollapseTo(ctx.UndoManager.Push(action));
    }
}

/// <summary>Deletes the column containing the caret. Disabled when the table has only one column.</summary>
public sealed class DeleteColumnCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "DeleteColumn";
    /// <inheritdoc/>
    public string Description => "Delete the current column.";

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx)
    {
        var pos = ctx.Selection.Active;
        return pos.IsInTableCell
            && ctx.Document.Blocks[pos.BlockIndex] is TableBlock t
            && t.ColumnCount > 1;
    }

    /// <inheritdoc/>
    public void Execute(EditorContext ctx)
    {
        var pos = ctx.Selection.Active;
        if (ctx.Document.Blocks[pos.BlockIndex] is not TableBlock table) return;
        var action = new DeleteColumnAction(ctx.Document, table, pos.CellCol);
        ctx.Selection.CollapseTo(ctx.UndoManager.Push(action));
    }
}

/// <summary>Deletes the entire table containing the caret.</summary>
public sealed class DeleteTableCommand : IEditorCommand
{
    /// <inheritdoc/>
    public string Name => "DeleteTable";
    /// <inheritdoc/>
    public string Description => "Delete the entire table.";

    /// <inheritdoc/>
    public bool CanExecute(EditorContext ctx) => ctx.Selection.Active.IsInTableCell;

    /// <inheritdoc/>
    public void Execute(EditorContext ctx)
    {
        var pos = ctx.Selection.Active;
        if (ctx.Document.Blocks[pos.BlockIndex] is not TableBlock table) return;
        var action = new DeleteTableAction(ctx.Document, table);
        ctx.Selection.CollapseTo(ctx.UndoManager.Push(action));
    }
}
