namespace MintedTextEditor.Core.Formatting;

/// <summary>
/// Platform-specific contract for opening a hyperlink URL.
/// </summary>
public interface IHyperlinkHandler
{
    /// <summary>Opens the given URL using the platform's default mechanism.</summary>
    void OpenUrl(string url);
}
