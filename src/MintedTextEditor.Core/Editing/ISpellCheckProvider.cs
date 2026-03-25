namespace MintedTextEditor.Core.Editing;

/// <summary>
/// Integration point for spell-check engines.
/// Implement this interface and assign it to the editor to highlight misspelled words.
/// </summary>
public interface ISpellCheckProvider
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="word"/> is correctly spelled.
    /// The implementation is expected to be fast (called per-word during layout).
    /// </summary>
    bool IsCorrect(string word);

    /// <summary>
    /// Returns a list of replacement suggestions for the misspelled <paramref name="word"/>.
    /// May return an empty collection when no suggestions are available.
    /// </summary>
    IReadOnlyList<string> GetSuggestions(string word);

    /// <summary>
    /// Adds a word to the provider's custom dictionary so it is no longer flagged.
    /// </summary>
    void AddToDictionary(string word);
}
