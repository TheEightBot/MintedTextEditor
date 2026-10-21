namespace MintedTextEditor.Core.Document;

/// <summary>
/// Root container for a rich-text document. Holds an ordered list of <see cref="Block"/> elements.
/// </summary>
public class Document
{
    private readonly List<IDocumentChangeListener> _listeners = new();

    /// <summary>The top-level blocks of the document, in order.</summary>
    public List<Block> Blocks { get; } = new();

    /// <summary>Fired when the document content changes.</summary>
    public event EventHandler<DocumentChangedEventArgs>? Changed;

    /// <summary>Creates an empty document with a single empty paragraph.</summary>
    public Document()
    {
        var paragraph = new Paragraph { Parent = this };
        Blocks.Add(paragraph);
    }

    /// <summary>Creates a document with the given blocks.</summary>
    public Document(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            block.Parent = this;
            Blocks.Add(block);
        }

        if (Blocks.Count == 0)
        {
            var paragraph = new Paragraph { Parent = this };
            Blocks.Add(paragraph);
        }
    }

    /// <summary>Appends a block to the end of the document and sets its parent.</summary>
    public void AddBlock(Block block)
    {
        block.Parent = this;
        Blocks.Add(block);
    }

    /// <summary>Inserts a block at the given index and sets its parent.</summary>
    public void InsertBlock(int index, Block block)
    {
        block.Parent = this;
        Blocks.Insert(index, block);
    }

    /// <summary>
    /// Removes the block at the given index. Does nothing if the index is out of range
    /// or if it is the only block in the document.
    /// </summary>
    public void RemoveBlock(int index)
    {
        if (index >= 0 && index < Blocks.Count && Blocks.Count > 1)
        {
            Blocks[index].Parent = null;
            Blocks.RemoveAt(index);
        }
    }

    /// <summary>Gets the combined plain text of the entire document.</summary>
    public string GetText() => string.Join("\n", Blocks.Select(b => b.GetText()));

    /// <summary>Total number of blocks in the document.</summary>
    public int BlockCount => Blocks.Count;

    // ── Change notifications ─────────────────────────────────────

    /// <summary>Registers a listener that is notified of document changes.</summary>
    public void AddChangeListener(IDocumentChangeListener listener) => _listeners.Add(listener);
    /// <summary>Unregisters a previously added change listener.</summary>
    public void RemoveChangeListener(IDocumentChangeListener listener) => _listeners.Remove(listener);

    /// <summary>Raises <see cref="Changed"/> and notifies all registered change listeners.</summary>
    public void NotifyChanged(DocumentChangedEventArgs e)
    {
        Changed?.Invoke(this, e);
        foreach (var listener in _listeners)
            listener.OnDocumentChanged(this, e);
    }
}
