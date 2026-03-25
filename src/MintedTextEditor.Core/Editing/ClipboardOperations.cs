using MintedTextEditor.Core.Document;

namespace MintedTextEditor.Core.Editing;

/// <summary>
/// Platform-independent copy, cut, and paste operations.
/// </summary>
public static class ClipboardOperations
{
    /// <summary>
    /// Copies the text in <paramref name="range"/> to the clipboard.
    /// </summary>
    public static async Task CopyAsync(Document.Document document, TextRange range, IClipboardProvider clipboard)
    {
        string text = DocumentEditor.GetSelectedText(document, range);
        await clipboard.SetTextAsync(text);
    }

    /// <summary>
    /// Copies the text in <paramref name="range"/> to the clipboard and then deletes it from the document.
    /// Returns the caret position after the cut.
    /// </summary>
    public static async Task<DocumentPosition> CutAsync(Document.Document document, TextRange range, IClipboardProvider clipboard)
    {
        string text = DocumentEditor.GetSelectedText(document, range);
        await clipboard.SetTextAsync(text);
        return DocumentEditor.DeleteRange(document, range);
    }

    /// <summary>
    /// Pastes plain text from the clipboard at <paramref name="position"/>.
    /// Replaces <paramref name="selectionRange"/> if it is not empty.
    /// Returns the caret position after the paste, or null if the clipboard was empty.
    /// </summary>
    public static async Task<DocumentPosition?> PasteAsync(
        Document.Document document,
        DocumentPosition position,
        TextRange selectionRange,
        IClipboardProvider clipboard)
    {
        string? text = await clipboard.GetTextAsync();
        if (string.IsNullOrEmpty(text)) return null;

        DocumentPosition insertPos = position;
        if (!selectionRange.IsEmpty)
            insertPos = DocumentEditor.DeleteRange(document, selectionRange);

        return DocumentEditor.InsertText(document, insertPos, text);
    }
}
