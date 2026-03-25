namespace MintedTextEditor.Core.Document;

/// <summary>
/// Listener interface for document change notifications.
/// </summary>
public interface IDocumentChangeListener
{
    void OnDocumentChanged(Document document, DocumentChangedEventArgs e);
}

/// <summary>
/// Event arguments for document change notifications.
/// </summary>
public class DocumentChangedEventArgs : EventArgs
{
    public DocumentChangeType ChangeType { get; }
    public TextRange AffectedRange { get; }

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
    TextInserted,
    TextDeleted,
    BlockSplit,
    BlocksMerged,
    StyleChanged
}
