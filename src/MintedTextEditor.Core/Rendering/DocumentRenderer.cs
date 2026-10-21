using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Layout;

namespace MintedTextEditor.Core.Rendering;

/// <summary>
/// Renders a <see cref="DocumentLayout"/> to an <see cref="IDrawingContext"/>.
/// Handles scrolling, viewport clipping, text run drawing, and highlight backgrounds.
/// </summary>
public class DocumentRenderer
{
    private const float IndentWidth = 24f;
    private const float BlockQuoteBorderWidth = 4f;
    private readonly PaintCache _paintCache = new();

    /// <summary>Vertical scroll offset in pixels.</summary>
    public float ScrollOffset { get; set; }

    /// <summary>Background color for the document area.</summary>
    public EditorColor BackgroundColor { get; set; } = EditorColor.White;

    /// <summary>Default text color when a style has no explicit color set.</summary>
    public EditorColor DefaultTextColor { get; set; } = EditorColor.Black;

    /// <summary>
    /// When true, text runs using the framework default black color are rendered with
    /// <see cref="DefaultTextColor"/> so theme switches (for example dark mode) remain legible.
    /// </summary>
    public bool PreferThemeTextColorForDefaultBlack { get; set; } = true;

    /// <summary>Viewport width, used for block-level decorations.</summary>
    public float ViewportWidth { get; set; }

    // ─── Scrollbar ────────────────────────────────────────────────────────────

    /// <summary>When <c>true</c>, a thin overlay scrollbar is rendered.</summary>
    public bool ShowScrollbar { get; set; } = true;

    /// <summary>Width of the scrollbar thumb/track strip in logical pixels.</summary>
    public float ScrollbarWidth { get; set; } = 6f;

    /// <summary>Scrollbar track color.</summary>
    public EditorColor ScrollbarTrackColor { get; set; } = new EditorColor(240, 240, 240);

    /// <summary>Scrollbar thumb color.</summary>
    public EditorColor ScrollbarThumbColor { get; set; } = new EditorColor(160, 160, 160);

    // ─── Line numbers ────────────────────────────────────────────────────────

    /// <summary>When <c>true</c>, a line-number gutter is rendered on the left.</summary>
    public bool ShowLineNumbers { get; set; }

    /// <summary>Width of the line-number gutter in logical pixels.</summary>
    public float LineNumbersGutterWidth { get; set; } = 48f;

    /// <summary>Background fill of the gutter.</summary>
    public EditorColor LineNumbersGutterColor { get; set; } = new EditorColor(248, 248, 248);

    /// <summary>Color used to draw the line-number digits.</summary>
    public EditorColor LineNumbersTextColor { get; set; } = new EditorColor(150, 150, 150);

    /// <summary>
    /// Optional action invoked within the clipped/translated coordinate space immediately
    /// before text is rendered. Use this to draw selection highlights or other layers that
    /// must appear behind text but in front of the background.
    /// </summary>
    public Action<IDrawingContext>? PreTextLayer { get; set; }

    /// <summary>
    /// Optional callback to resolve an image source string to a platform image object
    /// (e.g. <c>SKBitmap</c>) understood by <see cref="IDrawingContext.DrawImage"/>.
    /// When null, image runs fall back to a grey placeholder rectangle.
    /// </summary>
    public Func<string, object?>? ImageResolver { get; set; }

    /// <summary>
    /// Render the given layout into the drawing context within the specified viewport.
    /// </summary>
    public void Render(DocumentLayout layout, EditorRect viewport, IDrawingContext context)
    {
        ViewportWidth = viewport.Width;
        float visibleTop = ScrollOffset;
        float visibleBottom = ScrollOffset + viewport.Height;

        // Draw background
        var bgPaint = new EditorPaint { Color = BackgroundColor, Style = PaintStyle.Fill };
        context.FillRect(viewport, bgPaint);

        // ── Line number gutter ───────────────────────────────────────────────
        if (ShowLineNumbers)
            RenderLineNumbersGutter(layout, viewport, context);

        context.Save();
        context.ClipRect(viewport);
        context.Translate(viewport.X, viewport.Y - ScrollOffset);

        // Pre-text layer (e.g. selection highlights) — drawn before text so highlights appear behind.
        PreTextLayer?.Invoke(context);

        foreach (var block in layout.Blocks)
        {
            float blockBottom = block.Y + block.TotalHeight;
            float blockTop = block.Y;

            // Cull blocks entirely outside the visible region
            if (blockBottom < ScrollOffset) continue;
            if (blockTop > ScrollOffset + viewport.Height) break;

            RenderBlock(block, visibleTop, visibleBottom, context);
        }

        context.Restore();

        // ── Overlay scrollbar ────────────────────────────────────────────────
        if (ShowScrollbar && layout.TotalHeight > viewport.Height)
            RenderScrollbar(layout, viewport, context);
    }

    private void RenderBlock(LayoutBlock block, float visibleTop, float visibleBottom, IDrawingContext context)
    {
        if (block is TableLayoutBlock tableBlock)
        {
            RenderTable(tableBlock, visibleTop, visibleBottom, context);
            return;
        }

        var pStyle = block.ParagraphStyle;

        // Block quote: tinted background + left border
        if (pStyle?.IsBlockQuote == true)
        {
            var bgRect = new EditorRect(0, block.Y, ViewportWidth, block.TotalHeight);
            var bgPaint = new EditorPaint
            {
                Color = new EditorColor(0, 0, 0, 20),   // subtle dark tint
                Style = PaintStyle.Fill
            };
            context.FillRect(bgRect, bgPaint);

            var borderRect = new EditorRect(0, block.Y, BlockQuoteBorderWidth, block.TotalHeight);
            var borderPaint = new EditorPaint
            {
                Color = new EditorColor(100, 100, 100, 200),
                Style = PaintStyle.Fill
            };
            context.FillRect(borderRect, borderPaint);
        }

        foreach (var line in block.Lines)
        {
            float lineTop = block.Y + line.Y;
            float lineBottom = lineTop + line.Height;
            if (lineBottom < visibleTop) continue;
            if (lineTop > visibleBottom) break;

            RenderLine(line, block.Y, context);
        }

        // List decorations: drawn after all lines so we can use first-line metrics
        if (pStyle != null && pStyle.ListType != ListType.None && block.Lines.Count > 0)
        {
            RenderListGlyph(block, pStyle, context);
        }
    }

    private void RenderListGlyph(LayoutBlock block, ParagraphStyle pStyle, IDrawingContext context)
    {
        var firstLine = block.Lines[0];
        float glyphX = pStyle.IndentLevel * IndentWidth;
        float glyphY = block.Y + firstLine.Y + firstLine.Baseline;

        string glyphText = pStyle.ListType == ListType.Bullet
            ? "\u2022"   // bullet •
            : $"{block.ListNumber}.";

        var glyphPaint = new EditorPaint
        {
            Color = DefaultTextColor,
            Font = new EditorFont(TextStyle.Default.FontFamily, TextStyle.Default.FontSize, false, false),
            Style = PaintStyle.Fill,
            IsAntiAlias = true
        };

        context.DrawText(glyphText, glyphX, glyphY, glyphPaint);
    }

    // ─── Table ────────────────────────────────────────────────────────────────

    private void RenderTable(TableLayoutBlock table, float visibleTop, float visibleBottom, IDrawingContext context)
    {
        int rowCount = table.RowHeights.Length;
        int colCount = table.ColumnWidths.Length;
        float tableY = table.Y;
        var rowTops = GridLinePositions(table.RowHeights, table.BorderWidth);

        // ── Cell backgrounds and content ──────────────────────────────────────
        for (int r = 0; r < rowCount; r++)
        {
            if (tableY + rowTops[r] + table.BorderWidth > visibleBottom)
                break;

            for (int c = 0; c < colCount; c++)
            {
                var info = table.GetCellInfo(r, c);
                if (info.IsCovered)
                    continue;

                // Cheap cull on the rows the cell covers before computing its exact bounds.
                int lastRow = Math.Min(rowCount, r + Math.Max(1, info.RowSpan));
                if (tableY + rowTops[lastRow] < visibleTop)
                    continue;

                var bounds = table.GetCellBounds(r, c);
                float cellTop = tableY + bounds.Y;
                float cellBottom = cellTop + bounds.Height;
                if (cellBottom < visibleTop || cellTop > visibleBottom)
                    continue;

                if (info.Background is { } background)
                {
                    context.FillRect(
                        new EditorRect(bounds.X, cellTop, bounds.Width, bounds.Height),
                        new EditorPaint { Color = background, Style = PaintStyle.Fill });
                }

                if (r >= table.Cells.Length || c >= table.Cells[r].Length)
                    continue;

                var cellLayout = table.Cells[r][c];
                float cellContentX = bounds.X + table.CellPadding;
                float cellContentY = cellTop + table.CellPadding;

                context.Save();
                context.Translate(cellContentX, cellContentY);
                context.ClipRect(new EditorRect(
                    0f, 0f,
                    bounds.Width - 2f * table.CellPadding,
                    bounds.Height - 2f * table.CellPadding));

                foreach (var line in cellLayout.Lines)
                {
                    float lineTop = cellContentY + line.Y;
                    float lineBottom = lineTop + line.Height;
                    if (lineBottom < visibleTop) continue;
                    if (lineTop > visibleBottom) break;

                    RenderLine(line, 0f, context);
                }

                context.Restore();
            }
        }

        RenderTableBorders(table, visibleTop, visibleBottom, context);
    }

    /// <summary>
    /// Positions of the grid lines of a table axis, relative to the table: line i sits just before
    /// column (or row) i, and the last entry is the far outer edge. Accumulated the same way as
    /// <see cref="TableLayoutBlock.GetColumnX"/> / <see cref="TableLayoutBlock.GetRowY"/>.
    /// </summary>
    private static float[] GridLinePositions(float[] sizes, float borderWidth)
    {
        var lines = new float[sizes.Length + 1];
        float pos = borderWidth;
        for (int i = 0; i <= sizes.Length; i++)
        {
            lines[i] = pos - borderWidth;
            if (i < sizes.Length)
                pos += sizes[i] + borderWidth;
        }
        return lines;
    }

    /// <summary>
    /// Draws the grid lines of a table as collapsed borders. Each edge between two slots (or between a
    /// slot and the table's outside) is drawn once, at the grid line's position, with the wider of the
    /// two adjacent cells' side widths; edges inside a spanning cell are skipped.
    /// </summary>
    private static void RenderTableBorders(TableLayoutBlock table, float visibleTop, float visibleBottom, IDrawingContext context)
    {
        int rowCount = table.RowHeights.Length;
        int colCount = table.ColumnWidths.Length;
        if (rowCount == 0 || colCount == 0)
            return;

        float g = table.BorderWidth;
        float tableY = table.Y;
        var borderPaint = new EditorPaint { Color = table.BorderColor, Style = PaintStyle.Fill };

        // Grid line positions: line i sits just before column/row i (line N is the outer far edge).
        var lineX = GridLinePositions(table.ColumnWidths, g);
        var lineY = GridLinePositions(table.RowHeights, g);

        // Width of one side of the cell occupying a slot; -1 outside the grid.
        float SideWidth(int r, int c, Func<EditorBorder, float> side)
        {
            if (r < 0 || r >= rowCount || c < 0 || c >= colCount)
                return -1f;
            var (ar, ac) = table.GetOwningCell(r, c);
            var border = table.GetCellInfo(ar, ac).Border;
            return border is null ? g : Math.Max(0f, side(border));
        }

        // Width of the horizontal edge above row r in column c (0 when it lies inside a span).
        float HorizontalEdge(int r, int c)
        {
            if (r > 0 && r < rowCount && table.GetOwningCell(r - 1, c) == table.GetOwningCell(r, c))
                return 0f;
            return Math.Max(0f, Math.Max(SideWidth(r - 1, c, b => b.Bottom), SideWidth(r, c, b => b.Top)));
        }

        // Width of the vertical edge left of column c in row r (0 when it lies inside a span).
        float VerticalEdge(int r, int c)
        {
            if (c > 0 && c < colCount && table.GetOwningCell(r, c - 1) == table.GetOwningCell(r, c))
                return 0f;
            return Math.Max(0f, Math.Max(SideWidth(r, c - 1, b => b.Right), SideWidth(r, c, b => b.Left)));
        }

        // Horizontal lines: runs of equal-width edges are drawn as one rectangle spanning the
        // grid-line junctions at both ends. Lines grow down from the grid line, except the outer
        // bottom line, which grows up so it stays inside the table's height.
        for (int r = 0; r <= rowCount; r++)
        {
            float lineTop = tableY + lineY[r];
            bool isBottom = r == rowCount;
            if (!isBottom && lineTop > visibleBottom) break;

            int c = 0;
            while (c < colCount)
            {
                float width = HorizontalEdge(r, c);
                int end = c + 1;
                while (end < colCount && HorizontalEdge(r, end) == width) end++;

                float y = isBottom ? lineTop + g - width : lineTop;
                if (width > 0f && y + width >= visibleTop && y <= visibleBottom)
                {
                    float x = lineX[c];
                    float right = lineX[end] + g;
                    context.FillRect(new EditorRect(x, y, right - x, width), borderPaint);
                }

                c = end;
            }
        }

        // Vertical lines, one rectangle per row. A junction not covered by a horizontal line
        // (between two row-spanning cells) is filled by extending the edge above it. Lines grow
        // right from the grid line, except the outer right line, which grows left so its far
        // edge stays at the table's edge.
        for (int r = 0; r < rowCount; r++)
        {
            float top = tableY + lineY[r] + g;
            float height = table.RowHeights[r];
            if (top + height + g < visibleTop) continue;
            if (top > visibleBottom) break;

            for (int c = 0; c <= colCount; c++)
            {
                float width = VerticalEdge(r, c);
                if (width <= 0f)
                    continue;

                float edgeHeight = height;
                if (r + 1 < rowCount
                    && VerticalEdge(r + 1, c) > 0f
                    && (c == 0 || HorizontalEdge(r + 1, c - 1) <= 0f)
                    && (c == colCount || HorizontalEdge(r + 1, c) <= 0f))
                    edgeHeight += g;

                float x = c == colCount ? lineX[c] + g - width : lineX[c];
                context.FillRect(new EditorRect(x, top, width, edgeHeight), borderPaint);
            }
        }
    }

    // ─── Scrollbar ────────────────────────────────────────────────────────────

    private void RenderScrollbar(DocumentLayout layout, EditorRect viewport, IDrawingContext context)    {
        float trackX      = viewport.X + viewport.Width - ScrollbarWidth;
        float trackTop    = viewport.Y;
        float trackHeight = viewport.Height;

        // Track
        var trackPaint = new EditorPaint { Color = ScrollbarTrackColor, Style = PaintStyle.Fill };
        context.FillRect(new EditorRect(trackX, trackTop, ScrollbarWidth, trackHeight), trackPaint);

        // Thumb — proportional to viewport/content ratio
        float contentHeight = layout.TotalHeight;
        float thumbHeight   = Math.Max(ScrollbarWidth * 3f,
                                       trackHeight * (trackHeight / contentHeight));
        float maxThumbTop   = trackHeight - thumbHeight;
        float scrollRatio   = contentHeight > trackHeight
                                ? ScrollOffset / (contentHeight - trackHeight)
                                : 0f;
        float thumbTop      = trackTop + scrollRatio * maxThumbTop;

        var thumbPaint = new EditorPaint { Color = ScrollbarThumbColor, Style = PaintStyle.Fill };
        context.FillRect(new EditorRect(trackX, thumbTop, ScrollbarWidth, thumbHeight), thumbPaint);
    }

    // ─── Line number gutter ───────────────────────────────────────────────────

    private void RenderLineNumbersGutter(DocumentLayout layout, EditorRect viewport, IDrawingContext context)
    {
        // Gutter background
        var gutterRect  = new EditorRect(viewport.X, viewport.Y, LineNumbersGutterWidth, viewport.Height);
        var gutterPaint = new EditorPaint { Color = LineNumbersGutterColor, Style = PaintStyle.Fill };
        context.FillRect(gutterRect, gutterPaint);

        var numPaint = _paintCache.GetTextPaint(LineNumbersTextColor, "Segoe UI", 10f, false, false);

        for (int bi = 0; bi < layout.Blocks.Count; bi++)
        {
            var block = layout.Blocks[bi];
            float blockTop    = block.Y - ScrollOffset + viewport.Y;
            float blockBottom = blockTop + block.TotalHeight;

            // Cull
            if (blockBottom < viewport.Y) continue;
            if (blockTop   > viewport.Y + viewport.Height) break;

            if (block.Lines.Count == 0) continue;

            var firstLine = block.Lines[0];
            float lineY   = blockTop + firstLine.Y + firstLine.Baseline;
            string label  = (bi + 1).ToString();
            float  labelX = viewport.X + LineNumbersGutterWidth - 6f; // right-align with 6px padding

            context.DrawText(label, labelX, lineY, numPaint);
        }
    }

    // ─── Line rendering ───────────────────────────────────────────────────────

    private void RenderLine(LayoutLine line, float blockY, IDrawingContext context)
    {
        float lineTop = blockY + line.Y;

        foreach (var run in line.Runs)
        {
            var style = run.Style;

            // Draw highlight background if set
            if (style.HighlightColor != EditorColor.Transparent)
            {
                var highlightRect = new EditorRect(run.X, lineTop, run.Width, line.Height);
                var highlightPaint = new EditorPaint
                {
                    Color = style.HighlightColor,
                    Style = PaintStyle.Fill
                };
                context.FillRect(highlightRect, highlightPaint);
            }

            // Draw text
            float effectiveFontSize = style.FontSize;
            float baselineAdjust = 0f;
            if (style.IsSubscript)
            {
                effectiveFontSize = style.FontSize * 0.65f;
                baselineAdjust = line.Height * 0.2f;
            }
            else if (style.IsSuperscript)
            {
                effectiveFontSize = style.FontSize * 0.65f;
                baselineAdjust = -(line.Height * 0.25f);
            }

            // Image run
            if (run.IsImage)
            {
                    var imgRect = new EditorRect(run.X, lineTop + (line.Height - run.Height) / 2f, run.Width, run.Height);
                object? resolvedImage = run.ImageSource is not null ? ImageResolver?.Invoke(run.ImageSource) : null;
                if (resolvedImage is not null)
                {
                    context.DrawImage(resolvedImage, imgRect);
                }
                else
                {
                    // Placeholder: grey filled rect with thin border
                    var fillPaint = new EditorPaint { Color = new EditorColor(0xCC, 0xCC, 0xCC), Style = PaintStyle.Fill };
                    var strokePaint = new EditorPaint { Color = new EditorColor(0x99, 0x99, 0x99), Style = PaintStyle.Stroke, StrokeWidth = 1f };
                    context.FillRect(imgRect, fillPaint);
                    context.DrawRect(imgRect, strokePaint);
                }
                continue;
            }

            EditorColor runTextColor = style.TextColor;
            if (runTextColor == EditorColor.Transparent ||
                (PreferThemeTextColorForDefaultBlack && runTextColor == TextStyle.Default.TextColor))
            {
                runTextColor = DefaultTextColor;
            }

            var textPaint = _paintCache.GetTextPaint(
                runTextColor,
                style.FontFamily,
                effectiveFontSize,
                style.IsBold,
                style.IsItalic);

            float textY = lineTop + line.Baseline + baselineAdjust;
            context.DrawText(run.Text, run.X, textY, textPaint);

            // Draw underline
            if (style.IsUnderline)
            {
                float underlineY = textY + 2f;
                var underlinePaint = new EditorPaint
                {
                    Color       = textPaint.Color,
                    StrokeWidth = 1f,
                    Style       = PaintStyle.Stroke,
                    IsAntiAlias = true
                };
                context.DrawLine(run.X, underlineY, run.X + run.Width, underlineY, underlinePaint);
            }

            // Draw strikethrough
            if (style.IsStrikethrough)
            {
                float strikeY = lineTop + line.Baseline * 0.65f;
                var strikePaint = new EditorPaint
                {
                    Color       = textPaint.Color,
                    StrokeWidth = 1f,
                    Style       = PaintStyle.Stroke,
                    IsAntiAlias = true
                };
                context.DrawLine(run.X, strikeY, run.X + run.Width, strikeY, strikePaint);
            }
        }
    }
}
