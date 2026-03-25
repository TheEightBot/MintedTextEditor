namespace MintedTextEditor.Core.Editing;

/// <summary>
/// Integration point for platform print services.
/// Implement this interface and pass it to the editor host to provide printing support.
/// </summary>
public interface IPrintProvider
{
    /// <summary>
    /// Begins a print job for the given HTML representation of the document.
    /// The implementation is responsible for invoking the platform print dialog.
    /// </summary>
    /// <param name="html">HTML snapshot of the current document content.</param>
    /// <param name="jobName">Display name shown in the print queue.</param>
    void Print(string html, string jobName = "Document");

    /// <summary>
    /// Returns <c>true</c> when printing is currently available on this platform.
    /// </summary>
    bool IsAvailable { get; }
}
