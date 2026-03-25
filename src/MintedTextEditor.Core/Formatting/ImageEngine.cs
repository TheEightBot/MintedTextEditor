using MintedTextEditor.Core.Document;

namespace MintedTextEditor.Core.Formatting;

/// <summary>
/// Operations for inserting, removing, resizing, and replacing inline images within a document.
/// All mutating methods fire <see cref="Document.Document.NotifyChanged"/>.
/// </summary>
public static class ImageEngine
{
    // ── Insert / Remove ───────────────────────────────────────────────────

    /// <summary>
    /// Inserts a new <see cref="ImageInline"/> at the given position.
    /// Returns the inserted inline.
    /// </summary>
    public static ImageInline InsertImage(
        Document.Document doc,
        DocumentPosition position,
        string source,
        string altText = "",
        float width = 0,
        float height = 0)
    {
        var para = GetParagraph(doc, position.BlockIndex);
        var image = new ImageInline(source, altText, width, height) { Parent = para };

        int ii = Math.Min(position.InlineIndex, para.Inlines.Count);

        // Split a TextRun at the caret offset when inserting mid-run
        if (ii < para.Inlines.Count && para.Inlines[ii] is TextRun run && position.Offset > 0)
        {
            var right = run.Split(position.Offset);
            int insertAt = ii + 1;
            para.Inlines.Insert(insertAt, image);
            if (right.Length > 0)
                para.Inlines.Insert(insertAt + 1, right);
        }
        else
        {
            para.Inlines.Insert(ii, image);
        }

        doc.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.TextInserted,
            new TextRange(position, position)));

        return image;
    }

    /// <summary>
    /// Removes the given <see cref="ImageInline"/> from the document.
    /// </summary>
    public static void RemoveImage(Document.Document doc, ImageInline image)
    {
        if (image.Parent is not Paragraph para) return;

        int idx = para.Inlines.IndexOf(image);
        if (idx < 0) return;

        para.Inlines.RemoveAt(idx);
        image.Parent = null;

        var pos = new DocumentPosition(doc.Blocks.IndexOf(para), idx, 0);
        doc.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.TextDeleted,
            new TextRange(pos, pos)));
    }

    // ── Resize / Replace ──────────────────────────────────────────────────

    /// <summary>
    /// Updates the display dimensions of the given image.
    /// When <paramref name="maintainAspectRatio"/> is <see langword="true"/>,
    /// setting only one dimension will auto-calculate the other (requires the
    /// original dimensions to be non-zero).
    /// </summary>
    public static void ResizeImage(
        Document.Document doc,
        ImageInline image,
        float width,
        float height,
        bool maintainAspectRatio = true)
    {
        if (maintainAspectRatio && image.Width > 0 && image.Height > 0)
        {
            float aspect = image.Width / image.Height;
            if (width <= 0 && height > 0)
                width = height * aspect;
            else if (height <= 0 && width > 0)
                height = width / aspect;
            else if (width > 0 && height > 0)
            {
                // Preserve aspect by fitting to the tighter dimension.
                if (width / height > aspect)
                    width = height * aspect;
                else
                    height = width / aspect;
            }
        }

        image.Width  = width;
        image.Height = height;

        var para = image.Parent as Paragraph;
        int bi   = para is not null ? doc.Blocks.IndexOf(para) : 0;
        var pos  = new DocumentPosition(bi, 0, 0);
        doc.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.StyleChanged,
            new TextRange(pos, pos)));
    }

    /// <summary>
    /// Replaces the image source URI of the given <see cref="ImageInline"/>
    /// without changing its position or dimensions.
    /// </summary>
    public static void ReplaceImage(
        Document.Document doc, ImageInline image, string newSource)
    {
        image.Source = newSource;

        var para = image.Parent as Paragraph;
        int bi   = para is not null ? doc.Blocks.IndexOf(para) : 0;
        var pos  = new DocumentPosition(bi, 0, 0);
        doc.NotifyChanged(new DocumentChangedEventArgs(
            DocumentChangeType.StyleChanged,
            new TextRange(pos, pos)));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static Paragraph GetParagraph(Document.Document doc, int blockIndex)
    {
        if (blockIndex < 0 || blockIndex >= doc.Blocks.Count)
            throw new ArgumentOutOfRangeException(nameof(blockIndex));
        if (doc.Blocks[blockIndex] is not Paragraph para)
            throw new InvalidOperationException("Block is not a Paragraph.");
        return para;
    }
}
