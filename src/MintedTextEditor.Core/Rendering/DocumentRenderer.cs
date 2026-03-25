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
        const float borderWidth = 1f;
        var borderPaint = new EditorPaint
        {
            Color = new EditorColor(160, 160, 160),
            Style = PaintStyle.Fill
        };

        int rowCount = table.RowHeights.Length;
        int colCount = table.ColumnWidths.Length;
        float totalWidth = table.ColumnWidths.Sum() + (colCount + 1) * borderWidth;
        float tableY = table.Y;

        // Top border
        context.FillRect(new EditorRect(0f, tableY, totalWidth, borderWidth), borderPaint);

        float rowY = tableY + borderWidth;
        for (int r = 0; r < rowCount; r++)
        {
            float rowHeight = table.RowHeights[r];
            float rowBottom = rowY + rowHeight + borderWidth;

            if (rowBottom < visibleTop)
            {
                rowY += rowHeight + borderWidth;
                continue;
            }

            if (rowY > visibleBottom)
                break;

            // Left border
            context.FillRect(new EditorRect(0f, rowY, borderWidth, rowHeight), borderPaint);

            float colX = borderWidth;
            for (int c = 0; c < colCount; c++)
            {
                float colWidth = table.ColumnWidths[c];

                // Render cell content lines (if any)
                if (r < table.Cells.Length && c < table.Cells[r].Length)
                {
                    var cellLayout = table.Cells[r][c];
                    float cellContentX = colX + table.CellPadding;
                    float cellContentY = rowY + table.CellPadding;

                    context.Save();
                    context.Translate(cellContentX, cellContentY);
                    context.ClipRect(new EditorRect(
                        0f, 0f,
                        colWidth - 2f * table.CellPadding,
                        rowHeight - 2f * table.CellPadding));

                    foreach (var line in cellLayout.Lines)
                    {
                        float lineTop = rowY + table.CellPadding + line.Y;
                        float lineBottom = lineTop + line.Height;
                        if (lineBottom < visibleTop) continue;
                        if (lineTop > visibleBottom) break;

                        RenderLine(line, 0f, context);
                    }

                    context.Restore();
                }

                colX += colWidth;

                // Right/vertical border
                context.FillRect(new EditorRect(colX, rowY, borderWidth, rowHeight), borderPaint);
                colX += borderWidth;
            }

            rowY += rowHeight;

            // Bottom horizontal border for this row
            context.FillRect(new EditorRect(0f, rowY, totalWidth, borderWidth), borderPaint);
            rowY += borderWidth;
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
