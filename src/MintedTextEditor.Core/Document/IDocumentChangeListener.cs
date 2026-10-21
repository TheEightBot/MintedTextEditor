namespace MintedTextEditor.Core.Document;

/// <summary>
/// Listener interface for document change notifications.
/// </summary>
public interface IDocumentChangeListener
{
    /// <summary>Called after the document has changed.</summary>
    /// <param name="document">The document that changed.</param>
    /// <param name="e">Details of the change.</param>
    void OnDocumentChanged(Document document, DocumentChangedEventArgs e);
}

/// <summary>
/// Event arguments for document change notifications.
/// </summary>
public class DocumentChangedEventArgs : EventArgs
{
    /// <summary>The kind of change that occurred.</summary>
    public DocumentChangeType ChangeType { get; }
    /// <summary>The range of the document affected by the change.</summary>
    public TextRange AffectedRange { get; }

    /// <summary>Creates event arguments for the given change type and affected range.</summary>
    public DocumentChangedEventArgs(DocumentChangeType changeType, TextRange affectedRange)
    {
        ChangeType = changeType;
        AffectedRange = affectedRange;
    }
}

/// <summary>
/// Types of document changes.
/// </summary>
public enum DocumentChangeType
{
    /// <summary>Text was inserted.</summary>
    TextInserted,
    /// <summary>Text was deleted.</summary>
    TextDeleted,
    /// <summary>A block was split into two.</summary>
    BlockSplit,
    /// <summary>Two blocks were merged into one.</summary>
    BlocksMerged,
    /// <summary>Formatting was changed.</summary>
    StyleChanged
}
