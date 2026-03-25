namespace MintedTextEditor.Core.Editing;

/// <summary>
/// Abstracts platform clipboard access so core editing logic stays platform-independent.
/// </summary>
public interface IClipboardProvider
{
    /// <summary>Writes plain text to the clipboard.</summary>
    Task SetTextAsync(string text);

    /// <summary>Reads plain text from the clipboard. Returns null if the clipboard has no text.</summary>
    Task<string?> GetTextAsync();
}
