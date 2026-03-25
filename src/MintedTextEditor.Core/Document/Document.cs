namespace MintedTextEditor.Core.Document;

/// <summary>
/// Root container for a rich-text document. Holds an ordered list of <see cref="Block"/> elements.
/// </summary>
public class Document
{
    private readonly List<IDocumentChangeListener> _listeners = new();

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

    public void AddBlock(Block block)
    {
        block.Parent = this;
        Blocks.Add(block);
    }

    public void InsertBlock(int index, Block block)
    {
        block.Parent = this;
        Blocks.Insert(index, block);
    }

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

    public void AddChangeListener(IDocumentChangeListener listener) => _listeners.Add(listener);
    public void RemoveChangeListener(IDocumentChangeListener listener) => _listeners.Remove(listener);

    public void NotifyChanged(DocumentChangedEventArgs e)
    {
        Changed?.Invoke(this, e);
        foreach (var listener in _listeners)
            listener.OnDocumentChanged(this, e);
    }
}
