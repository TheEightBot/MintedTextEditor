using MintedTextEditor.Core.Document;
using MintedTextEditor.Core.Rendering;

namespace MintedTextEditor.Core.Layout;

/// <summary>
/// Caches <see cref="LayoutBlock"/> results per block and selectively invalidates
/// on document edits, so only changed blocks are re-laid-out.
/// </summary>
public class LayoutCache : IDocumentChangeListener
{
    private readonly Dictionary<int, LayoutBlock> _cache = new();
    private readonly TextLayoutEngine _engine;
    private float _lastViewportWidth = -1f;

    public LayoutCache()
        : this(new TextLayoutEngine())
    {
    }

    public LayoutCache(TextLayoutEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>
    /// Gets or computes the full <see cref="DocumentLayout"/> for the given document and viewport width.
    /// Re-uses cached block layouts where possible.
    /// </summary>
    public DocumentLayout GetLayout(Document.Document document, float viewportWidth, IDrawingContext context)
    {
        if (_lastViewportWidth < 0f || Math.Abs(_lastViewportWidth - viewportWidth) > 0.5f)
        {
            InvalidateAll();
            _lastViewportWidth = viewportWidth;
        }

        var layout = new DocumentLayout { ViewportWidth = viewportWidth };
        float documentY = 0;
        int numberListCounter = 0;

        for (int i = 0; i < document.Blocks.Count; i++)
        {
            LayoutBlock layoutBlock;

            if (_cache.TryGetValue(i, out var cached))
            {
                layoutBlock = cached;
            }
            else
            {
                layoutBlock = _engine.LayoutBlock(document.Blocks[i], i, viewportWidth, context);
                _cache[i] = layoutBlock;
            }

            layoutBlock.Y = documentY;

            if (document.Blocks[i] is Paragraph para)
            {
                if (para.Style.ListType == ListType.Number)
                    layoutBlock.ListNumber = ++numberListCounter;
                else
                {
                    numberListCounter = 0;
                    layoutBlock.ListNumber = 0;
                }
            }

            layout.Blocks.Add(layoutBlock);
            documentY += layoutBlock.TotalHeight;
        }

        layout.TotalHeight = documentY;
        return layout;
    }

    /// <summary>
    /// Invalidates the cached layout for a specific block index.
    /// </summary>
    public void InvalidateBlock(int blockIndex)
    {
        _cache.Remove(blockIndex);
    }

    /// <summary>
    /// Invalidates all cached block layouts.
    /// </summary>
    public void InvalidateAll()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Handles document change notifications by invalidating the affected blocks.
    /// </summary>
    public void OnDocumentChanged(Document.Document document, DocumentChangedEventArgs e)
    {
        var range = e.AffectedRange;

        switch (e.ChangeType)
        {
            case DocumentChangeType.TextInserted:
            case DocumentChangeType.TextDeleted:
            case DocumentChangeType.StyleChanged:
                // Invalidate blocks in the affected range
                for (int i = range.Start.BlockIndex; i <= range.End.BlockIndex; i++)
                    InvalidateBlock(i);
                break;

            case DocumentChangeType.BlockSplit:
            case DocumentChangeType.BlocksMerged:
                // Structural changes invalidate everything from the affected block onward
                InvalidateFromBlock(range.Start.BlockIndex);
                break;
        }
    }

    /// <summary>
    /// Invalidates all cached blocks from the given index onward.
    /// Used for structural changes that shift block indices.
    /// </summary>
    private void InvalidateFromBlock(int startIndex)
    {
        var keysToRemove = _cache.Keys.Where(k => k >= startIndex).ToList();
        foreach (var key in keysToRemove)
            _cache.Remove(key);
    }
}
