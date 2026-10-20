namespace MintedTextEditor.Core.Input;

/// <summary>
/// Receives the low-level edits produced by <see cref="ImeCompositionTracker"/>.
/// Offsets and counts are UTF-16 code units in the caret's paragraph, matching the
/// coordinates reported by <see cref="ImeTextContext"/>. These are exact-range edits:
/// unlike a user Backspace they never expand to a whole grapheme cluster.
/// </summary>
public interface IImeEditSink
{
    /// <summary>Inserts text at the caret, replacing any selection. May contain newlines.</summary>
    void InsertText(string text);

    /// <summary>Deletes <paramref name="count"/> units ending at the selection start. The selection is kept.</summary>
    void DeleteBefore(int count);

    /// <summary>Deletes <paramref name="count"/> units starting at the selection end. The selection is kept.</summary>
    void DeleteAfter(int count);

    /// <summary>Sets the selection (or caret when equal) to paragraph-relative offsets.</summary>
    void SetSelection(int start, int end);

    /// <summary>Splits the paragraph at the caret, as the Enter key does.</summary>
    void Enter();
}
