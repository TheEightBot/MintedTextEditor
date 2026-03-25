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
    public string Name => "InsertTable";
    public string Description => "Inserts a table with the specified number of rows and columns.";

    public int Rows { get; set; } = 2;
    public int Columns { get; set; } = 2;

    public void Execute(EditorContext ctx)
    {
        if (Rows <= 0 || Columns <= 0) return;
        var action = new InsertTableAction(ctx.Document, ctx.Selection.Active, Rows, Columns);
        var pos = ctx.UndoManager.Push(action);
        ctx.Selection.CollapseTo(pos);
    }

    public bool CanExecute(EditorContext ctx) => Rows > 0 && Columns > 0;
}

/// <summary>Inserts a row above the row containing the caret.</summary>
public sealed class InsertRowAboveCommand : IEditorCommand
{
    public string Name => "InsertRowAbove";
    public string Description => "Insert a row above the current row.";

    public bool CanExecute(EditorContext ctx) => ctx.Selection.Active.IsInTableCell;

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
    public string Name => "InsertRowBelow";
    public string Description => "Insert a row below the current row.";

    public bool CanExecute(EditorContext ctx) => ctx.Selection.Active.IsInTableCell;

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
    public string Name => "DeleteRow";
    public string Description => "Delete the current row.";

    public bool CanExecute(EditorContext ctx)
    {
        var pos = ctx.Selection.Active;
        return pos.IsInTableCell
            && ctx.Document.Blocks[pos.BlockIndex] is TableBlock t
            && t.RowCount > 1;
    }

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
    public string Name => "InsertColumnLeft";
    public string Description => "Insert a column to the left of the current column.";

    public bool CanExecute(EditorContext ctx) => ctx.Selection.Active.IsInTableCell;

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
    public string Name => "InsertColumnRight";
    public string Description => "Insert a column to the right of the current column.";

    public bool CanExecute(EditorContext ctx) => ctx.Selection.Active.IsInTableCell;

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
    public string Name => "DeleteColumn";
    public string Description => "Delete the current column.";

    public bool CanExecute(EditorContext ctx)
    {
        var pos = ctx.Selection.Active;
        return pos.IsInTableCell
            && ctx.Document.Blocks[pos.BlockIndex] is TableBlock t
            && t.ColumnCount > 1;
    }

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
    public string Name => "DeleteTable";
    public string Description => "Delete the entire table.";

    public bool CanExecute(EditorContext ctx) => ctx.Selection.Active.IsInTableCell;

    public void Execute(EditorContext ctx)
    {
        var pos = ctx.Selection.Active;
        if (ctx.Document.Blocks[pos.BlockIndex] is not TableBlock table) return;
        var action = new DeleteTableAction(ctx.Document, table);
        ctx.Selection.CollapseTo(ctx.UndoManager.Push(action));
    }
}
