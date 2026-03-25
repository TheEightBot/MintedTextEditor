using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Layout;

/// <summary>
/// Performs word-wrapping and line-breaking to convert a <see cref="Document.Document"/>
/// into a <see cref="DocumentLayout"/> of positioned visual lines and runs.
/// </summary>
public class TextLayoutEngine
{
    private const float IndentWidth = 24f;

    /// <summary>
    /// When <c>true</c> (default), text wraps at the viewport boundary.
    /// When <c>false</c>, lines extend horizontally without breaking.
    /// </summary>
    public bool WordWrap { get; set; } = true;

    /// <summary>Color used when laying out hyperlink text fragments.</summary>
    public EditorColor HyperlinkTextColor { get; set; } = new EditorColor(0x00, 0x66, 0xCC);

    /// <summary>When true, hyperlink text is force-underlined during layout.</summary>
    public bool UnderlineHyperlinks { get; set; } = true;

    /// <summary>
    /// Lay out the entire document for the given viewport width.
    /// </summary>
    public DocumentLayout Layout(Document.Document document, float viewportWidth, IDrawingContext context)
    {
        var layout = new DocumentLayout { ViewportWidth = viewportWidth };
        float documentY = 0;
        int numberListCounter = 0;

        for (int blockIndex = 0; blockIndex < document.Blocks.Count; blockIndex++)
        {
            var block = document.Blocks[blockIndex];
            var layoutBlock = LayoutBlock(block, blockIndex, viewportWidth, context);
            layoutBlock.Y = documentY;

            // Track sequential numbers for ordered lists; reset on any non-numbered paragraph
            if (block is Paragraph para)
            {
                if (para.Style.ListType == ListType.Number)
                    layoutBlock.ListNumber = ++numberListCounter;
                else
                    numberListCounter = 0;
            }

            layout.Blocks.Add(layoutBlock);
            documentY += layoutBlock.TotalHeight;
        }

        layout.TotalHeight = documentY;
        return layout;
    }

    /// <summary>
    /// Lay out a single block, producing a <see cref="Layout.LayoutBlock"/>.
    /// </summary>
    public LayoutBlock LayoutBlock(Block block, int blockIndex, float viewportWidth, IDrawingContext context)
    {
        if (block is Paragraph paragraph)
            return LayoutParagraph(paragraph, blockIndex, viewportWidth, context);

        if (block is TableBlock table)
            return LayoutTable(table, blockIndex, viewportWidth, context);

        // Fallback for unknown block types — empty block
        return new LayoutBlock { BlockIndex = blockIndex, TotalHeight = 0 };
    }

    private TableLayoutBlock LayoutTable(TableBlock table, int blockIndex, float viewportWidth, IDrawingContext context)
    {
        int rowCount = table.RowCount;
        int colCount = table.ColumnCount;

        if (rowCount == 0 || colCount == 0)
            return new TableLayoutBlock { BlockIndex = blockIndex, TotalHeight = 0 };

        const float cellPadding = 4f;
        const float borderWidth = 1f;
        const float minCellHeight = 20f;

        float[] colWidths;
        if (table.ColumnWidths.Count == colCount && table.ColumnWidths.All(w => w > 1f))
        {
            colWidths = table.ColumnWidths.ToArray();
        }
        else
        {
            // Equal column widths, accounting for all vertical border lines.
            float totalBorders = (colCount + 1) * borderWidth;
            float colWidth = Math.Max(1f, (viewportWidth - totalBorders) / colCount);
            colWidths = new float[colCount];
            Array.Fill(colWidths, colWidth);
        }

        var cells = new LayoutBlock[rowCount][];
        var rowHeights = new float[rowCount];

        for (int r = 0; r < rowCount; r++)
        {
            cells[r] = new LayoutBlock[colCount];
            float maxCellContentHeight = minCellHeight;

            for (int c = 0; c < colCount; c++)
            {
                var cell = table.GetCell(r, c);
                var cellLayout = new LayoutBlock { BlockIndex = blockIndex };
                float cellContentHeight = 0f;
                float cellInnerWidth = Math.Max(1f, colWidths[c] - 2f * cellPadding);

                if (cell is not null)
                {
                    foreach (var cellBlock in cell.Blocks)
                    {
                        var lb = LayoutBlock(cellBlock, blockIndex, cellInnerWidth, context);
                        lb.Y = cellContentHeight;
                        foreach (var line in lb.Lines)
                        {
                            line.Y += lb.Y; // adjust to be relative to cell content top
                            cellLayout.Lines.Add(line);
                        }
                        cellContentHeight += lb.TotalHeight;
                    }
                }

                cellContentHeight = Math.Max(cellContentHeight, minCellHeight);
                cellLayout.TotalHeight = cellContentHeight;
                cells[r][c] = cellLayout;
                maxCellContentHeight = Math.Max(maxCellContentHeight, cellContentHeight);
            }

            float computedRowHeight = maxCellContentHeight + 2f * cellPadding;
            float explicitRowHeight = r < table.Rows.Count ? table.Rows[r].Height : 0f;
            rowHeights[r] = explicitRowHeight > 1f
                ? Math.Max(explicitRowHeight, computedRowHeight)
                : computedRowHeight;
        }

        float tableHeight = rowHeights.Sum() + borderWidth * (rowCount + 1);

        return new TableLayoutBlock
        {
            BlockIndex = blockIndex,
            ColumnWidths = colWidths,
            RowHeights = rowHeights,
            Cells = cells,
            CellPadding = cellPadding,
            TotalHeight = tableHeight
        };
    }

    private LayoutBlock LayoutParagraph(Paragraph paragraph, int blockIndex, float viewportWidth, IDrawingContext context)
    {
        var style = paragraph.Style;
        bool isRtl = style.Direction == TextDirection.RightToLeft;
        float indent = style.IndentLevel * IndentWidth;
        // List items need extra room on the left for the bullet/number glyph
        float listIndentOffset = style.ListType != ListType.None ? IndentWidth : 0f;

        float textStartX;
        float availableWidth;
        TextAlignment effectiveAlignment;

        if (isRtl)
        {
            // RTL: indent is taken from the right side; text flows from left edge.
            float rtlIndent = indent + listIndentOffset;
            textStartX = 0f;
            availableWidth = Math.Max(viewportWidth - rtlIndent, 1f);
            // "Left" alignment means "start-of-line" which for RTL is the right edge.
            effectiveAlignment = style.Alignment == TextAlignment.Left
                ? TextAlignment.Right
                : style.Alignment;
        }
        else
        {
            textStartX = indent + listIndentOffset;
            availableWidth = Math.Max(viewportWidth - textStartX, 1f);
            effectiveAlignment = style.Alignment;
        }

        float spaceBefore = style.SpaceBefore;
        float spaceAfter = style.SpaceAfter;

        var layoutBlock = new LayoutBlock { BlockIndex = blockIndex, ParagraphStyle = style };

        // Collect all inline fragments with their styles and measurements
        var fragments = CollectFragments(paragraph, context);

        // Perform word-wrapping (honour the WordWrap property)
        float wrapWidth = WordWrap ? availableWidth : float.MaxValue;
        var lines = WrapFragments(fragments, wrapWidth, textStartX, blockIndex, context);

        // Apply alignment (for RTL, effectiveAlignment is already Right when style is Left)
        if (effectiveAlignment != TextAlignment.Left)
            ApplyAlignment(lines, availableWidth, textStartX, effectiveAlignment);

        // Position lines vertically
        float lineY = spaceBefore;
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            line.Y = lineY;
            line.LineIndexInBlock = i;
            line.BlockIndex = blockIndex;

            // Empty visual lines should place caret at the logical text start.
            // This keeps list/indent paragraphs aligned before first typed character.
            if (line.Runs.Count == 0)
                line.DefaultCaretX = isRtl ? availableWidth : textStartX;

            // Apply line spacing
            float lineHeight = line.Height * style.LineSpacing;
            lineY += lineHeight;

            layoutBlock.Lines.Add(line);
        }

        layoutBlock.TotalHeight = lineY + spaceAfter;

        return layoutBlock;
    }

    /// <summary>
    /// Collects text fragments from a paragraph's inlines for layout.
    /// Each fragment is a segment of text with its associated style and source reference.
    /// Heading-level font size and weight overrides are applied here.
    /// </summary>
    private List<LayoutFragment> CollectFragments(Paragraph paragraph, IDrawingContext context)
    {
        var fragments = new List<LayoutFragment>();
        int headingLevel = paragraph.Style.HeadingLevel;

        foreach (var inline in paragraph.Inlines)
        {
            if (inline is TextRun textRun)
            {
                if (textRun.Text.Length == 0) continue;

                var effectiveStyle = ApplyHeadingStyle(textRun.Style, headingLevel);
                var paint = CreatePaintFromStyle(effectiveStyle);
                var text = textRun.Text;

                // Split the text into word-level fragments for wrapping
                int pos = 0;
                while (pos < text.Length)
                {
                    // Find the next word boundary
                    int wordEnd = FindNextWordEnd(text, pos);
                    string fragment = text[pos..wordEnd];
                    var size = context.MeasureText(fragment, paint);

                    fragments.Add(new LayoutFragment(
                        fragment, size.Width, size.Height,
                        textRun, effectiveStyle, pos));

                    pos = wordEnd;
                }
            }
            else if (inline is LineBreak)
            {
                fragments.Add(LayoutFragment.NewLine());
            }
            else if (inline is ImageInline image)
            {
                float width  = image.Width  > 0 ? image.Width  : 100f;
                float height = image.Height > 0 ? image.Height : 100f;
                // Pass the ImageInline as SourceInline so hit-testing resolves it correctly.
                fragments.Add(new LayoutFragment(
                    "\uFFFC", width, height, image, TextStyle.Default, 0,
                    isImage: true, imageSource: image.Source));
            }
            else if (inline is HyperlinkInline hyperlink)
            {
                // Track offset within the hyperlink's full text so caret/selection
                // positions map to (inlineIndex=hyperlink's index, offset=char within hyperlink).
                int hyperlinkOffset = 0;
                foreach (var child in hyperlink.Children)
                {
                    if (child is not TextRun linkRun || linkRun.Text.Length == 0)
                    {
                        hyperlinkOffset += child.Length;
                        continue;
                    }

                    // Inherit run style with underline and standard link blue
                    var effectiveStyle = ApplyHeadingStyle(linkRun.Style, headingLevel)
                        .WithUnderline(UnderlineHyperlinks)
                        .WithTextColor(HyperlinkTextColor);
                    var paint = CreatePaintFromStyle(effectiveStyle);
                    var text = linkRun.Text;

                    int pos = 0;
                    while (pos < text.Length)
                    {
                        int wordEnd = FindNextWordEnd(text, pos);
                        string fragment = text[pos..wordEnd];
                        var size = context.MeasureText(fragment, paint);
                        // Use 'hyperlink' as SourceInline so IndexOf(paragraph.Inlines[…])
                        // succeeds; offset is relative to the start of the HyperlinkInline.
                        fragments.Add(new LayoutFragment(fragment, size.Width, size.Height,
                            hyperlink, effectiveStyle, hyperlinkOffset + pos));
                        pos = wordEnd;
                    }
                    hyperlinkOffset += linkRun.Text.Length;
                }
            }
        }

        return fragments;
    }

    /// <summary>
    /// Finds the end of the current word (including trailing whitespace).
    /// </summary>
    private static int FindNextWordEnd(string text, int start)
    {
        int pos = start;

        // Skip non-space characters (the word itself)
        while (pos < text.Length && !char.IsWhiteSpace(text[pos]))
            pos++;

        // Include trailing whitespace (so it stays with the word for wrapping)
        while (pos < text.Length && char.IsWhiteSpace(text[pos]))
            pos++;

        // Ensure progress even for single whitespace
        if (pos == start)
            pos++;

        return pos;
    }

    /// <summary>
    /// Wraps fragments into visual lines respecting the available width.
    /// </summary>
    private List<LayoutLine> WrapFragments(
        List<LayoutFragment> fragments, float availableWidth, float indent,
        int blockIndex, IDrawingContext context)
    {
        var lines = new List<LayoutLine>();
        var currentLine = new LayoutLine { BlockIndex = blockIndex };
        float currentX = indent;
        float maxAscent = 0;
        float maxDescent = 0;
        float maxLeading = 0;

        // Get default font metrics for empty line height
        var defaultPaint = CreatePaintFromStyle(TextStyle.Default);
        var defaultMetrics = context.GetFontMetrics(defaultPaint);

        foreach (var fragment in fragments)
        {
            // Explicit line break
            if (fragment.IsNewLine)
            {
                FinalizeLayoutLine(currentLine, maxAscent, maxDescent, maxLeading, defaultMetrics);
                lines.Add(currentLine);

                currentLine = new LayoutLine { BlockIndex = blockIndex };
                currentX = indent;
                maxAscent = 0;
                maxDescent = 0;
                maxLeading = 0;
                continue;
            }

            var paint = CreatePaintFromStyle(fragment.Style);
            var metrics = context.GetFontMetrics(paint);

            // Check if fragment fits on current line
            bool fitsOnLine = currentX + fragment.Width <= indent + availableWidth;
            bool isFirstOnLine = currentLine.Runs.Count == 0;

            if (!fitsOnLine && !isFirstOnLine)
            {
                // Wrap to a new line
                FinalizeLayoutLine(currentLine, maxAscent, maxDescent, maxLeading, defaultMetrics);
                lines.Add(currentLine);

                currentLine = new LayoutLine { BlockIndex = blockIndex };
                currentX = indent;
                maxAscent = 0;
                maxDescent = 0;
                maxLeading = 0;
                isFirstOnLine = true;
            }

            // If the fragment is too wide for a full line, do character-level wrapping
            if (fragment.Width > availableWidth && isFirstOnLine && !fragment.IsImage)
            {
                CharacterWrap(fragment, availableWidth, indent, blockIndex, context,
                    ref currentLine, ref currentX, ref maxAscent, ref maxDescent, ref maxLeading,
                    lines, defaultMetrics);
                continue;
            }

            // Add the run to the current line
            var run = new LayoutRun(
                fragment.Text, currentX, fragment.Width,
                fragment.SourceInline, fragment.Style, fragment.SourceOffset,
                fragment.IsImage, fragment.ImageSource, fragment.Height);
            currentLine.Runs.Add(run);
            currentX += fragment.Width;

            // Track max metrics for line height calculation
            if (fragment.IsImage)
            {
                // Image height drives the line height: treat the image as pure descent.
                if (fragment.Height > maxDescent) maxDescent = fragment.Height;
            }
            else
            {
                if (Math.Abs(metrics.Ascent) > Math.Abs(maxAscent)) maxAscent = metrics.Ascent;
                if (metrics.Descent > maxDescent) maxDescent = metrics.Descent;
                if (metrics.Leading > maxLeading) maxLeading = metrics.Leading;
            }
        }

        // Don't forget the last line
        if (currentLine.Runs.Count > 0 || lines.Count == 0)
        {
            FinalizeLayoutLine(currentLine, maxAscent, maxDescent, maxLeading, defaultMetrics);
            lines.Add(currentLine);
        }

        return lines;
    }

    /// <summary>
    /// Performs character-level wrapping for text that is wider than the available width.
    /// </summary>
    private void CharacterWrap(
        LayoutFragment fragment, float availableWidth, float indent, int blockIndex,
        IDrawingContext context,
        ref LayoutLine currentLine, ref float currentX,
        ref float maxAscent, ref float maxDescent, ref float maxLeading,
        List<LayoutLine> lines, EditorFontMetrics defaultMetrics)
    {
        var paint = CreatePaintFromStyle(fragment.Style);
        var metrics = context.GetFontMetrics(paint);
        string text = fragment.Text;
        int charIndex = 0;

        while (charIndex < text.Length)
        {
            // Find how many characters fit on the current line
            int fitCount = FindCharacterFitCount(text, charIndex, availableWidth - (currentX - indent), paint, context);

            if (fitCount == 0 && currentLine.Runs.Count == 0)
                fitCount = 1; // Always make progress — at least one character per line

            if (fitCount == 0)
            {
                // Wrap to new line
                FinalizeLayoutLine(currentLine, maxAscent, maxDescent, maxLeading, defaultMetrics);
                lines.Add(currentLine);
                currentLine = new LayoutLine { BlockIndex = blockIndex };
                currentX = indent;
                maxAscent = 0;
                maxDescent = 0;
                maxLeading = 0;
                continue;
            }

            string chunk = text[charIndex..(charIndex + fitCount)];
            var size = context.MeasureText(chunk, paint);

            var run = new LayoutRun(
                chunk, currentX, size.Width,
                fragment.SourceInline, fragment.Style, fragment.SourceOffset + charIndex,
                fragment.IsImage, fragment.ImageSource, size.Height);
            currentLine.Runs.Add(run);
            currentX += size.Width;

            if (Math.Abs(metrics.Ascent) > Math.Abs(maxAscent)) maxAscent = metrics.Ascent;
            if (metrics.Descent > maxDescent) maxDescent = metrics.Descent;
            if (metrics.Leading > maxLeading) maxLeading = metrics.Leading;

            charIndex += fitCount;

            // If there's more text, wrap to a new line
            if (charIndex < text.Length)
            {
                FinalizeLayoutLine(currentLine, maxAscent, maxDescent, maxLeading, defaultMetrics);
                lines.Add(currentLine);
                currentLine = new LayoutLine { BlockIndex = blockIndex };
                currentX = indent;
                maxAscent = 0;
                maxDescent = 0;
                maxLeading = 0;
            }
        }
    }

    /// <summary>
    /// Finds how many characters from <paramref name="text"/> starting at <paramref name="startIndex"/>
    /// fit within <paramref name="maxWidth"/>.
    /// </summary>
    private static int FindCharacterFitCount(string text, int startIndex, float maxWidth, EditorPaint paint, IDrawingContext context)
    {
        if (maxWidth <= 0) return 0;

        int remaining = text.Length - startIndex;

        // Binary search for the maximum characters that fit
        int lo = 0, hi = remaining;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            string segment = text[startIndex..(startIndex + mid)];
            var size = context.MeasureText(segment, paint);
            if (size.Width <= maxWidth)
                lo = mid;
            else
                hi = mid - 1;
        }

        return lo;
    }

    private static void FinalizeLayoutLine(LayoutLine line, float maxAscent, float maxDescent, float maxLeading, EditorFontMetrics defaultMetrics)
    {
        if (maxAscent == 0 && maxDescent == 0)
        {
            // Empty line — use default metrics
            maxAscent = defaultMetrics.Ascent;
            maxDescent = defaultMetrics.Descent;
            maxLeading = defaultMetrics.Leading;
        }

        line.Height = maxDescent - maxAscent + maxLeading;
        line.Baseline = -maxAscent; // Baseline measured from line top (ascent is negative)
    }

    private static LayoutLine CreateEmptyLine(int blockIndex, float indent, float y, IDrawingContext context)
    {
        var paint = CreatePaintFromStyle(TextStyle.Default);
        var metrics = context.GetFontMetrics(paint);

        return new LayoutLine
        {
            Y = y,
            Height = metrics.Descent - metrics.Ascent + metrics.Leading,
            Baseline = -metrics.Ascent,
            BlockIndex = blockIndex,
            LineIndexInBlock = 0
        };
    }

    private static EditorPaint CreatePaintFromStyle(TextStyle style)
    {
        return new EditorPaint
        {
            Color = style.TextColor,
            Font = new EditorFont(style.FontFamily, style.FontSize, style.IsBold, style.IsItalic),
            Style = PaintStyle.Fill,
            IsAntiAlias = true
        };
    }

    /// <summary>
    /// Returns a copy of <paramref name="style"/> with font size and bold weight adjusted
    /// for the given heading level. Level 0 returns the style unchanged.
    /// </summary>
    private static TextStyle ApplyHeadingStyle(TextStyle style, int headingLevel)
    {
        if (headingLevel == 0) return style;
        float scaledSize = GetHeadingFontSize(headingLevel, style.FontSize);
        // H1–H4 are bold by convention; H5–H6 are regular weight
        bool bold = headingLevel >= 1 && headingLevel <= 4;
        return style.WithFontSize(scaledSize).WithBold(bold || style.IsBold);
    }

    private static float GetHeadingFontSize(int headingLevel, float baseSize) => headingLevel switch
    {
        1 => baseSize * 2.0f,
        2 => baseSize * 1.5f,
        3 => baseSize * 1.17f,
        4 => baseSize * 1.0f,
        5 => baseSize * 0.83f,
        6 => baseSize * 0.67f,
        _ => baseSize
    };

    /// <summary>
    /// Post-processes lines to apply Center, Right, or Justify alignment by
    /// shifting run X positions within the available width.
    /// </summary>
    private static void ApplyAlignment(
        List<LayoutLine> lines, float availableWidth, float textStartX, TextAlignment alignment)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Runs.Count == 0) continue;

            float lineWidth = line.Runs.Sum(r => r.Width);
            bool isLastLine = i == lines.Count - 1;

            if (alignment == TextAlignment.Justify && !isLastLine && line.Runs.Count > 1)
            {
                // Distribute extra space evenly between runs
                float extraSpace = availableWidth - lineWidth;
                float spacePerGap = extraSpace / (line.Runs.Count - 1);
                float runX = textStartX;
                var justified = new List<LayoutRun>(line.Runs.Count);
                foreach (var run in line.Runs)
                {
                    justified.Add(run.WithX(runX));
                    runX += run.Width + spacePerGap;
                }
                line.Runs.Clear();
                line.Runs.AddRange(justified);
            }
            else
            {
                float offset = alignment switch
                {
                    TextAlignment.Center  => (availableWidth - lineWidth) / 2f,
                    TextAlignment.Right   => availableWidth - lineWidth,
                    _ => 0f
                };
                if (offset == 0f) continue;

                var shifted = new List<LayoutRun>(line.Runs.Count);
                foreach (var run in line.Runs)
                    shifted.Add(run.WithX(run.X + offset));
                line.Runs.Clear();
                line.Runs.AddRange(shifted);
            }
        }
    }

    /// <summary>
    /// Internal fragment used during layout. Represents a word or element to be positioned.
    /// </summary>
    private readonly struct LayoutFragment
    {
        public string Text { get; }
        public float Width { get; }
        public float Height { get; }
        public Inline? SourceInline { get; }
        public TextStyle Style { get; }
        public int SourceOffset { get; }
        public bool IsNewLine { get; }
        public bool IsImage { get; }
        public string? ImageSource { get; }

        public LayoutFragment(string text, float width, float height,
            Inline? sourceInline, TextStyle style, int sourceOffset, bool isImage = false, string? imageSource = null)
        {
            Text = text;
            Width = width;
            Height = height;
            SourceInline = sourceInline;
            Style = style;
            SourceOffset = sourceOffset;
            IsNewLine = false;
            IsImage = isImage;
            ImageSource = imageSource;
        }

        private LayoutFragment(bool isNewLine)
        {
            Text = string.Empty;
            Width = 0;
            Height = 0;
            SourceInline = null;
            Style = TextStyle.Default;
            SourceOffset = 0;
            IsNewLine = isNewLine;
            IsImage = false;
            ImageSource = null;
        }

        public static LayoutFragment NewLine() => new(isNewLine: true);
    }
}
