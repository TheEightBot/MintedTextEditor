# Tables

MintedTextEditor supports creating and editing tables with rich content in each cell.

## Inserting a Table

```csharp
// Insert a 3×3 table at the caret position
editor.InsertTable(rows: 3, columns: 3);
```

In XAML via the toolbar insert button, or programmatically via the command:

```csharp
editor.CommandRegistry.Execute("InsertTable", new InsertTableArgs(rows: 3, columns: 3));
```

## TableBlock Model

Tables are represented in the document as `TableBlock`:

```csharp
var table = (TableBlock)doc.Blocks[0];

Console.WriteLine(table.RowCount);    // number of rows
Console.WriteLine(table.ColumnCount); // number of columns

// Access a specific cell
TableCell cell = table[row: 0, column: 1];

// Each cell contains one or more Paragraph blocks
foreach (Paragraph para in cell.Paragraphs)
    Console.WriteLine(para.PlainText);
```

## TableCell

Each `TableCell` supports a full set of paragraphs, including nested rich content:

| Property | Description |
|---|---|
| `Paragraphs` | List of `Paragraph` blocks in this cell |
| `ColumnSpan` | Number of columns this cell spans |
| `RowSpan` | Number of rows this cell spans |
| `IsHeader` | Whether this cell renders as a header (`<th>`) |
| `Style` | `TableCellStyle` with padding, borders, and background |

## TableCellStyle

```csharp
var cellStyle = new TableCellStyle
{
    Padding     = new Thickness(8),
    BorderColor = new EditorColor(0xFFCCCCCC),
    BorderWidth = 1f,
    Background  = new EditorColor(0xFFF5F5F5),
};
```

## Column Widths

**Fixed width:**

```csharp
table.SetColumnWidth(columnIndex: 0, width: 120f);
```

**Proportional (percentage-based):**

```csharp
table.SetColumnWidths(new[] { 0.2f, 0.5f, 0.3f }); // must sum to 1.0
```

## Adding and Removing Rows/Columns

```csharp
// Insert a row after index 1
table.InsertRowAfter(1);

// Insert a column before index 0
table.InsertColumnBefore(0);

// Delete the second row
table.DeleteRow(1);

// Delete the third column
table.DeleteColumn(2);
```

## Merging and Splitting Cells

```csharp
// Merge cells (0,0) through (1,2) — creates a 2-row, 3-column span
table.MergeCells(startRow: 0, startColumn: 0, endRow: 1, endColumn: 2);

// Split a merged cell back into individual cells
table.SplitCell(row: 0, column: 0);
```

## Keyboard Navigation

When the caret is inside a table cell:

| Key | Action |
|---|---|
| `Tab` | Move to next cell (wraps to next row) |
| `Shift+Tab` | Move to previous cell |
| `Enter` | Insert a new paragraph within the cell |
| Arrow keys | Navigate characters within the cell |

## HTML Export

Tables export as standard HTML:

```html
<table>
  <tr>
    <th>Header 1</th>
    <th>Header 2</th>
  </tr>
  <tr>
    <td>Cell A</td>
    <td>Cell B</td>
  </tr>
</table>
```
