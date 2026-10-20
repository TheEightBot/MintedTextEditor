namespace MintedTextEditor.Core.Input;

/// <summary>
/// The text an input method editor sees around the caret: the plain text of the caret's
/// paragraph plus the selection within it, in UTF-16 code units. Inline images are
/// represented as U+FFFC so offsets line up with the document.
/// </summary>
public readonly record struct ImeTextContext(string ParagraphText, int SelectionStart, int SelectionEnd)
{
    /// <summary>An empty paragraph with the caret at 0.</summary>
    public static ImeTextContext Empty => new(string.Empty, 0, 0);

    /// <summary>Gets up to <paramref name="length"/> units immediately before the selection.</summary>
    public string TextBeforeCursor(int length)
    {
        int start = Math.Max(0, SelectionStart - Math.Max(0, length));
        return ParagraphText[start..SelectionStart];
    }

    /// <summary>Gets up to <paramref name="length"/> units immediately after the selection.</summary>
    public string TextAfterCursor(int length)
    {
        int end = Math.Min(ParagraphText.Length, SelectionEnd + Math.Max(0, length));
        return ParagraphText[SelectionEnd..end];
    }

    /// <summary>Gets the selected text, or an empty string when the selection is collapsed.</summary>
    public string SelectedText => ParagraphText[SelectionStart..SelectionEnd];
}
