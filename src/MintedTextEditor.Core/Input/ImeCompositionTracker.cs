namespace MintedTextEditor.Core.Input;

/// <summary>
/// Platform-neutral model of an input method's composing region. Soft keyboards
/// (Gboard, Samsung, MIUI, …) replace the word being typed many times per word; this
/// tracker remembers where the current composition sits in the caret's paragraph and
/// turns each update into the minimal set of edits on an <see cref="IImeEditSink"/>.
/// Semantics follow Android's <c>InputConnection</c>: the composing region is tracked
/// independently of the selection, and cursor positions use the relative
/// <c>newCursorPosition</c> rules.
/// </summary>
public sealed class ImeCompositionTracker
{
    private readonly IImeEditSink _sink;
    private int _composingStart;
    private string _composingText = string.Empty;

    public ImeCompositionTracker(IImeEditSink sink) => _sink = sink;

    /// <summary>The text currently in the composing region (already present in the editor).</summary>
    public string ComposingText => _composingText;

    /// <summary>Whether a composing region is active.</summary>
    public bool HasComposition => _composingText.Length > 0;

    /// <summary>Paragraph offset where the composing region starts, or -1 when there is none.</summary>
    public int ComposingStart => HasComposition ? _composingStart : -1;

    /// <summary>Paragraph offset where the composing region ends, or -1 when there is none.</summary>
    public int ComposingEnd => HasComposition ? _composingStart + _composingText.Length : -1;

    /// <summary>
    /// Replaces the composing region (or the selection, when there is none) with
    /// <paramref name="text"/>, which stays composing. Text containing a newline splits the
    /// paragraph, so the composition ends: offsets would otherwise span paragraphs.
    /// </summary>
    public void SetComposingText(string text, int newCursorPosition, ImeTextContext context)
    {
        text ??= string.Empty;
        int start = ReplaceComposition(text, newCursorPosition, context);
        if (ContainsNewline(text))
        {
            _composingText = string.Empty;
            return;
        }
        _composingStart = start;
        _composingText = text;
    }

    /// <summary>
    /// Replaces the composing region (or the selection, when there is none) with
    /// <paramref name="text"/> and ends the composition. An empty commit deletes the
    /// composing text, as on Android.
    /// </summary>
    public void CommitText(string text, int newCursorPosition, ImeTextContext context)
    {
        ReplaceComposition(text ?? string.Empty, newCursorPosition, context);
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
    /// The selection is left unchanged.
    /// </summary>
    public void SetComposingRegion(int start, int end, ImeTextContext context)
    {
        var (s, e) = Clamp(start, end, context.ParagraphText.Length);
        _composingStart = s;
        _composingText = context.ParagraphText[s..e];
    }

    /// <summary>Moves the selection. The composing region is kept, as on Android.</summary>
    public void SetSelection(int start, int end) => _sink.SetSelection(start, end);

    /// <summary>
    /// Deletes UTF-16 units around the selection (Android <c>deleteSurroundingText</c>
    /// semantics): the selection itself is never deleted. With a collapsed caret a count that
    /// runs past the paragraph edge joins the neighbouring paragraph (the break counts as one
    /// unit).
    /// </summary>
    /// <returns><see langword="true"/> when the delete crosses into another paragraph, so the
    /// caller must re-sync the IME (offsets are paragraph-relative).</returns>
    public bool DeleteSurrounding(int beforeLength, int afterLength, ImeTextContext context)
    {
        beforeLength = Math.Max(0, beforeLength);
        afterLength = Math.Max(0, afterLength);
        int len = context.ParagraphText.Length;
        bool collapsed = context.SelectionStart == context.SelectionEnd;
        bool crosses = collapsed
            && (beforeLength > context.SelectionStart || afterLength > len - context.SelectionEnd);

        if (crosses)
        {
            _composingText = string.Empty;
        }
        else
        {
            // Adjust the composing range in original coordinates, right-hand range first.
            if (afterLength > 0) AdjustForDelete(context.SelectionEnd, afterLength);
            if (beforeLength > 0) AdjustForDelete(context.SelectionStart - beforeLength, beforeLength);
        }

        if (beforeLength > 0) _sink.DeleteBefore(beforeLength);
        if (afterLength > 0) _sink.DeleteAfter(afterLength);
        return crosses;
    }

    /// <summary>
    /// Like <see cref="DeleteSurrounding"/> but counts in code points, so a surrogate pair
    /// (most emoji) counts as one.
    /// </summary>
    /// <returns><see langword="true"/> when the delete crosses into another paragraph.</returns>
    public bool DeleteSurroundingInCodePoints(int beforeLength, int afterLength, ImeTextContext context)
    {
        string text = context.ParagraphText;

        int before = 0, pos = context.SelectionStart, i = 0;
        for (; i < beforeLength && pos > 0; i++)
        {
            int step = pos >= 2 && char.IsSurrogatePair(text[pos - 2], text[pos - 1]) ? 2 : 1;
            pos -= step;
            before += step;
        }
        before += Math.Max(0, beforeLength - i); // past the paragraph start: breaks/units beyond

        int after = 0;
        pos = context.SelectionEnd;
        for (i = 0; i < afterLength && pos < text.Length; i++)
        {
            int step = pos + 1 < text.Length && char.IsSurrogatePair(text[pos], text[pos + 1]) ? 2 : 1;
            pos += step;
            after += step;
        }
        after += Math.Max(0, afterLength - i);

        return DeleteSurrounding(before, after, context);
    }

    /// <summary>
    /// Replaces paragraph text [<paramref name="start"/>, <paramref name="end"/>) with
    /// <paramref name="text"/> (Android <c>replaceText</c>, which Gboard uses when a
    /// suggestion is picked) and places the cursor per <paramref name="newCursorPosition"/>.
    /// Ends any composition.
    /// </summary>
    public void Replace(int start, int end, string text, int newCursorPosition, ImeTextContext context)
    {
        text ??= string.Empty;
        int len = context.ParagraphText.Length;
        var (s, e) = Clamp(start, end, len);
        _composingText = string.Empty;

        if (text.Length > 0)
        {
            // Always establish the paragraph-local range: the editor's real selection may
            // extend beyond this paragraph even when the clamped context matches.
            _sink.SetSelection(s, e);
            _sink.InsertText(text);
        }
        else if (e > s)
        {
            _sink.SetSelection(e, e);
            _sink.DeleteBefore(e - s);
        }

        if (!ContainsNewline(text))
            PlaceCursor(s, text.Length, newCursorPosition, len - (e - s) + text.Length);
    }

    /// <summary>Ends any composition and splits the paragraph (IME editor action / Enter).</summary>
    public void Enter()
    {
        _composingText = string.Empty;
        _sink.Enter();
    }

    /// <summary>
    /// Replaces the composing region (or the selection) with <paramref name="newText"/>
    /// using the smallest edit, then applies the cursor rule. Returns the start offset of
    /// the replaced range.
    /// </summary>
    private int ReplaceComposition(string newText, int newCursorPosition, ImeTextContext context)
    {
        int len = context.ParagraphText.Length;
        bool caretCollapsed = context.SelectionStart == context.SelectionEnd;
        int start, end;
        bool edited;

        if (HasComposition)
        {
            start = Math.Clamp(_composingStart, 0, len);
            end = Math.Clamp(_composingStart + _composingText.Length, start, len);
            string old = context.ParagraphText[start..end];

            int prefix = 0;
            int max = Math.Min(old.Length, newText.Length);
            while (prefix < max && old[prefix] == newText[prefix]) prefix++;
            // Never keep half of a surrogate pair.
            if (prefix > 0 && char.IsHighSurrogate(newText[prefix - 1])) prefix--;

            int toDelete = old.Length - prefix;
            string toInsert = newText[prefix..];
            edited = toDelete > 0 || toInsert.Length > 0;

            if (edited && !(caretCollapsed && context.SelectionStart == end))
                _sink.SetSelection(end, end);
            if (toDelete > 0) _sink.DeleteBefore(toDelete);
            if (toInsert.Length > 0) _sink.InsertText(toInsert);
        }
        else
        {
            start = context.SelectionStart;
            end = context.SelectionEnd;
            edited = newText.Length > 0 || end > start;
            if (newText.Length > 0)
            {
                _sink.InsertText(newText); // replaces any selection
            }
            else if (end > start)
            {
                _sink.SetSelection(end, end);
                _sink.DeleteBefore(end - start);
            }
        }

        if (!ContainsNewline(newText))
        {
            int newLen = len - (end - start) + newText.Length;
            if (edited)
                PlaceCursor(start, newText.Length, newCursorPosition, newLen);
            else
                PlaceCursorIfMoved(start, newText.Length, newCursorPosition, newLen, context);
        }
        return start;
    }

    // Android: newCursorPosition > 0 is relative to the end of the inserted text (1 = right
    // after it); <= 0 is relative to its start. Our edits leave the caret after the text.
    private void PlaceCursor(int start, int textLength, int newCursorPosition, int paragraphLength)
    {
        int target = CursorTarget(start, textLength, newCursorPosition, paragraphLength);
        if (target != start + textLength)
            _sink.SetSelection(target, target);
    }

    private void PlaceCursorIfMoved(int start, int textLength, int newCursorPosition, int paragraphLength, ImeTextContext context)
    {
        int target = CursorTarget(start, textLength, newCursorPosition, paragraphLength);
        if (context.SelectionStart != target || context.SelectionEnd != target)
            _sink.SetSelection(target, target);
    }

    private static int CursorTarget(int start, int textLength, int newCursorPosition, int paragraphLength)
    {
        long target = newCursorPosition > 0
            ? (long)start + textLength + newCursorPosition - 1
            : (long)start + newCursorPosition;
        return (int)Math.Clamp(target, 0, paragraphLength);
    }

    /// <summary>Shifts/shrinks the composing range for a deletion of [delStart, delStart+length).</summary>
    private void AdjustForDelete(int delStart, int length)
    {
        if (!HasComposition || length <= 0) return;
        int delEnd = delStart + length;
        int cs = _composingStart, ce = cs + _composingText.Length;

        // Remove the overlapping part of the composing text.
        int overlapStart = Math.Max(cs, delStart);
        int overlapEnd = Math.Min(ce, delEnd);
        if (overlapEnd > overlapStart)
            _composingText = _composingText.Remove(overlapStart - cs, overlapEnd - overlapStart);

        _composingStart = cs <= delStart ? cs : Math.Max(delStart, cs - length);
    }

    private static (int Start, int End) Clamp(int start, int end, int length)
    {
        int s = Math.Clamp(Math.Min(start, end), 0, length);
        int e = Math.Clamp(Math.Max(start, end), 0, length);
        return (s, e);
    }

    private static bool ContainsNewline(string text)
        => text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0;
}
