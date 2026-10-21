using MintedTextEditor.Core.Document;

namespace MintedTextEditor.Core.Events;

/// <summary>
/// Remembers which images a document contains and reports the images added or removed since
/// the last check, so the editor can raise its image inserted/removed events after edits,
/// undo and redo.
/// </summary>
/// <remarks>
/// Undo and redo rebuild paragraphs from snapshots, which replaces every image in them with a
/// new <see cref="ImageInline"/> instance. A removed image and an added image with the same
/// <see cref="ImageInline.Source"/> are therefore treated as the same image and not reported.
/// </remarks>
public sealed class DocumentImageTracker
{
    private List<ImageInline> _images = new();

    /// <summary>The images found by the last <see cref="Reset"/> or <see cref="Update"/>, in document order.</summary>
    public IReadOnlyList<ImageInline> Images => _images;

    /// <summary>Takes <paramref name="document"/>'s current images as the baseline without reporting changes.</summary>
    /// <param name="document">The document to scan.</param>
    public void Reset(Document.Document document) => _images = CollectImages(document);

    /// <summary>
    /// Scans <paramref name="document"/>, stores its images as the new baseline and returns the
    /// images added and removed since the previous baseline.
    /// </summary>
    /// <param name="document">The document to scan.</param>
    public DocumentImageChanges Update(Document.Document document)
    {
        var current = CollectImages(document);
        var previousSet = new HashSet<ImageInline>(_images, ReferenceEqualityComparer.Instance);
        var currentSet = new HashSet<ImageInline>(current, ReferenceEqualityComparer.Instance);

        var inserted = current.Where(i => !previousSet.Contains(i)).ToList();
        var removed = _images.Where(i => !currentSet.Contains(i)).ToList();
        _images = current;

        // Pair off re-created instances of the same image (see remarks).
        for (int r = removed.Count - 1; r >= 0; r--)
        {
            int match = inserted.FindIndex(i => string.Equals(i.Source, removed[r].Source, StringComparison.Ordinal));
            if (match < 0) continue;
            inserted.RemoveAt(match);
            removed.RemoveAt(r);
        }

        return new DocumentImageChanges(inserted, removed);
    }

    /// <summary>Returns every image in <paramref name="document"/>, including those in table cells and hyperlinks, in document order.</summary>
    /// <param name="document">The document to scan.</param>
    public static List<ImageInline> CollectImages(Document.Document document)
    {
        var images = new List<ImageInline>();
        CollectFromBlocks(document.Blocks, images);
        return images;
    }

    private static void CollectFromBlocks(IEnumerable<Block> blocks, List<ImageInline> images)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    CollectFromInlines(paragraph.Inlines, images);
                    break;
                case TableBlock table:
                    foreach (var row in table.Rows)
                        foreach (var cell in row.Cells)
                            CollectFromBlocks(cell.Blocks, images);
                    break;
            }
        }
    }

    private static void CollectFromInlines(IEnumerable<Inline> inlines, List<ImageInline> images)
    {
        foreach (var inline in inlines)
        {
            if (inline is ImageInline image)
                images.Add(image);
            else if (inline is HyperlinkInline link)
                CollectFromInlines(link.Children, images);
        }
    }
}

/// <summary>Images added to and removed from a document, as reported by <see cref="DocumentImageTracker.Update"/>.</summary>
/// <param name="Inserted">Images now in the document that were not there before, in document order.</param>
/// <param name="Removed">Images that were in the document and no longer are.</param>
public sealed record DocumentImageChanges(IReadOnlyList<ImageInline> Inserted, IReadOnlyList<ImageInline> Removed)
{
    /// <summary>True when no image was added or removed.</summary>
    public bool IsEmpty => Inserted.Count == 0 && Removed.Count == 0;
}
