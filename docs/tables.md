# Tables

MintedTextEditor supports creating and editing tables with rich content in each cell.

## Inserting a Table

The toolbar's Insert Table button asks for the number of rows and columns. Programmatically, use the undoable `InsertTableCommand` at the current selection:

```csharp
using MintedTextEditor.Core.Commands;

var ctx = new EditorContext(editor.Document, editor.Selection, editor.UndoManager,
    editor.Formatting, editor.FontFormatting);

// Insert a 3×3 table at the caret position
new InsertTableCommand { Rows = 3, Columns = 3 }.Execute(ctx);
```

`TableEngine.InsertTable(doc, position, rows, cols)` (namespace `MintedTextEditor.Core.Formatting`) inserts one directly into the model, without undo.

## TableBlock Model

Tables are represented in the document as `TableBlock`:

```csharp
var table = (TableBlock)doc.Blocks[0];

Console.WriteLine(table.RowCount);    // number of rows
Console.WriteLine(table.ColumnCount); // number of cells in the first row

// Access a specific cell (null when out of range)
TableCell? cell = table.GetCell(rowIndex: 0, colIndex: 1);

// Each cell contains blocks, normally Paragraphs
foreach (var block in cell!.Blocks)
    Console.WriteLine(block.GetText());
```

## TableCell

Each `TableCell` holds a list of blocks, so cells can contain formatted text:

| Property | Description |
|---|---|
| `Blocks` | List of blocks (paragraphs) in this cell |
| `ColumnSpan` | Number of columns this cell spans |
| `RowSpan` | Number of rows this cell spans |
| `IsMerged` | `true` for a cell covered by another cell's span |
| `Background` | Optional `EditorColor` background |
| `Border` | Optional `EditorBorder` (per-side widths) |

`TableBlock.Style` (`TableStyle`) has `BorderWidth`, `BorderColor`, `CellPadding`, `HasHeaderRow` and `HeaderBackground`.

> The renderer does not yet draw cell backgrounds, per-cell borders, `TableStyle` values or spans: tables are drawn with a 1-unit border, 4-unit padding, and every cell in its own grid slot. These properties are kept in the model for your own use.

## Column Widths and Row Heights

Drag a column or row border in the editor to resize it. In the model, `TableBlock.ColumnWidths` holds one width per column (logical units); when it is empty or doesn't match the column count, columns share the available width equally. `TableRow.Height` sets a minimum row height.

```csharp
table.ColumnWidths.Clear();
table.ColumnWidths.AddRange(new[] { 120f, 300f, 180f });
```

## Adding and Removing Rows/Columns

Undoable commands act on the cell containing the caret: `InsertRowAboveCommand`, `InsertRowBelowCommand`, `DeleteRowCommand`, `InsertColumnLeftCommand`, `InsertColumnRightCommand`, `DeleteColumnCommand` and `DeleteTableCommand` (also available from the toolbar's Table menu).

`TableEngine` changes a table directly:

```csharp
// Insert a row after index 1
TableEngine.InsertRow(table, afterRowIndex: 1);

// Insert a column before index 0
TableEngine.InsertColumn(table, afterColIndex: -1);

// Delete the second row (a table keeps at least one row)
TableEngine.DeleteRow(table, 1);

// Delete the third column (a table keeps at least one column)
TableEngine.DeleteColumn(table, 2);
```

## Merging and Splitting Cells

```csharp
// Merge a 2-row, 3-column block starting at (0,0)
TableEngine.MergeCells(table, startRow: 0, startCol: 0, rowSpan: 2, colSpan: 3);

// Split a merged cell back into individual cells
TableEngine.SplitCell(table, table.GetCell(0, 0)!);
```

Merges are stored in the model and exported to HTML as `colspan`/`rowspan`; see the note above about rendering.

## Keyboard Navigation

When the caret is inside a table cell:

| Key | Action |
|---|---|
| `Tab` | Move to next cell (wraps to next row; leaves the table after the last cell) |
| `Shift+Tab` | Move to previous cell |
| `Ctrl/Cmd+Alt+Up` / `Down` | Insert a row above / below |
| `Ctrl/Cmd+Alt+Left` / `Right` | Insert a column left / right |
| `Ctrl/Cmd+Alt+Backspace` | Delete the current row |
| `Ctrl/Cmd+Alt+Delete` | Delete the current column |
| `Ctrl/Cmd+Shift+Delete` (or `Backspace`) | Delete the table |
| Arrow keys | Navigate characters within the cell |

`Enter` does not split a paragraph inside a cell.

## HTML Export

Tables export as standard HTML; each cell's content is written as its paragraphs (line breaks added here for readability):

```html
<table>
  <tr><td><p>Header 1</p></td><td><p>Header 2</p></td></tr>
  <tr><td><p>Cell A</p></td><td><p>Cell B</p></td></tr>
</table>
```

In Markdown (with GFM extensions on), the first row becomes the header row of a pipe table.
