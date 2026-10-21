using MintedTextEditor.Core.Document;

namespace MintedTextEditor.Core.Events;

/// <summary>
/// Remembers which images a document contains, and the source of each, and reports the images
/// added or removed since the last check, so the editor can raise its image inserted/removed
/// events after edits, undo and redo.
/// </summary>
/// <remarks>
/// <para>
/// Undo and redo rebuild paragraphs from snapshots, which replaces every image in them with a
/// new <see cref="ImageInline"/> instance. A removed image and an added image with the same
/// <see cref="ImageInline.Source"/> are therefore treated as the same image and not reported.
/// </para>
/// <para>
/// Changing the <see cref="ImageInline.Source"/> of an image already in the document (as
/// <see cref="Formatting.ImageEngine.ReplaceImage"/> does) is reported as the removal of the old
/// source and the insertion of the updated image.
/// </para>
/// </remarks>
public sealed class DocumentImageTracker
{
    private List<TrackedImage> _images = new();

    /// <summary>The images found by the last <see cref="Reset"/> or <see cref="Update"/>, in document order.</summary>
    public IReadOnlyList<ImageInline> Images => _images.Select(i => i.Image).ToList();

    /// <summary>Takes <paramref name="document"/>'s current images as the baseline without reporting changes.</summary>
    /// <param name="document">The document to scan.</param>
    public void Reset(Document.Document document) => _images = Snapshot(document);

    /// <summary>
    /// Scans <paramref name="document"/>, stores its images as the new baseline and returns the
    /// images added and removed since the previous baseline.
    /// </summary>
    /// <param name="document">The document to scan.</param>
    public DocumentImageChanges Update(Document.Document document)
    {
        var current = Snapshot(document);
        // Instance and source must both match: an image whose source changed in place counts as
        // removed (with its old source) and inserted (with its new one).
        var previousSet = new HashSet<TrackedImage>(_images, TrackedImageComparer.Instance);
        var currentSet = new HashSet<TrackedImage>(current, TrackedImageComparer.Instance);

        var inserted = current.Where(i => !previousSet.Contains(i)).Select(i => i.Image).ToList();
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

    // The source is copied because ReplaceImage mutates the instance the baseline refers to.
    private static List<TrackedImage> Snapshot(Document.Document document) =>
        CollectImages(document).Select(i => new TrackedImage(i, i.Source)).ToList();

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

    /// <summary>Compares tracked images by instance and by the source recorded for them.</summary>
    private sealed class TrackedImageComparer : IEqualityComparer<TrackedImage>
    {
        public static readonly TrackedImageComparer Instance = new();

        public bool Equals(TrackedImage? x, TrackedImage? y) =>
            ReferenceEquals(x?.Image, y?.Image) && string.Equals(x?.Source, y?.Source, StringComparison.Ordinal);

        public int GetHashCode(TrackedImage obj) =>
            HashCode.Combine(ReferenceEqualityComparer.Instance.GetHashCode(obj.Image), obj.Source);
    }
}

/// <summary>An image as recorded by <see cref="DocumentImageTracker"/>, with the source it had at that time.</summary>
/// <param name="Image">The image inline.</param>
/// <param name="Source">
/// The image's <see cref="ImageInline.Source"/> when it was recorded. For an image whose source
/// was changed in place this is the old source, while <paramref name="Image"/> now has the new one.
/// </param>
public sealed record TrackedImage(ImageInline Image, string Source);

/// <summary>Images added to and removed from a document, as reported by <see cref="DocumentImageTracker.Update"/>.</summary>
/// <param name="Inserted">Images now in the document that were not there before, or whose source changed, in document order.</param>
/// <param name="Removed">Images that were in the document and no longer are, or whose source changed, each with the source it had.</param>
public sealed record DocumentImageChanges(IReadOnlyList<ImageInline> Inserted, IReadOnlyList<TrackedImage> Removed)
{
    /// <summary>True when no image was added or removed.</summary>
    public bool IsEmpty => Inserted.Count == 0 && Removed.Count == 0;
}
