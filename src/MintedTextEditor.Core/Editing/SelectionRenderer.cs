using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Layout;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Editing;

/// <summary>
/// Renders the selection highlight behind document text.
/// Call <see cref="Render"/> before the text rendering pass so the highlight appears behind text.
/// </summary>
public class SelectionRenderer
{
    private readonly CaretRenderer _caretRenderer;

    /// <summary>Fill color used for the selection highlight.</summary>
    public EditorColor SelectionColor { get; set; } = new EditorColor(0x66, 0x99, 0xCC, 0x99); // semi-transparent blue

    public SelectionRenderer(CaretRenderer caretRenderer)
    {
        _caretRenderer = caretRenderer;
    }

    /// <summary>
    /// Renders selection highlight rectangles for <paramref name="selection"/> using
    /// the provided layout, document and drawing context.
    /// Does nothing when the selection is empty.
    /// </summary>
    public void Render(Selection selection, DocumentLayout layout, Document.Document document, IDrawingContext context)
    {
        if (selection.IsEmpty) return;

        var range = selection.Range; // guaranteed Start <= End
        var paint = new EditorPaint { Color = SelectionColor, IsAntiAlias = false };

        foreach (var block in layout.Blocks)
        {
            int blockIdx = block.BlockIndex;

            // Skip blocks entirely outside the selection range
            if (blockIdx < range.Start.BlockIndex || blockIdx > range.End.BlockIndex)
                continue;

            foreach (var line in block.Lines)
            {
                if (line.Runs.Count == 0) continue;

                // Determine DocumentPosition at first and last character on this line
                var lineStart = RunStartPosition(line.Runs[0], blockIdx, document);
                var lineEnd = RunEndPosition(line.Runs[^1], blockIdx, document);

                // Skip if selection does not overlap this line
                if (range.End <= lineStart || range.Start >= lineEnd) continue;

                // Pixel X for the left edge of the highlighted region
                float startX = range.Start <= lineStart
                    ? line.Runs[0].X
                    : GetXForPosition(range.Start, layout, document, context);

                // Pixel X for the right edge of the highlighted region
                float endX = range.End >= lineEnd
                    ? line.Runs[^1].X + line.Runs[^1].Width
                    : GetXForPosition(range.End, layout, document, context);

                if (endX <= startX) continue;

                float y = block.Y + line.Y;
                context.FillRect(new EditorRect(startX, y, endX - startX, line.Height), paint);
            }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private float GetXForPosition(DocumentPosition pos, DocumentLayout layout, Document.Document document, IDrawingContext context)
    {
        return _caretRenderer.GetCaretX(pos, layout, document, context);
    }

    private static DocumentPosition RunStartPosition(LayoutRun run, int blockIndex, Document.Document document)
    {
        if (run.SourceInline is null) return new DocumentPosition(blockIndex, 0, 0);

        var paragraph = GetParagraph(document, blockIndex);
        if (paragraph is null) return new DocumentPosition(blockIndex, 0, 0);
        int inlineIdx = paragraph.Inlines.IndexOf(run.SourceInline);
        if (inlineIdx < 0) return new DocumentPosition(blockIndex, 0, 0);

        return new DocumentPosition(blockIndex, inlineIdx, run.SourceOffset);
    }

    private static DocumentPosition RunEndPosition(LayoutRun run, int blockIndex, Document.Document document)
    {
        if (run.SourceInline is null) return new DocumentPosition(blockIndex, 0, 0);

        var paragraph = GetParagraph(document, blockIndex);
        if (paragraph is null) return new DocumentPosition(blockIndex, 0, 0);
        int inlineIdx = paragraph.Inlines.IndexOf(run.SourceInline);
        if (inlineIdx < 0) return new DocumentPosition(blockIndex, 0, 0);

        int offset = Math.Clamp(run.SourceOffset + run.Text.Length, 0, run.SourceInline.Length);
        return new DocumentPosition(blockIndex, inlineIdx, offset);
    }

    private static Paragraph? GetParagraph(Document.Document document, int blockIndex)
    {
        blockIndex = Math.Clamp(blockIndex, 0, document.Blocks.Count - 1);
        return document.Blocks[blockIndex] as Paragraph;
    }
}
