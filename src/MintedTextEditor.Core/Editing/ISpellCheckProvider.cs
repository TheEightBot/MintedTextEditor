using System.Diagnostics.CodeAnalysis;

namespace MintedTextEditor.Core.Editing;

/// <summary>
/// Planned integration point for spell-check engines.
/// </summary>
/// <remarks>
/// Experimental (<c>MTE001</c>): the editor does not use this interface yet, so implementing it
/// has no effect. Its shape may change once spell checking is wired into the control.
/// </remarks>
[Experimental("MTE001", UrlFormat = "https://github.com/TheEightBot/MintedTextEditor/blob/main/docs/architecture.md#experimental-apis")]
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
