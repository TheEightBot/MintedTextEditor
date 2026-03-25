using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Layout;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Input;

/// <summary>
/// Maps pixel coordinates to document positions using a <see cref="DocumentLayout"/>.
/// </summary>
public static class DocumentHitTester
{
    /// <summary>
    /// Perform a hit test at the given coordinates, returning the nearest document position.
    /// Coordinates are in document space (y = 0 at document top, not affected by scroll).
    /// </summary>
    public static HitTestResult HitTest(float x, float y, DocumentLayout layout, Document.Document document, IDrawingContext context)
    {
        if (layout.Blocks.Count == 0)
            return new HitTestResult(new DocumentPosition(0, 0, 0));

        // Find the target block by Y coordinate
        bool isAfterLastBlock = false;
        var block = FindBlockAtY(y, layout, out isAfterLastBlock);
        if (block is null)
            return new HitTestResult(new DocumentPosition(0, 0, 0));

        // Table blocks require special handling since their lines live in Cells[][], not block.Lines
        if (block is TableLayoutBlock tableLayout)
            return HitTestTable(x, y, tableLayout, document, context, isAfterLastBlock);

        // Find the target line within the block
        var line = FindLineAtY(y, block);
        if (line is null)
        {
            // Fallback to first line of block
            line = block.Lines.Count > 0 ? block.Lines[0] : null;
            if (line is null)
                return new HitTestResult(new DocumentPosition(block.BlockIndex, 0, 0));
        }

        // Find position within the line at the given X
        return HitTestLine(x, line, block, document, context, isAfterLastBlock);
    }

    /// <summary>
    /// Finds the block at the given Y coordinate.
    /// If Y is below all blocks, returns the last block and sets isAfterLastBlock = true.
    /// </summary>
    private static LayoutBlock? FindBlockAtY(float y, DocumentLayout layout, out bool isAfterLastBlock)
    {
        isAfterLastBlock = false;

        if (y < 0) return layout.Blocks[0];

        for (int i = 0; i < layout.Blocks.Count; i++)
        {
            var block = layout.Blocks[i];
            if (y < block.Y + block.TotalHeight)
                return block;
        }

        // Past the end of all blocks
        isAfterLastBlock = true;
        return layout.Blocks[^1];
    }

    /// <summary>
    /// Finds the line at the given Y coordinate within a block.
    /// Line Y values are relative to the block, so we subtract block.Y from the document Y.
    /// </summary>
    private static LayoutLine? FindLineAtY(float y, LayoutBlock block)
    {
        float localY = y - block.Y;

        for (int i = 0; i < block.Lines.Count; i++)
        {
            var line = block.Lines[i];
            if (localY < line.Y + line.Height)
                return line;
        }

        // Past the end — return last line
        return block.Lines.Count > 0 ? block.Lines[^1] : null;
    }

    /// <summary>
    /// Hit-tests a specific line to find the character position at the given X coordinate.
    /// </summary>
    private static HitTestResult HitTestLine(
        float x, LayoutLine line, LayoutBlock block,
        Document.Document document, IDrawingContext context, bool isAfterLastBlock)
    {
        if (line.Runs.Count == 0)
        {
            // Empty line — position at start
            return new HitTestResult(
                new DocumentPosition(block.BlockIndex, 0, 0),
                line, null, true, isAfterLastBlock);
        }

        // Check if X is before the first run (margin click -> line start)
        var firstRun = line.Runs[0];
        if (x < firstRun.X)
        {
            var pos = RunToDocumentPosition(firstRun, 0, block.BlockIndex, document);
            return new HitTestResult(pos, line, firstRun, false, isAfterLastBlock);
        }

        // Check if X is past the last run (line end)
        var lastRun = line.Runs[^1];
        if (x >= lastRun.X + lastRun.Width)
        {
            var pos = RunToDocumentPosition(lastRun, lastRun.Text.Length, block.BlockIndex, document);
            return new HitTestResult(pos, line, lastRun, true, isAfterLastBlock);
        }

        // Find the run containing X
        foreach (var run in line.Runs)
        {
            if (x >= run.X && x < run.X + run.Width)
            {
                int charIndex = FindCharacterIndex(x, run, context);
                var pos = RunToDocumentPosition(run, charIndex, block.BlockIndex, document);
                bool atEnd = charIndex >= run.Text.Length;
                return new HitTestResult(pos, line, run, atEnd, isAfterLastBlock);
            }
        }

        // Fallback — snap to last run end
        var fallbackPos = RunToDocumentPosition(lastRun, lastRun.Text.Length, block.BlockIndex, document);
        return new HitTestResult(fallbackPos, line, lastRun, true, isAfterLastBlock);
    }

    /// <summary>
    /// Finds the character index within a run at the given X coordinate,
    /// using snap-to-nearest-character logic (midpoint of each character).
    /// </summary>
    private static int FindCharacterIndex(float x, LayoutRun run, IDrawingContext context)
    {
        var paint = CreatePaintFromStyle(run.Style);
        float relativeX = x - run.X;

        // Binary search would be more efficient but simple linear scan is fine
        // for typical run lengths and gives correct results
        for (int i = 0; i < run.Text.Length; i++)
        {
            var measured = context.MeasureText(run.Text[..(i + 1)], paint);
            float charMidpoint = (i == 0 ? 0 : context.MeasureText(run.Text[..i], paint).Width);
            charMidpoint = (charMidpoint + measured.Width) / 2f;

            if (relativeX < charMidpoint)
                return i;
        }

        return run.Text.Length;
    }

    /// <summary>
    /// Converts a run-relative character index to a DocumentPosition.
    /// </summary>
    private static DocumentPosition RunToDocumentPosition(LayoutRun run, int charIndexInRun, int blockIndex, Document.Document document)
    {
        if (run.SourceInline is null)
            return new DocumentPosition(blockIndex, 0, 0);

        // Find the inline index of the source inline in the paragraph
        if (blockIndex < document.Blocks.Count && document.Blocks[blockIndex] is Paragraph paragraph)
        {
            for (int i = 0; i < paragraph.Inlines.Count; i++)
            {
                if (ReferenceEquals(paragraph.Inlines[i], run.SourceInline))
                {
                    int offset = run.SourceOffset + charIndexInRun;
                    // Clamp offset to valid range
                    offset = Math.Clamp(offset, 0, run.SourceInline.Length);
                    return new DocumentPosition(blockIndex, i, offset);
                }
            }
        }

        return new DocumentPosition(blockIndex, 0, 0);
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

    /// <summary>
    /// Hit-tests a click at (x, y) inside a table layout block.
    /// Finds the row/column, then the line within the cell, and returns a position with CellRow/CellCol.
    /// </summary>
    private static HitTestResult HitTestTable(
        float x, float y, TableLayoutBlock tableBlock,
        Document.Document document, IDrawingContext context, bool isAfterLastBlock)
    {
        const float borderWidth = 1f;
        int blockIdx = tableBlock.BlockIndex;
        float localY = y - tableBlock.Y;

        // Find the row
        int row = tableBlock.RowHeights.Length - 1;
        float rowStartY = borderWidth;
        {
            float accY = borderWidth;
            for (int r = 0; r < tableBlock.RowHeights.Length; r++)
            {
                if (localY < accY + tableBlock.RowHeights[r] || r == tableBlock.RowHeights.Length - 1)
                {
                    row = r;
                    rowStartY = accY;
                    break;
                }
                accY += tableBlock.RowHeights[r] + borderWidth;
            }
        }

        // Find the column
        int col = tableBlock.ColumnWidths.Length - 1;
        float colStartX = borderWidth;
        {
            float accX = borderWidth;
            for (int c = 0; c < tableBlock.ColumnWidths.Length; c++)
            {
                if (x < accX + tableBlock.ColumnWidths[c] || c == tableBlock.ColumnWidths.Length - 1)
                {
                    col = c;
                    colStartX = accX;
                    break;
                }
                accX += tableBlock.ColumnWidths[c] + borderWidth;
            }
        }

        var cellBlock = tableBlock.Cells[row][col];

        // Get the paragraph from the document model for inline-index lookup
        var docTable = document.Blocks[blockIdx] as TableBlock;
        var docCell = docTable?.GetCell(row, col);
        var cellPara = docCell?.Blocks.Count > 0 ? docCell.Blocks[0] as Paragraph : null;

        if (cellPara is null || cellBlock.Lines.Count == 0)
            return new HitTestResult(new DocumentPosition(blockIdx, 0, 0, row, col));

        // Coordinates relative to cell content origin
        float cellContentY = localY - rowStartY - tableBlock.CellPadding;
        float cellContentX = x - colStartX - tableBlock.CellPadding;

        // Find the line in the cell
        LayoutLine? line = cellBlock.Lines[^1];
        foreach (var ln in cellBlock.Lines)
        {
            if (cellContentY < ln.Y + ln.Height) { line = ln; break; }
        }

        // Hit-test runs in that line using the cell paragraph for inline-index resolution
        DocumentPosition pos;
        if (line.Runs.Count == 0)
        {
            pos = new DocumentPosition(blockIdx, 0, 0, row, col);
        }
        else if (cellContentX < line.Runs[0].X)
        {
            pos = RunToDocumentPositionInCell(line.Runs[0], 0, blockIdx, cellPara, row, col);
        }
        else if (cellContentX >= line.Runs[^1].X + line.Runs[^1].Width)
        {
            var last = line.Runs[^1];
            pos = RunToDocumentPositionInCell(last, last.Text.Length, blockIdx, cellPara, row, col);
        }
        else
        {
            pos = new DocumentPosition(blockIdx, 0, 0, row, col);
            foreach (var run in line.Runs)
            {
                if (cellContentX >= run.X && cellContentX < run.X + run.Width)
                {
                    int charIdx = FindCharacterIndex(cellContentX, run, context);
                    pos = RunToDocumentPositionInCell(run, charIdx, blockIdx, cellPara, row, col);
                    break;
                }
            }
        }

        return new HitTestResult(pos, line, null, false, isAfterLastBlock);
    }

    /// <summary>
    /// Variant of <see cref="RunToDocumentPosition"/> that resolves inline index from a
    /// directly provided paragraph (used for table cells where the block is a TableBlock).
    /// Returns a position with the given <paramref name="cellRow"/>/<paramref name="cellCol"/>.
    /// </summary>
    private static DocumentPosition RunToDocumentPositionInCell(
        LayoutRun run, int charIndexInRun, int blockIndex,
        Paragraph paragraph, int cellRow, int cellCol)
    {
        if (run.SourceInline is null)
            return new DocumentPosition(blockIndex, 0, 0, cellRow, cellCol);

        for (int i = 0; i < paragraph.Inlines.Count; i++)
        {
            if (ReferenceEquals(paragraph.Inlines[i], run.SourceInline))
            {
                int offset = Math.Clamp(run.SourceOffset + charIndexInRun, 0, run.SourceInline.Length);
                return new DocumentPosition(blockIndex, i, offset, cellRow, cellCol);
            }
        }
        return new DocumentPosition(blockIndex, 0, 0, cellRow, cellCol);
    }
}
