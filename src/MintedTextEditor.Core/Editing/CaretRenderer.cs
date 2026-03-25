using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Layout;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Editing;

/// <summary>
/// Renders the caret (cursor line) at the correct pixel position using the document layout.
/// </summary>
public class CaretRenderer
{
    /// <summary>Width of the caret line in pixels.</summary>
    public float CaretWidth { get; set; } = 2f;

    /// <summary>Color of the caret.</summary>
    public EditorColor CaretColor { get; set; } = EditorColor.Black;

    /// <summary>
    /// Draws the caret at its current position within the layout.
    /// </summary>
    public void Render(Caret caret, DocumentLayout layout, Document.Document document, IDrawingContext context)
    {
        if (!caret.IsVisible) return;

        var rect = GetCaretRect(caret.Position, layout, document, context);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        var paint = new EditorPaint
        {
            Color = CaretColor,
            Style = PaintStyle.Fill,
            IsAntiAlias = true
        };

        context.FillRect(rect, paint);
    }

    /// <summary>
    /// Computes the pixel rectangle for the caret at the given document position.
    /// Coordinates are in document space (not scroll-adjusted).
    /// </summary>
    public EditorRect GetCaretRect(DocumentPosition position, DocumentLayout layout, Document.Document document, IDrawingContext context)
    {
        if (layout.Blocks.Count == 0)
            return new EditorRect(0, 0, CaretWidth, 14f);

        int blockIndex = Math.Clamp(position.BlockIndex, 0, layout.Blocks.Count - 1);
        var block = layout.Blocks[blockIndex];

        if (block.Lines.Count == 0 && block is not TableLayoutBlock)
            return new EditorRect(0, block.Y, CaretWidth, 14f);

        // ── Table cell caret ─────────────────────────────────────────────────────
        if (position.IsInTableCell && block is TableLayoutBlock tableLayout)
            return GetTableCaretRect(position, block.Y, tableLayout, document, context);

        if (block.Lines.Count == 0)
            return new EditorRect(0, block.Y, CaretWidth, 14f);

        // Find the line and X position for this document position
        float caretX = 0;
        LayoutLine? targetLine = null;

        foreach (var line in block.Lines)
        {
            foreach (var run in line.Runs)
            {
                if (run.SourceInline is null) continue;

                // Check if this run contains the caret position
                var paragraph = document.Blocks[blockIndex] as Paragraph;
                if (paragraph is null) continue;

                int inlineIndex = -1;
                for (int i = 0; i < paragraph.Inlines.Count; i++)
                {
                    if (ReferenceEquals(paragraph.Inlines[i], run.SourceInline))
                    {
                        inlineIndex = i;
                        break;
                    }
                }

                if (inlineIndex != position.InlineIndex) continue;

                // Check if the offset falls within this run's source range
                int runStart = run.SourceOffset;
                int runEnd = run.SourceOffset + run.Text.Length;

                if (position.Offset >= runStart && position.Offset <= runEnd)
                {
                    int offsetInRun = position.Offset - run.SourceOffset;
                    if (run.IsImage)
                    {
                        // Image text is a single "\uFFFC" placeholder — use actual pixel width
                        // when offset 1 (after the image), else place caret at image left edge.
                        caretX = offsetInRun >= run.Text.Length ? run.X + run.Width : run.X;
                    }
                    else if (offsetInRun > 0)
                    {
                        var paint = CreatePaintFromStyle(run.Style);
                        var measured = context.MeasureText(run.Text[..offsetInRun], paint);
                        caretX = run.X + measured.Width;
                    }
                    else
                    {
                        caretX = run.X;
                    }

                    targetLine = line;
                    break;
                }
            }

            if (targetLine is not null) break;
        }

        // If we couldn't find the exact run, place caret at start of first line
        // or end of last run in the last line
        if (targetLine is null)
        {
            targetLine = FindLineForPosition(position, block, document);
            if (targetLine is null)
                targetLine = block.Lines[0];

            // Position at end of line if we couldn't locate precisely
            if (targetLine.Runs.Count > 0 && position.Offset > 0)
            {
                var lastRun = targetLine.Runs[^1];
                caretX = lastRun.X + lastRun.Width;
            }
            else
            {
                // For RTL empty lines DefaultCaretX holds the right-edge position;
                // for LTR empty lines it is 0 (the default value).
                caretX = targetLine.DefaultCaretX;
            }
        }

        float caretY = block.Y + targetLine.Y;
        return new EditorRect(caretX, caretY, CaretWidth, targetLine.Height);
    }

    /// <summary>
    /// Computes the caret rect for a position inside a table cell.
    /// </summary>
    private EditorRect GetTableCaretRect(
        DocumentPosition position, float tableBlockY,
        TableLayoutBlock tableLayout, Document.Document document, IDrawingContext context)
    {
        const float borderWidth = 1f;
        int row = Math.Clamp(position.CellRow, 0, tableLayout.Cells.Length - 1);
        int col = Math.Clamp(position.CellCol, 0, tableLayout.Cells[row].Length - 1);

        // Compute row content origin Y (relative to table block)
        float cellOriginY = borderWidth;
        for (int r = 0; r < row; r++)
            cellOriginY += tableLayout.RowHeights[r] + borderWidth;
        cellOriginY += tableLayout.CellPadding;

        // Compute col content origin X
        float cellOriginX = borderWidth;
        for (int c = 0; c < col; c++)
            cellOriginX += tableLayout.ColumnWidths[c] + borderWidth;
        cellOriginX += tableLayout.CellPadding;

        var cellBlock = tableLayout.Cells[row][col];
        if (cellBlock.Lines.Count == 0)
            return new EditorRect(cellOriginX, tableBlockY + cellOriginY, CaretWidth, 14f);

        // Get the paragraph for the cell
        var docTable = document.Blocks[position.BlockIndex] as TableBlock;
        var docCell = docTable?.GetCell(row, col);
        var cellPara = docCell?.Blocks.Count > 0 ? docCell.Blocks[0] as Paragraph : null;
        if (cellPara is null)
            return new EditorRect(cellOriginX, tableBlockY + cellOriginY, CaretWidth, 14f);

        // Find the target line and caret X within the cell
        float caretX = cellOriginX;
        LayoutLine? targetLine = null;

        foreach (var line in cellBlock.Lines)
        {
            foreach (var run in line.Runs)
            {
                if (run.SourceInline is null) continue;

                int inlineIndex = -1;
                for (int i = 0; i < cellPara.Inlines.Count; i++)
                {
                    if (ReferenceEquals(cellPara.Inlines[i], run.SourceInline)) { inlineIndex = i; break; }
                }
                if (inlineIndex != position.InlineIndex) continue;

                int runStart = run.SourceOffset;
                int runEnd = run.SourceOffset + run.Text.Length;
                if (position.Offset < runStart || position.Offset > runEnd) continue;

                int offsetInRun = position.Offset - run.SourceOffset;
                if (run.IsImage)
                    caretX = cellOriginX + (offsetInRun >= run.Text.Length ? run.X + run.Width : run.X);
                else if (offsetInRun > 0)
                {
                    var paint = CreatePaintFromStyle(run.Style);
                    caretX = cellOriginX + run.X + context.MeasureText(run.Text[..offsetInRun], paint).Width;
                }
                else
                    caretX = cellOriginX + run.X;

                targetLine = line;
                break;
            }
            if (targetLine is not null) break;
        }

        if (targetLine is null)
        {
            targetLine = cellBlock.Lines[0];
            if (targetLine.Runs.Count > 0 && position.Offset > 0)
            {
                var lastRun = targetLine.Runs[^1];
                caretX = cellOriginX + lastRun.X + lastRun.Width;
            }
            else
                caretX = cellOriginX + targetLine.DefaultCaretX;
        }

        // line.Y is relative to cell content origin (after TextLayoutEngine fix)
        float caretY = tableBlockY + cellOriginY + targetLine.Y;
        return new EditorRect(caretX, caretY, CaretWidth, targetLine.Height);
    }

    /// <summary>
    /// Computes the X coordinate of the caret for the given position.
    /// Used to set the preferred X for vertical navigation.
    /// </summary>
    public float GetCaretX(DocumentPosition position, DocumentLayout layout, Document.Document document, IDrawingContext context)
    {
        var rect = GetCaretRect(position, layout, document, context);
        return rect.X;
    }

    private static LayoutLine? FindLineForPosition(DocumentPosition position, LayoutBlock block, Document.Document document)
    {
        if (block.Lines.Count == 0) return null;

        var paragraph = document.Blocks[position.BlockIndex] as Paragraph;
        if (paragraph is null) return block.Lines[0];

        // For an empty paragraph or position at (_, 0, 0), return first line
        if (paragraph.Inlines.Count == 0 || (position.InlineIndex == 0 && position.Offset == 0))
            return block.Lines[0];

        // Find the line that contains runs from the target inline
        foreach (var line in block.Lines)
        {
            foreach (var run in line.Runs)
            {
                if (run.SourceInline is null) continue;
                int inlineIdx = paragraph.Inlines.IndexOf(run.SourceInline);
                if (inlineIdx == position.InlineIndex)
                {
                    int runEnd = run.SourceOffset + run.Text.Length;
                    if (position.Offset >= run.SourceOffset && position.Offset <= runEnd)
                        return line;
                }
            }
        }

        // Fallback: last line
        return block.Lines[^1];
    }

    private static EditorPaint CreatePaintFromStyle(TextStyle style)
    {
        return new EditorPaint
        {
            Color = style.TextColor,
            IsAntiAlias = true,
            Font = new EditorFont(style.FontFamily, style.FontSize, style.IsBold, style.IsItalic)
        };
    }
}
