namespace MintedTextEditor.Core.Input;

/// <summary>
/// Platform-neutral model of an input method's composing region. Soft keyboards
/// (Gboard, Samsung, MIUI, …) replace the word being typed many times per word; this
/// tracker mirrors the text already placed in the editor for the current composition
/// and turns each update into the minimal set of edits on an <see cref="IImeEditSink"/>.
/// The composition always ends at the caret.
/// </summary>
public sealed class ImeCompositionTracker
{
    private readonly IImeEditSink _sink;
    private string _composingText = string.Empty;

    public ImeCompositionTracker(IImeEditSink sink) => _sink = sink;

    /// <summary>The text currently in the composing region (already present in the editor).</summary>
    public string ComposingText => _composingText;

    /// <summary>Replaces the composing region with <paramref name="text"/>.</summary>
    public void SetComposingText(string text) => ReplaceComposition(text ?? string.Empty);

    /// <summary>Replaces the composing region with <paramref name="text"/> and ends the composition.</summary>
    public void CommitText(string text)
    {
        if (!string.IsNullOrEmpty(text))
            ReplaceComposition(text);
        _composingText = string.Empty;
    }

    /// <summary>Ends the composition, keeping its text.</summary>
    public void FinishComposing() => _composingText = string.Empty;

    /// <summary>Forgets the composition without editing (e.g. after a programmatic change).</summary>
    public void Reset() => _composingText = string.Empty;

    /// <summary>
    /// Marks existing paragraph text [<paramref name="start"/>, <paramref name="end"/>) as the
    /// composing region. IMEs do this when the caret lands in or next to a word so they can
    /// re-compose it; without adopting it the next update would insert the word a second time.
    /// The caret is moved to the region end, where the composition must sit.
    /// </summary>
    public void SetComposingRegion(int start, int end, ImeTextContext context)
    {
        int len = context.ParagraphText.Length;
        int s = Math.Clamp(Math.Min(start, end), 0, len);
        int e = Math.Clamp(Math.Max(start, end), 0, len);

        if (context.SelectionStart != e || context.SelectionEnd != e)
            _sink.SetSelection(e, e);

        _composingText = context.ParagraphText[s..e];
    }

    /// <summary>
    /// Deletes UTF-16 units around the selection (Android <c>deleteSurroundingText</c>
    /// semantics): the selection itself is never deleted.
    /// </summary>
    public void DeleteSurrounding(int beforeLength, int afterLength)
    {
        if (beforeLength > 0)
        {
            // The composition ends at the caret, so a delete before it eats its tail.
            _composingText = beforeLength >= _composingText.Length
                ? string.Empty
                : _composingText[..^beforeLength];
            _sink.DeleteBefore(beforeLength);
        }
        if (afterLength > 0)
            _sink.DeleteAfter(afterLength);
    }

    /// <summary>
    /// Like <see cref="DeleteSurrounding"/> but counts in code points, so a surrogate pair
    /// (most emoji) counts as one.
    /// </summary>
    public void DeleteSurroundingInCodePoints(int beforeLength, int afterLength, ImeTextContext context)
    {
        string text = context.ParagraphText;

        int before = 0, pos = context.SelectionStart;
        for (int i = 0; i < beforeLength && pos > 0; i++)
        {
            int step = pos >= 2 && char.IsSurrogatePair(text[pos - 2], text[pos - 1]) ? 2 : 1;
            pos -= step;
            before += step;
        }

        int after = 0;
        pos = context.SelectionEnd;
        for (int i = 0; i < afterLength && pos < text.Length; i++)
        {
            int step = pos + 1 < text.Length && char.IsSurrogatePair(text[pos], text[pos + 1]) ? 2 : 1;
            pos += step;
            after += step;
        }

        DeleteSurrounding(before, after);
    }

    /// <summary>Ends any composition and splits the paragraph (IME editor action / Enter).</summary>
    public void Enter()
    {
        _composingText = string.Empty;
        _sink.Enter();
    }

    private void ReplaceComposition(string newText)
    {
        string old = _composingText;
        _composingText = newText;
        if (string.Equals(old, newText, StringComparison.Ordinal)) return;

        int prefix = 0;
        int max = Math.Min(old.Length, newText.Length);
        while (prefix < max && old[prefix] == newText[prefix]) prefix++;
        // Never keep half of a surrogate pair.
        if (prefix > 0 && char.IsHighSurrogate(newText[prefix - 1])) prefix--;

        int toDelete = old.Length - prefix;
        if (toDelete > 0)
            _sink.DeleteBefore(toDelete);
        if (newText.Length > prefix)
            _sink.InsertText(newText[prefix..]);
    }
}
