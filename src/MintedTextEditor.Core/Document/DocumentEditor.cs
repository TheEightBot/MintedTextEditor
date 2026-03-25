namespace MintedTextEditor.Core.Document;

/// <summary>
/// Stateless helper for safe document mutations (insert, delete, split, merge, style).
/// All methods fire change notifications via <see cref="Document.NotifyChanged"/>.
/// </summary>
public static class DocumentEditor
{
    /// <summary>
    /// Inserts text at the given position with the specified style.
    /// Returns the new position immediately after the inserted text.
    /// </summary>
    public static DocumentPosition InsertText(Document document, DocumentPosition position, string text, TextStyle? style = null)
    {
        if (string.IsNullOrEmpty(text)) return position;

        var paragraph = GetParagraph(document, position);
        style ??= TextStyle.Default;

        // If paragraph is empty, just add a new run
        if (paragraph.Inlines.Count == 0)
        {
            paragraph.AppendRun(text, style);
            var newPos = position.With(0, text.Length);
            NotifyTextInserted(document, position, newPos);
            return newPos;
        }

        // Find the target inline
        int inlineIndex = Math.Min(position.InlineIndex, paragraph.Inlines.Count - 1);
        var inline = paragraph.Inlines[inlineIndex];

        if (inline is TextRun run)
        {
            int offset = Math.Min(position.Offset, run.Text.Length);

            if (run.Style == style)
            {
                // Same style: insert directly into existing run
                run.Text = run.Text.Insert(offset, text);
                var newPos = position.With(inlineIndex, offset + text.Length);
                NotifyTextInserted(document, position, newPos);
                return newPos;
            }
            else
            {
                // Different style: split the run and insert a new one
                var rightPart = run.Split(offset);
                var newRun = new TextRun(text, style);
                int insertIndex = inlineIndex + 1;
                paragraph.InsertInline(insertIndex, newRun);
                if (rightPart.Length > 0)
                    paragraph.InsertInline(insertIndex + 1, rightPart);

                var newPos = position.With(insertIndex, text.Length);
                NotifyTextInserted(document, position, newPos);
                return newPos;
            }
        }
        else if (inline is HyperlinkInline hyperlink)
        {
            // Insert text into the appropriate child run, keeping the caret inside the hyperlink.
            int offset = Math.Clamp(position.Offset, 0, hyperlink.Length);
            int remaining = offset;
            for (int j = 0; j < hyperlink.Children.Count; j++)
            {
                var child = hyperlink.Children[j];
                if (remaining <= child.Length)
                {
                    if (child is TextRun childRun)
                    {
                        int childOffset = Math.Clamp(remaining, 0, childRun.Text.Length);
                        if (childRun.Style == style)
                        {
                            childRun.Text = childRun.Text.Insert(childOffset, text);
                        }
                        else
                        {
                            // Different style: split child run and insert new run between parts.
                            var rightPart = childRun.Split(childOffset);
                            var newRun = new TextRun(text, style) { Parent = hyperlink.Parent };
                            hyperlink.Children.Insert(j + 1, newRun);
                            if (rightPart.Length > 0)
                            {
                                rightPart.Parent = hyperlink.Parent;
                                hyperlink.Children.Insert(j + 2, rightPart);
                            }
                        }
                    }
                    else
                    {
                        // Non-text child — insert a new run after it.
                        var newRun = new TextRun(text, style) { Parent = hyperlink.Parent };
                        hyperlink.Children.Insert(j + 1, newRun);
                    }
                    var newPos = position.With(inlineIndex, offset + text.Length);
                    NotifyTextInserted(document, position, newPos);
                    return newPos;
                }
                remaining -= child.Length;
            }
            // Append to end of hyperlink.
            if (hyperlink.Children.LastOrDefault() is TextRun lastRun && lastRun.Style == style)
            {
                lastRun.Text += text;
            }
            else
            {
                hyperlink.Children.Add(new TextRun(text, style) { Parent = hyperlink.Parent });
            }
            var endPos = position.With(inlineIndex, hyperlink.Length);
            NotifyTextInserted(document, position, endPos);
            return endPos;
        }
        else
        {
            // For non-text, non-hyperlink inlines (e.g. ImageInline), insert a new run after.
            var newRun = new TextRun(text, style);
            int insertIndex = inlineIndex + 1;
            paragraph.InsertInline(insertIndex, newRun);
            var newPos = position.With(insertIndex, text.Length);
            NotifyTextInserted(document, position, newPos);
            return newPos;
        }
    }

    /// <summary>
    /// Deletes content within the given range. Returns the collapsed position (start of range).
    /// </summary>
    public static DocumentPosition DeleteRange(Document document, TextRange range)
    {
        if (range.IsEmpty) return range.Start;

        var start = range.Start;
        var end = range.End;

        if (start.BlockIndex == end.BlockIndex)
        {
            // Single-block deletion
            DeleteWithinBlock(document, start, end);
        }
        else
        {
            // Multi-block deletion:
            // 1. Delete from start position to end of first block
            // 2. Delete entire intermediate blocks
            // 3. Delete from start of last block to end position
            // 4. Merge first and last blocks

            // Guard: don't attempt cross-block deletion involving table blocks
            if (document.Blocks[start.BlockIndex] is TableBlock || document.Blocks[end.BlockIndex] is TableBlock)
            {
                document.NotifyChanged(new DocumentChangedEventArgs(
                    DocumentChangeType.TextDeleted,
                    new TextRange(start, start)));
                return start;
            }

            var firstParagraph = GetParagraph(document, start);
            var lastParagraph = GetParagraph(document, end);

            // Trim end of first paragraph
            TrimParagraphEnd(firstParagraph, start);

            // Trim start of last paragraph
            TrimParagraphStart(lastParagraph, end);

            // Merge remaining inlines from last paragraph into first
            foreach (var inline in lastParagraph.Inlines)
                firstParagraph.AddInline(inline);

            // Remove intermediate blocks and last block (in reverse to keep indices stable)
            for (int i = end.BlockIndex; i > start.BlockIndex; i--)
            {
                document.Blocks[i].Parent = null;
                document.Blocks.RemoveAt(i);
            }
        }

        NormalizeInlines(GetParagraph(document, start));

        document.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.TextDeleted,
            new TextRange(start, start)));

        return start;
    }

    /// <summary>
    /// Splits a block at the given position, creating a new block after it.
    /// Returns the position at the start of the new block.
    /// </summary>
    public static DocumentPosition SplitBlock(Document document, DocumentPosition position)
    {
        // Table cells don't support block-level splits — return the current position unchanged.
        if (position.IsInTableCell)
            return position;

        var paragraph = GetParagraph(document, position.BlockIndex);
        var newParagraph = new Paragraph { Style = paragraph.Style.Clone() };

        // Move inlines after the split point to the new paragraph
        int inlineIndex = position.InlineIndex;
        int offset = position.Offset;

        if (inlineIndex < paragraph.Inlines.Count && paragraph.Inlines[inlineIndex] is TextRun run && offset < run.Text.Length)
        {
            // Split the run at the offset
            var rightPart = run.Split(offset);
            if (rightPart.Length > 0)
                newParagraph.AddInline(rightPart);
            inlineIndex++; // Move past the split run
        }
        else if (inlineIndex < paragraph.Inlines.Count && offset >= (paragraph.Inlines[inlineIndex]?.Length ?? 0))
        {
            inlineIndex++; // Move past fully consumed inline
        }

        // Move remaining inlines to the new paragraph
        while (inlineIndex < paragraph.Inlines.Count)
        {
            var inline = paragraph.Inlines[inlineIndex];
            paragraph.Inlines.RemoveAt(inlineIndex);
            inline.Parent = null;
            newParagraph.AddInline(inline);
        }

        document.InsertBlock(position.BlockIndex + 1, newParagraph);

        var newPos = new DocumentPosition(position.BlockIndex + 1, 0, 0);
        document.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.BlockSplit,
            new TextRange(position, newPos)));

        return newPos;
    }

    /// <summary>
    /// Merges the block at blockIndex+1 into the block at blockIndex.
    /// Returns the position at the merge point.
    /// </summary>
    public static DocumentPosition MergeBlocks(Document document, int blockIndex)
    {
        if (blockIndex < 0 || blockIndex + 1 >= document.Blocks.Count)
            throw new ArgumentOutOfRangeException(nameof(blockIndex));

        // Cannot merge table blocks with adjacent blocks
        if (document.Blocks[blockIndex] is TableBlock || document.Blocks[blockIndex + 1] is TableBlock)
            return new DocumentPosition(blockIndex, 0, 0);

        var first = GetParagraph(document, blockIndex);
        var second = GetParagraph(document, blockIndex + 1);

        int mergeInlineIndex = first.Inlines.Count;
        var mergePos = new DocumentPosition(blockIndex, mergeInlineIndex, 0);

        foreach (var inline in second.Inlines)
            first.AddInline(inline);

        document.Blocks[blockIndex + 1].Parent = null;
        document.Blocks.RemoveAt(blockIndex + 1);

        NormalizeInlines(first);

        document.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.BlocksMerged,
            new TextRange(mergePos, mergePos)));

        return mergePos;
    }

    /// <summary>
    /// Applies a style transformation to all text runs within the given range.
    /// </summary>
    public static void ApplyTextStyle(Document document, TextRange range, Func<TextStyle, TextStyle> transform)
    {
        if (range.IsEmpty) return;

        var start = range.Start;
        var end = range.End;

        for (int bi = start.BlockIndex; bi <= end.BlockIndex && bi < document.Blocks.Count; bi++)
        {
            var paragraph = document.Blocks[bi] as Paragraph;
            if (paragraph == null) continue;

            for (int ii = 0; ii < paragraph.Inlines.Count; ii++)
            {
                if (paragraph.Inlines[ii] is not TextRun run) continue;

                bool isFirst = bi == start.BlockIndex && ii == start.InlineIndex;
                bool isLast = bi == end.BlockIndex && ii == end.InlineIndex;

                if (isFirst && isLast)
                {
                    // Range within a single run: split if needed.
                    // Break immediately — ApplyStyleToRunSegment may insert new runs into
                    // the Inlines list; those runs must not be re-processed by this loop.
                    ApplyStyleToRunSegment(paragraph, ii, start.Offset, end.Offset, transform);
                    break;
                }
                else if (isFirst)
                {
                    ApplyStyleToRunSegment(paragraph, ii, start.Offset, run.Text.Length, transform);
                }
                else if (isLast)
                {
                    ApplyStyleToRunSegment(paragraph, ii, 0, end.Offset, transform);
                }
                else if (bi > start.BlockIndex && bi < end.BlockIndex)
                {
                    // Entire intermediate block
                    run.Style = transform(run.Style);
                }
                else if (bi == start.BlockIndex && ii > start.InlineIndex)
                {
                    run.Style = transform(run.Style);
                }
                else if (bi == end.BlockIndex && ii < end.InlineIndex)
                {
                    run.Style = transform(run.Style);
                }
            }
        }

        document.NotifyChanged(new DocumentChangedEventArgs(DocumentChangeType.StyleChanged, range));
    }

    /// <summary>
    /// Applies a paragraph style transformation to all paragraphs within the range.
    /// </summary>
    public static void ApplyParagraphStyle(Document document, TextRange range, Action<ParagraphStyle> transform)
    {
        for (int bi = range.Start.BlockIndex; bi <= range.End.BlockIndex && bi < document.Blocks.Count; bi++)
        {
            if (document.Blocks[bi] is Paragraph paragraph)
                transform(paragraph.Style);
        }

        document.NotifyChanged(new DocumentChangedEventArgs(DocumentChangeType.StyleChanged, range));
    }

    // ── Private helpers ──────────────────────────────────────────

    /// <summary>
    /// Extracts the plain text content covered by <paramref name="range"/>.
    /// Paragraphs are separated by a newline character.
    /// </summary>
    public static string GetSelectedText(Document document, TextRange range)
    {
        if (range.IsEmpty) return string.Empty;

        var start = range.Start;
        var end   = range.End;
        var sb    = new System.Text.StringBuilder();

        for (int bi = start.BlockIndex; bi <= end.BlockIndex && bi < document.Blocks.Count; bi++)
        {
            if (bi > start.BlockIndex)
                sb.Append('\n');

            if (document.Blocks[bi] is not Paragraph para) continue;

            int startInline = bi == start.BlockIndex ? start.InlineIndex : 0;
            int endInline   = bi == end.BlockIndex   ? end.InlineIndex   : para.Inlines.Count - 1;

            for (int ii = startInline; ii <= endInline && ii < para.Inlines.Count; ii++)
            {
                if (para.Inlines[ii] is not TextRun run) continue;

                int startOffset = bi == start.BlockIndex && ii == startInline ? start.Offset : 0;
                int endOffset   = bi == end.BlockIndex   && ii == endInline   ? end.Offset   : run.Text.Length;

                startOffset = Math.Clamp(startOffset, 0, run.Text.Length);
                endOffset   = Math.Clamp(endOffset,   0, run.Text.Length);
                if (startOffset < endOffset)
                    sb.Append(run.Text, startOffset, endOffset - startOffset);
            }
        }

        return sb.ToString();
    }

    private static Paragraph GetParagraph(Document document, int blockIndex)
    {
        if (blockIndex < 0 || blockIndex >= document.Blocks.Count)
            throw new ArgumentOutOfRangeException(nameof(blockIndex));
        return document.Blocks[blockIndex] as Paragraph
            ?? throw new InvalidOperationException($"Block at index {blockIndex} is not a Paragraph.");
    }

    /// <summary>
    /// Returns the paragraph for the given position, routing through table-cell context when needed.
    /// </summary>
    private static Paragraph GetParagraph(Document document, DocumentPosition position)
    {
        if (position.IsInTableCell)
        {
            if (position.BlockIndex < 0 || position.BlockIndex >= document.Blocks.Count)
                throw new ArgumentOutOfRangeException(nameof(position));
            var table = document.Blocks[position.BlockIndex] as TableBlock
                ?? throw new InvalidOperationException($"Block at index {position.BlockIndex} is not a TableBlock.");
            var cell = table.GetCell(position.CellRow, position.CellCol)
                ?? throw new InvalidOperationException($"Table cell ({position.CellRow},{position.CellCol}) not found.");
            if (cell.Blocks.Count == 0)
                throw new InvalidOperationException($"Table cell ({position.CellRow},{position.CellCol}) has no paragraphs.");
            return cell.Blocks[0] as Paragraph
                ?? throw new InvalidOperationException($"Table cell ({position.CellRow},{position.CellCol}) first block is not a Paragraph.");
        }
        return GetParagraph(document, position.BlockIndex);
    }

    /// <summary>
    /// Resolves a (potentially stale) <see cref="DocumentPosition"/> to a canonical position whose
    /// <see cref="DocumentPosition.InlineIndex"/> and <see cref="DocumentPosition.Offset"/> are valid
    /// for the current document.  This is typically needed after style operations that split or merge
    /// <see cref="TextRun"/> objects, which may shift inline indices.
    /// <para>
    /// The method treats the position as a logical "absolute character offset from the start of the
    /// paragraph/cell" and remaps it to the correct inline and offset within that inline.
    /// </para>
    /// </summary>
    public static DocumentPosition NormalizePosition(Document document, DocumentPosition position)
    {
        Paragraph paragraph;
        try
        {
            paragraph = GetParagraph(document, position);
        }
        catch
        {
            // Block or cell no longer exists – clamp to document end.
            int lastBlock = Math.Max(0, document.Blocks.Count - 1);
            return new DocumentPosition(lastBlock, 0, 0);
        }

        if (paragraph.Inlines.Count == 0)
            return position.With(0, 0);

        // Compute absolute character offset for the stored (inlineIndex, offset) pair.
        // Sum the lengths of all inlines BEFORE the stored inline index, then add the offset.
        // We intentionally do NOT clamp position.Offset — it may exceed the current inline
        // length when the inline was split, and the unclamped sum gives the correct absolute
        // character position in the paragraph.
        int absOffset = 0;
        for (int i = 0; i < position.InlineIndex && i < paragraph.Inlines.Count; i++)
            absOffset += paragraph.Inlines[i].Length;
        absOffset += position.Offset; // raw, unclamped

        // Remap absolute offset back to (inlineIndex, offset) in the current inline structure.
        int running = 0;
        for (int i = 0; i < paragraph.Inlines.Count; i++)
        {
            int len = paragraph.Inlines[i].Length;
            if (absOffset <= running + len)
                return position.With(i, absOffset - running);
            running += len;
        }

        // Past the end: clamp to end of last inline.
        int last = paragraph.Inlines.Count - 1;
        return position.With(last, paragraph.Inlines[last].Length);
    }

    private static void DeleteWithinBlock(Document document, DocumentPosition start, DocumentPosition end)
    {
        var paragraph = GetParagraph(document, start);

        // Simple case: deletion spans full inlines or partial runs
        for (int ii = end.InlineIndex; ii >= start.InlineIndex && ii < paragraph.Inlines.Count; ii--)
        {
            var inline = paragraph.Inlines[ii];

            if (inline is HyperlinkInline hyperlink)
            {
                int hlStart = ii == start.InlineIndex ? start.Offset : 0;
                int hlEnd   = ii == end.InlineIndex   ? end.Offset   : hyperlink.Length;

                if (hlStart == 0 && hlEnd >= hyperlink.Length)
                {
                    paragraph.RemoveInline(ii);
                }
                else
                {
                    DeleteWithinHyperlink(hyperlink, hlStart, hlEnd);
                    if (hyperlink.Length == 0)
                        paragraph.RemoveInline(ii);
                }
                continue;
            }

            if (inline is not TextRun run)
            {
                if (ii > start.InlineIndex || (ii == start.InlineIndex && start.Offset == 0))
                    paragraph.RemoveInline(ii);
                continue;
            }

            int startOffset = ii == start.InlineIndex ? start.Offset : 0;
            int endOffset = ii == end.InlineIndex ? end.Offset : run.Text.Length;

            if (startOffset == 0 && endOffset >= run.Text.Length)
            {
                paragraph.RemoveInline(ii);
            }
            else
            {
                run.Text = run.Text[..startOffset] + run.Text[endOffset..];
                if (run.Text.Length == 0)
                    paragraph.RemoveInline(ii);
            }
        }
    }

    /// <summary>
    /// Deletes characters within [<paramref name="startOffset"/>, <paramref name="endOffset"/>)
    /// of a hyperlink's children, preserving any characters outside that range.
    /// </summary>
    private static void DeleteWithinHyperlink(HyperlinkInline hyperlink, int startOffset, int endOffset)
    {
        // Compute the cumulative start offset of each child (forward pass).
        int[] starts = new int[hyperlink.Children.Count];
        int pos = 0;
        for (int j = 0; j < hyperlink.Children.Count; j++)
        {
            starts[j] = pos;
            pos += hyperlink.Children[j].Length;
        }

        // Apply deletions backward so removing items doesn't shift earlier indices.
        for (int j = hyperlink.Children.Count - 1; j >= 0; j--)
        {
            var child = hyperlink.Children[j];
            int childStart = starts[j];
            int childEnd   = childStart + child.Length;

            // Intersection of deletion range with this child's range.
            int delFrom = Math.Max(startOffset, childStart) - childStart;
            int delTo   = Math.Min(endOffset,   childEnd)   - childStart;
            if (delTo <= delFrom) continue;

            if (child is TextRun childRun)
            {
                if (delFrom == 0 && delTo >= childRun.Text.Length)
                {
                    hyperlink.Children.RemoveAt(j);
                }
                else
                {
                    childRun.Text = childRun.Text[..delFrom] + childRun.Text[delTo..];
                    if (childRun.Text.Length == 0)
                        hyperlink.Children.RemoveAt(j);
                }
            }
            else
            {
                // Non-text child: remove only if the deletion covers its full extent.
                if (delFrom == 0 && delTo >= child.Length)
                    hyperlink.Children.RemoveAt(j);
            }
        }
    }

    private static void TrimParagraphEnd(Paragraph paragraph, DocumentPosition position)
    {
        // Remove everything from position to end of paragraph
        for (int i = paragraph.Inlines.Count - 1; i > position.InlineIndex; i--)
            paragraph.RemoveInline(i);

        if (position.InlineIndex < paragraph.Inlines.Count && paragraph.Inlines[position.InlineIndex] is TextRun run)
            run.Text = run.Text[..Math.Min(position.Offset, run.Text.Length)];
    }

    private static void TrimParagraphStart(Paragraph paragraph, DocumentPosition position)
    {
        // Remove everything before position
        for (int i = position.InlineIndex - 1; i >= 0; i--)
            paragraph.RemoveInline(i);

        // After removal, the target inline is now at index 0
        if (paragraph.Inlines.Count > 0 && paragraph.Inlines[0] is TextRun run)
        {
            int offset = Math.Min(position.Offset, run.Text.Length);
            run.Text = run.Text[offset..];
            if (run.Text.Length == 0)
                paragraph.RemoveInline(0);
        }
    }

    private static void ApplyStyleToRunSegment(Paragraph paragraph, int inlineIndex, int startOffset, int endOffset, Func<TextStyle, TextStyle> transform)
    {
        if (paragraph.Inlines[inlineIndex] is not TextRun run) return;

        if (startOffset == 0 && endOffset >= run.Text.Length)
        {
            run.Style = transform(run.Style);
            return;
        }

        // Need to split the run: [before][styled][after]
        var originalText = run.Text;
        var originalStyle = run.Style;

        if (endOffset < originalText.Length)
        {
            var after = new TextRun(originalText[endOffset..], originalStyle);
            paragraph.InsertInline(inlineIndex + 1, after);
        }

        var styledRun = new TextRun(originalText[startOffset..Math.Min(endOffset, originalText.Length)], transform(originalStyle));
        paragraph.InsertInline(inlineIndex + 1, styledRun);

        if (startOffset > 0)
        {
            run.Text = originalText[..startOffset];
        }
        else
        {
            paragraph.RemoveInline(inlineIndex);
        }
    }

    /// <summary>
    /// Merges adjacent TextRuns with the same style and removes empty runs.
    /// </summary>
    private static void NormalizeInlines(Paragraph paragraph)
    {
        for (int i = paragraph.Inlines.Count - 1; i >= 0; i--)
        {
            if (paragraph.Inlines[i] is TextRun run && run.Text.Length == 0)
                paragraph.RemoveInline(i);
        }

        for (int i = paragraph.Inlines.Count - 2; i >= 0; i--)
        {
            if (paragraph.Inlines[i] is TextRun a && paragraph.Inlines[i + 1] is TextRun b && a.Style == b.Style)
            {
                a.Merge(b);
                paragraph.RemoveInline(i + 1);
            }
        }
    }

    private static void NotifyTextInserted(Document document, DocumentPosition start, DocumentPosition end)
    {
        document.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.TextInserted,
            new TextRange(start, end)));
    }
}
