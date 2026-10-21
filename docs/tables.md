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

## Table and Cell Styling

`TableBlock.Style` (`TableStyle`) sets table-wide defaults:

| Property | Default | Description |
|---|---|---|
| `BorderWidth` | `1` | Width of the grid lines between and around cells (`0` for no grid) |
| `BorderColor` | `TableStyle.DefaultBorderColor` (grey 160,160,160) | Colour of the grid lines and per-cell borders |
| `CellPadding` | `4` | Space between a cell's edge and its content, on every side |
| `HasHeaderRow` | `false` | Fills the first row with `HeaderBackground` |
| `HeaderBackground` | `null` | Header-row fill; `null` uses `TableStyle.DefaultHeaderBackground`, a faint tint |

Per cell, `Background` fills the cell behind its content (it wins over the header-row fill), and `Border` overrides the width of each side. A cell without a `Border` uses `BorderWidth` on every side. Where two cells share an edge, the wider of their two sides is drawn, in `BorderColor`, so a side set to `0` disappears only when the neighbouring cell's side is `0` too. Borders are drawn on the grid lines, which are always `BorderWidth` apart; a wider border extends into the cell's padding.

```csharp
table.Style.BorderColor = new EditorColor(200, 200, 200);
table.Style.CellPadding = 8f;
table.Style.HasHeaderRow = true;
table.Style.HeaderBackground = new EditorColor(235, 240, 250);

var cell = table.GetCell(1, 2)!;
cell.Background = new EditorColor(255, 250, 205);
cell.Border = new EditorBorder(top: 0f, right: 0f, bottom: 2f, left: 0f);
```

These are model properties: after changing them on a table that is already displayed, raise `Document.NotifyChanged` (as `TableEngine` does) so the editor lays it out again. `TableEngine.SetCellBackground` sets a cell background and notifies for you. None of them are written to or read from HTML or Markdown.

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

A cell with `ColumnSpan`/`RowSpan` greater than 1 is drawn over the combined area of the slots it covers, including the grid lines between them, and taps anywhere in that area place the caret in it. The covered cells stay in `Row.Cells` (so the grid stays rectangular) but are not drawn, `Tab`/`Shift+Tab` skip them, and a caret placed in one (for example by `Ctrl+End` when the last slot is covered) moves to the spanning cell, so typing never goes into a hidden cell. Grid lines inside a spanning cell can't be dragged to resize. `TableBlock.GetSpanAnchor(row, col)` returns the cell that occupies a slot. A spanning cell taller than the rows it covers makes the last of those rows taller. Spans that run past the table's edge, or into a slot an earlier cell already covers, are cut short.

Merges are exported to HTML as `colspan`/`rowspan`.

## Keyboard Navigation

When the caret is inside a table cell:

| Key | Action |
|---|---|
| `Tab` | Move to next cell (wraps to next row; skips cells covered by a merge; leaves the table after the last cell) |
| `Shift+Tab` | Move to previous cell |
| `Ctrl/Cmd+Alt+Up` / `Down` | Insert a row above / below |
| `Ctrl/Cmd+Alt+Left` / `Right` | Insert a column left / right |
| `Ctrl/Cmd+Alt+Backspace` | Delete the current row |
| `Ctrl/Cmd+Alt+Delete` | Delete the current column |
| `Ctrl/Cmd+Shift+Delete` (or `Backspace`) | Delete the table |
| Arrow keys | Navigate characters within the cell |

`Enter` does not split a paragraph inside a cell.

## Editing Cell Content

- **Character formatting** (bold, italic, font, colours, clear formatting, …) applies to the selected text in a cell. A selection that spans several cells formats every cell between its start and end cell in reading order (row by row), and a selection that covers a whole table formats all of its cells. Cells hidden by a merge are not part of a selection, so their text is neither formatted nor copied. Each change is one undo step.
- **Hyperlinks** can be inserted at the caret or over the selected text in a cell. A link stays within one cell: when the selection runs into another cell, only its part in the start cell is linked.
- **Deleting a selection** that starts in a paragraph before a table and ends in a paragraph after it deletes the table along with the selected text and joins the two paragraphs. Undo puts the table and the text back.

## HTML Export

Tables export as standard HTML; each cell's content is written as its paragraphs (line breaks added here for readability):

```html
<table>
  <tr><td><p>Header 1</p></td><td><p>Header 2</p></td></tr>
  <tr><td><p>Cell A</p></td><td><p>Cell B</p></td></tr>
</table>
```

In Markdown (with GFM extensions on), the first row becomes the header row of a pipe table.
